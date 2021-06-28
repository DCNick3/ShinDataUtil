using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace ShinDataUtil.Decompression
{
    public class ShinPictureDecoder
    {
        static ShinPictureDecoder()
        {
            Trace.Assert(BitConverter.IsLittleEndian);
        }
        
        public static (Image<Rgba32>, (int effectiveWidth, int effectiveHeight)) DecodePicture(ReadOnlySpan<byte> picture)
        {
            var header = MemoryMarshal.Read<PictureHeader>(picture);
            var entriesData = MemoryMarshal.Cast<byte, PictureHeaderFragmentEntry>(
                picture[Marshal.SizeOf<PictureHeader>()..]);

            Trace.Assert(header.magic == 0x34434950);
            
            var entries = new PictureHeaderFragmentEntry[header.entryCount];
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

            return (image, (header.effectiveWidth, header.effectiveHeight));
        }

        public struct FragmentInfo
        {
            public int X, Y, Width, Height;
            public int OpaqueVertexCount, TransparentVertexCount;
            public ImmutableArray<ShinTextureDecompress.VertexEntry> Vertices;
        }
        
        public static ImmutableArray<FragmentInfo> DumpPictureFragments(ReadOnlySpan<byte> picture)
        {
            var header = MemoryMarshal.Read<PictureHeader>(picture);
            var entriesData = MemoryMarshal.Cast<byte, PictureHeaderFragmentEntry>(
                picture[Marshal.SizeOf<PictureHeader>()..]);

            Trace.Assert(header.magic == 0x34434950);
            
            var entries = new PictureHeaderFragmentEntry[header.entryCount];
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
            foreach (var entry in entries)
            {
                var (width, height) = ShinTextureDecompress.GetImageFragmentSize(entry.GetData(picture));
                
                var (vertices, opaqueVertexCount, transparentVertexCount) = 
                    ShinTextureDecompress.DecodeImageFragment(image, entry.x, entry.y, entry.GetData(picture));
                res.Add(new FragmentInfo
                {
                    X = entry.x,
                    Y = entry.y,
                    Width = width,
                    Height = height,
                    Vertices = vertices.ToImmutableArray(),
                    OpaqueVertexCount = opaqueVertexCount,
                    TransparentVertexCount = transparentVertexCount
                });
            }

            return res.ToImmutableArray();
        }

        private struct PictureHeader
        {
#pragma warning disable 649
            public uint magic;
            public uint field4;
            public uint field8;
            public uint field12;
            public ushort effectiveWidth;
            public ushort effectiveHeight;
            public uint field20;
            public uint entryCount;
            public uint pictureId;
#pragma warning restore 649
        }

        private struct PictureHeaderFragmentEntry
        {
#pragma warning disable 649
            public ushort x;
            public ushort y;
            public uint offset; /* from the beginning of the picture file */
            public uint size;
#pragma warning restore 649
            public ReadOnlySpan<byte> GetData(ReadOnlySpan<byte> file) => file[(int)offset..(int)(offset + size)];
        }
    }
}