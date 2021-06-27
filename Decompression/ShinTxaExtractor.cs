using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        public static long Extract(ReadOnlySpan<byte> data, string destinationDirectory, bool ignoreFileSize = false)
        {
            var header = MemoryMarshal.Read<TxaHeader>(data);

            var writtenBytes = 0L;
            
            Trace.Assert(header.magic == 0x34415854);
            Trace.Assert(header.version == 2);
            if (!ignoreFileSize)
                Trace.Assert(header.file_size == data.Length);
            Trace.Assert(header.always_zero == 0);

            var virtualIndexToActualIndex = new int[header.count];
            
            using var f = File.CreateText(Path.Combine(destinationDirectory, "index.txt"));

            var entries = new List<TxaEntryHeader>();
            
            var offset = sizeof(TxaHeader);
            for (var i = 0; i < header.count; i++)
            {
                var entry = MemoryMarshal.Read<TxaEntryHeader>(data[offset..]);
                var nameData = data[(offset + sizeof(TxaEntryHeader))..(offset+entry.entry_length)];
                while (nameData.Length > 0 && nameData[^1] == 0)
                {
                    nameData = nameData[..^1];
                }
                // we always have a zero terminator
                Trace.Assert(entry.entry_length - sizeof(TxaEntryHeader) > nameData.Length);

                entries.Add(entry);
                
                var name = Encoding.UTF8.GetString(nameData);

                // Not sure why they are here and what they do, but (i == entry.virtual_index) is __usually__ true, but not always
                Trace.Assert(virtualIndexToActualIndex[entry.virtual_index] == 0);
                virtualIndexToActualIndex[entry.virtual_index] = i;

                var dataOffset = checked((int) entry.data_offset);
                var size = checked((int)entry.data_compressed_size);
                if (size == 0)
                    size = checked((int)entry.data_decompressed_size);
                
                var elementTexture = DecodeElement(data[dataOffset..(dataOffset + size)], ref entry,header.use_dict != 0);

                var filename = Path.Combine(destinationDirectory, $"{name}.png");
                FastPngEncoder.WritePngToFile(filename, elementTexture);
                
                writtenBytes += new FileInfo(filename).Length;
                
                f.WriteLine($"{i:000} -> {entry.virtual_index:000} {JsonConvert.SerializeObject(name)}");
                
                offset += entry.entry_length;
                // all the entries are aligned to 4-byte boundary
                Trace.Assert(offset % 0x4 == 0);
            }

            f.Flush();
            writtenBytes += f.BaseStream.Length;
            
            Trace.Assert(entries.Sum(e => e.entry_length) == header.index_size);
            
            return writtenBytes;
        }

        private static Image<Rgba32> DecodeElement(ReadOnlySpan<byte> rawElementData, ref TxaEntryHeader header, bool useDict)
        {
            var data = rawElementData;
            if (header.data_compressed_size != 0)
            {
                var decompressed = new byte[header.data_decompressed_size];
                Lz77Decompressor.Decompress(decompressed, rawElementData, 12);
                data = decompressed;
            }

            var image = new Image<Rgba32>(header.width, header.height);

            if (useDict)
                ShinTextureDecompress.DecodeDict(image, 0, 0, header.width, header.height,
                    MemoryMarshal.Cast<byte, Rgba32>(data[..1024]), 
                    data[1024..], (image.Width + 3) & 0x7fffc, ReadOnlySpan<byte>.Empty);
            else
                ShinTextureDecompress.DecodeDifferential(image, 0, 0, header.width, header.height,
                    data, (4 * image.Width + 15) & 0x7fff0);

            return image;
        }
    }
}