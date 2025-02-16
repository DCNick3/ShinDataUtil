using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FastPngEncoderSharp;
using Newtonsoft.Json;
using ShinDataUtil.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ShinDataUtil.Decompression
{
    public class ShinPictureDecoder
    {
        static ShinPictureDecoder()
        {
            Trace.Assert(BitConverter.IsLittleEndian);
        }
        
        public static (Image<Rgba32>, (int effectiveWidth, int effectiveHeight), bool field20) DecodePicture(ReadOnlySpan<byte> picture)
        {
            var header = MemoryMarshal.Read<PicHeader>(picture);
            var entriesData = MemoryMarshal.Cast<byte, PicHeaderFragmentEntry>(
                picture[Marshal.SizeOf<PicHeader>()..]);

            Trace.Assert(header.magic == 0x34434950);
            Trace.Assert(header.version == 2);
            Trace.Assert(header.fileSize == picture.Length);
            Trace.Assert(header.originX == header.effectiveWidth / 2);
            Trace.Assert(header.originY == header.effectiveHeight 
                         || header.originY == header.effectiveHeight / 2
                         || header.originY == 0);
            Trace.Assert(header.field20 == 1 || header.field20 == 0);
            
            var entries = new PicHeaderFragmentEntry[header.entryCount];
            for (var i = 0; i < entries.Length; i++)
                entries[i] = entriesData[i];

            int totalWidth = header.effectiveWidth, totalHeight = header.effectiveHeight;
            foreach (var entry in entries)
            {
                var size = ShinTextureDecompress.GetImageFragmentSize(entry.GetData(picture));
                totalWidth = Math.Max(totalWidth, entry.x + size.Item1);
                totalHeight = Math.Max(totalHeight, entry.y + size.Item2);
            }

            var image = new Image<Rgba32>(totalWidth, totalHeight);
            foreach (var entry in entries) 
                ShinTextureDecompress.DecodeImageFragment(image, entry.x, entry.y, entry.GetData(picture));

            // make fully transparent pixels have the same values
            // (mainly to make round-trip tests work)
            for (var j = 0; j < image.Height; j++)
            {
                var row = image.DangerousGetPixelRowMemory(j).Span;
                for (var i = 0; i < image.Width; i++)
                    if (row[i].A == 0)
                        row[i] = Color.Transparent;
            }

            return (image, (header.effectiveWidth, header.effectiveHeight), header.field20 != 0);
        }

        public struct FragmentInfo
        {
            public int X, Y, Width, Height, MaxVertToX, MaxVertToY;
            public int DecompressedSize, CompressedSize;
            public bool UseDifferential, UseSeparateAlpha;
            public int OpaqueVertexCount, TransparentVertexCount;
            public ImmutableArray<PicVertexEntry> Vertices;
        }
        
        public static unsafe (Image<Rgba32> fragmentsOverlay, ImmutableArray<FragmentInfo>) DumpPictureFragments(
            ReadOnlySpan<byte> picture, string outname)
        {
            var header = MemoryMarshal.Read<PicHeader>(picture);
            var entriesData = MemoryMarshal.Cast<byte, PicHeaderFragmentEntry>(
                picture[Marshal.SizeOf<PicHeader>()..]);

            Trace.Assert(header.magic == 0x34434950);
            
            var entries = new PicHeaderFragmentEntry[header.entryCount];
            for (var i = 0; i < entries.Length; i++)
                entries[i] = entriesData[i];

            int totalWidth = header.effectiveHeight, totalHeight = header.effectiveWidth;
            foreach (var entry in entries)
            {
                var size = ShinTextureDecompress.GetImageFragmentSize(entry.GetData(picture));
                totalWidth = Math.Max(totalWidth, entry.x + size.Item1);
                totalHeight = Math.Max(totalHeight, entry.y + size.Item2);
            }

            var res = new List<FragmentInfo>();
            
            var image = new Image<Rgba32>(totalWidth, totalHeight);
            var fragmentsOverlay = new Image<Rgba32>(header.effectiveWidth, header.effectiveHeight);
            var colors = new[]
            {
                Color.Blue, Color.Brown, Color.Gray, Color.Lime,
                Color.Aquamarine, Color.IndianRed, Color.LightSkyBlue, Color.Gold, 
                Color.Azure, Color.Coral, 
            };
            var colorIndex = 0;
            
            foreach (var (index, entry) in entries.Select((x, i) => (i, x)))
            {
                if (colorIndex >= colors.Length)
                    colorIndex = 0;

                var fragmentData = entry.GetData(picture);
                var fragmentHeader = MemoryMarshal.Read<PicFragmentHeader>(fragmentData);

                var width = fragmentHeader.width;
                var height = fragmentHeader.height;
                
                //var vertices = fragmentHeader

                var fragmentFilename = Path.Combine(outname, $"fragment-{index}.png");

                var fragmentImage = new Image<Rgba32>(width, height);
                
                ShinTextureDecompress.DecodeImageFragment(fragmentImage, 0, 0, fragmentData);
                
                FastPngEncoder.WritePngToFile(fragmentFilename, fragmentImage);
                
                var vertexData = MemoryMarshal.Cast<byte, PicVertexEntry>(
                        fragmentData.Slice(sizeof(PicFragmentHeader)))
                    .Slice(0, fragmentHeader.opaqueVertexCount + fragmentHeader.transparentVertexCount);

                var vertices = vertexData.ToArray();
                
                fragmentsOverlay.Mutate(o =>
                {
                    o.Fill(colors[colorIndex++], new Rectangle(entry.x, entry.y, width, height));
                });
                
                var differentialStride = (width * 4 + 0xf) & 0x7ffffff0;
                var dictionaryStride = (width + 3) & 0x7ffffffc;
                
                int decompressedSize;
                if (fragmentHeader.UseDifferentialEncoding)
                    /* alignment magic */
                    decompressedSize = differentialStride * height;
                else
                {
                    /* alignment magic */
                    decompressedSize = dictionaryStride * height; /* One dictionary index is 1 byte */
                    if (fragmentHeader.UseSeparateAlpha)
                        decompressedSize *= 2; /* for each pixel we have 1 additional byte for alpha value */
                    decompressedSize += 0x400; /* dictionary size */
                }
                
                res.Add(new FragmentInfo
                {
                    X = entry.x,
                    Y = entry.y,
                    MaxVertToX = vertices.Max(v => v.toX),
                    MaxVertToY = vertices.Max(v => v.toY),
                    Width = width,
                    Height = height,
                    Vertices = vertices.ToImmutableArray(),
                    OpaqueVertexCount = fragmentHeader.opaqueVertexCount,
                    TransparentVertexCount = fragmentHeader.transparentVertexCount,
                    CompressedSize = fragmentHeader.compressedSize,
                    DecompressedSize = decompressedSize,
                    UseDifferential = fragmentHeader.UseDifferentialEncoding,
                    UseSeparateAlpha = fragmentHeader.UseSeparateAlpha,
                });
            }
            

            var fragFilename = Path.Combine(outname, "fragments.json");
            var maskFilename = Path.Combine(outname, "fragmentMask.png");
            
            File.WriteAllText(fragFilename, JsonConvert.SerializeObject(res.ToImmutableArray(), Formatting.Indented));
            FastPngEncoder.WritePngToFile(maskFilename, fragmentsOverlay);

            return (fragmentsOverlay, res.ToImmutableArray());
        }
    }
}