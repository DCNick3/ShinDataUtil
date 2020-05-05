using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FastPngEncoderSharp;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShinDataUtil.Decompression
{
    public static unsafe class ShinTxaExtractor
    {
        public static long Extract(ReadOnlySpan<byte> data, string destinationDirectory)
        {
            var header = MemoryMarshal.Read<Header>(data);

            var writtenBytes = 0L;
            
            Trace.Assert(header.magic == 0x34415854);
            Trace.Assert(header.version == 2);
            Trace.Assert(header.file_size == data.Length);

            var virtualIndexToActualIndex = new int[header.count];
            
            using var f = File.CreateText(Path.Combine(destinationDirectory, "index.txt"));

            var offset = sizeof(Header);
            for (var i = 0; i < header.count; i++)
            {
                var entry = MemoryMarshal.Read<EntryHeader>(data[offset..]);
                var nameData = data[(offset + sizeof(EntryHeader))..(offset+entry.length)];
                while (nameData.Length > 0 && nameData[^1] == 0)
                    nameData = nameData[..^1];

                var name = Encoding.UTF8.GetString(nameData);

                Trace.Assert(virtualIndexToActualIndex[entry.virtual_index] == 0);
                virtualIndexToActualIndex[entry.virtual_index] = i;

                var dataOffset = checked((int) entry.offset);
                var size = checked((int)entry.compressed_size);
                if (size == 0)
                    size = checked((int)entry.decompressed_size);
                
                var elementTexture = DecodeElement(data[dataOffset..(dataOffset + size)], ref entry,header.use_dict != 0);

                var filename = Path.Combine(destinationDirectory, $"{name}.png");
                FastPngEncoder.WritePngToFile(filename, elementTexture);
                
                writtenBytes += new FileInfo(filename).Length;
                
                f.WriteLine($"{i:000} -> {entry.virtual_index:000} {JsonConvert.SerializeObject(name)}");
                
                offset += entry.length;
            }

            f.Flush();
            writtenBytes += f.BaseStream.Length;
            
            return writtenBytes;
        }

        private static Image<Rgba32> DecodeElement(ReadOnlySpan<byte> rawElementData, ref EntryHeader header, bool useDict)
        {
            var data = rawElementData;
            if (header.compressed_size != 0)
            {
                var decompressed = new byte[header.decompressed_size];
                Lz77Decompressor.Decompress(decompressed, rawElementData, 12);
                data = decompressed;
            }

            var image = new Image<Rgba32>(header.width, header.height);

            ;
            
            if (useDict)
                ShinTextureDecompress.DecodeDict(image, 0, 0, header.width, header.height,
                    MemoryMarshal.Cast<byte, Rgba32>(data[..1024]), 
                    data[1024..], (image.Width + 3) & 0x7fffc, ReadOnlySpan<byte>.Empty);
            else
                ShinTextureDecompress.DecodeDifferential(image, 0, 0, header.width, header.height,
                    data, (4 * image.Width + 15) & 0x7fff0);

            return image;
        }

        private struct Header
        {
#pragma warning disable 649
            // ReSharper disable InconsistentNaming
            public uint magic;
            public uint version;
            public uint file_size;
            public uint use_dict;
            public uint count;
            public uint f_20;
            public uint index_size;
            public uint f_28;
#pragma warning restore 649
            // ReSharper restore InconsistentNaming
        }
        
        private struct EntryHeader
        {
#pragma warning disable 649
            // ReSharper disable InconsistentNaming
            public ushort length;
            public ushort virtual_index;
            public ushort width;
            public ushort height;
            public uint offset;
            public uint compressed_size;
            public uint decompressed_size;
#pragma warning restore 649
            // ReSharper restore InconsistentNaming

        }
    }
}