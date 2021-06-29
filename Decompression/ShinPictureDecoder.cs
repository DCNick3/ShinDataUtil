using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using ShinDataUtil.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

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

            return (image, (header.effectiveWidth, header.effectiveHeight), header.field20 != 0);
        }

        public struct FragmentInfo
        {
            public int X, Y, Width, Height, MaxVertToX, MaxVertToY;
            public int OpaqueVertexCount, TransparentVertexCount;
            public ImmutableArray<PicVertexEntry> Vertices;
        }
        
        public static (Image<Rgba32> fragmentsOverlay, ImmutableArray<FragmentInfo>) DumpPictureFragments(ReadOnlySpan<byte> picture)
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
                Rgba32.Blue, Rgba32.Brown, Rgba32.Gray, Rgba32.Lime,
                Rgba32.Aquamarine, Rgba32.IndianRed, Rgba32.LightSkyBlue, Rgba32.Gold, 
                Rgba32.Azure, Rgba32.Coral, 
            };
            var colorIndex = 0;
            
            foreach (var entry in entries)
            {
                if (colorIndex >= colors.Length)
                    colorIndex = 0;
                
                var (width, height) = ShinTextureDecompress.GetImageFragmentSize(entry.GetData(picture));
                
                var (vertices, opaqueVertexCount, transparentVertexCount) = 
                    ShinTextureDecompress.DecodeImageFragment(image, entry.x, entry.y, entry.GetData(picture));
                
                fragmentsOverlay.Mutate(o =>
                {
                    o.Fill(colors[colorIndex++], new Rectangle(entry.x, entry.y, width, height));
                });
                
                res.Add(new FragmentInfo
                {
                    X = entry.x,
                    Y = entry.y,
                    MaxVertToX = vertices.Max(v => v.toX),
                    MaxVertToY = vertices.Max(v => v.toY),
                    Width = width,
                    Height = height,
                    Vertices = vertices.ToImmutableArray(),
                    OpaqueVertexCount = opaqueVertexCount,
                    TransparentVertexCount = transparentVertexCount
                });
            }

            return (fragmentsOverlay, res.ToImmutableArray());
        }
    }
}