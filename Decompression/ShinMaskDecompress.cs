using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShinDataUtil.Decompression
{
    public static class ShinMaskDecompress
    {

        public static unsafe Image<L8> Decompress(ReadOnlySpan<byte> data)
        {
            var header = MemoryMarshal.Read<Header>(data);
            
            Trace.Assert(header.magic == 860574541);
            Trace.Assert(header.version == 1);
            Trace.Assert(header.file_size == data.Length);
            Trace.Assert(header.hell1 == 0);
            Trace.Assert(header.hell2 == 0);
            Trace.Assert(header.hell3 == 0);

            var dataOffset = sizeof(Header);

            var imageData = data.Slice(dataOffset);
            var decompressedSize = header.height * header.width;
            if (header.compressed_size != 0)
            {
                var decompressed = new byte[decompressedSize];
                Lz77Decompressor.Decompress(decompressed, imageData, 12);
                imageData = decompressed;
            }

            return Image.LoadPixelData<L8>(new Configuration(), imageData, header.width, header.height); 
        }
        
        private struct Header
        {
#pragma warning disable 649
            // ReSharper disable InconsistentNaming
            public uint magic;
            public uint version;
            public uint file_size;
            public ushort width;
            public ushort height;
            public uint compressed_size;
            public uint hell1;
            public uint hell2;
            public uint hell3;
#pragma warning restore 649
            // ReSharper restore InconsistentNaming
        }
    }
}