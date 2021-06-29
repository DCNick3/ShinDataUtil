using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using ShinDataUtil.Common;
using ShinDataUtil.Decompression;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ShinDataUtil.Compression
{
    public unsafe class ShinTxaEncoder
    {
        private static Regex IndexRegex = new Regex(@"(\d{3,}) -> (\d{3,}) ("".*"")");

        class IndexEntry
        {
            public int Index;
            public int VirtualIndex;
            public string Name = null!;
            public int DataOffset;
            public int DecompressedSize;
            public int CompressedSize;
            public Image<Rgba32> Image = null!;
        }

        static void CompressElement(Stream outtxa, Image<Rgba32> image, IndexEntry indexEntry, bool useDict)
        {
            int strideBytes, neededSize;
            if (useDict)
            {
                strideBytes = (image.Width + 3) & 0x7fffc;
                neededSize = 1024 + image.Height * strideBytes;
            }
            else
            {
                strideBytes = (4 * image.Width + 15) & 0x7fff0;
                neededSize = image.Height * strideBytes;
            }

            var buffer = ArrayPool<byte>.Shared.Rent(neededSize);
            try
            {
                if (useDict)
                    ShinTextureCompress.EncodeDict(image, 0, 0, image.Width, image.Height,
                        MemoryMarshal.Cast<byte, Rgba32>(buffer.AsSpan()[..1024]),
                        buffer.AsSpan()[1024..neededSize], strideBytes, Span<byte>.Empty);
                else
                    ShinTextureCompress.EncodeDifferential(image, 0, 0, image.Width, image.Height,
                        buffer.AsSpan()[..neededSize], strideBytes);

                indexEntry.DecompressedSize = neededSize;
                indexEntry.CompressedSize = 0;

                var compressor = new Lz77Compressor(12);
                var (compressed, actualCompressedSize) = compressor.Compress(buffer.AsSpan()[..neededSize]);

                if (actualCompressedSize < neededSize)
                {
                    indexEntry.CompressedSize = actualCompressedSize;
                    outtxa.Write(compressed, 0, actualCompressedSize);
                }
                else
                    outtxa.Write(buffer, 0, neededSize);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static void BuildTxa(Stream outtxa, string sourceDirectory)
        {
            var indexData = File.ReadAllLines(Path.Combine(sourceDirectory, "index.txt"));
            var index = indexData.Select(_ =>
            {
                var m = IndexRegex.Match(_);
                
                var res = new IndexEntry
                {
                    Index = int.Parse(m.Groups[1].Value),
                    VirtualIndex = int.Parse(m.Groups[2].Value),
                    Name = JsonConvert.DeserializeObject<string>(m.Groups[3].Value)
                };
                using var fs = File.OpenRead(Path.Combine(sourceDirectory, res.Name + ".png"));
                res.Image = Image.Load<Rgba32>(fs, new PngDecoder());
                return res;
            }).OrderBy(_ => _.Index).ToImmutableArray();

            var useDict = index.All(x => ShinTextureCompress.EligibleForDictCompression(x.Image));
            
            var headSize = sizeof(TxaHeader);
            foreach (var entry in index)
            {
                headSize += sizeof(TxaEntryHeader);
                headSize += Encoding.UTF8.GetByteCount(entry.Name) + 1;
                headSize += (4 - headSize % 4) % 4;
                Trace.Assert(headSize % 4 == 0);
            }
            
            outtxa.SetLength(0);
            // here will be data!
            outtxa.Seek(headSize, SeekOrigin.Begin);

            var maxDecompressedSize = 0;
            
            foreach (var entry in index)
            {
                entry.DataOffset = checked((int)outtxa.Position);
                CompressElement(outtxa, entry.Image, entry, useDict);
                maxDecompressedSize = Math.Max(entry.DecompressedSize, maxDecompressedSize);
            }
            
            // now write all the headers
            
            outtxa.Seek(0, SeekOrigin.Begin);
            var header = new TxaHeader
            {
                magic = 0x34415854,
                version = 2,
                file_size = checked((uint)outtxa.Length),
                use_dict = useDict ? 1U : 0U,
                count = checked((uint)index.Length),
                max_decompressed_size = checked((uint)maxDecompressedSize),
                index_size = checked((uint)(headSize - sizeof(TxaHeader))),
                always_zero = 0
            };
            outtxa.Write(SpanUtil.AsBytes(ref header));
            foreach (var entry in index)
            {
                var nameSizeWithAlignment = Encoding.UTF8.GetByteCount(entry.Name);
                var nameTrailingZerosCount = 1 + (4 - (nameSizeWithAlignment + sizeof(TxaEntryHeader) + 1) % 4) % 4;
                nameSizeWithAlignment += nameTrailingZerosCount;
                
                var entryHeader = new TxaEntryHeader
                {
                    entry_length = checked((ushort)(sizeof(TxaEntryHeader) + nameSizeWithAlignment)),
                    virtual_index = checked((ushort)entry.VirtualIndex),
                    width = checked((ushort)entry.Image.Width),
                    height = checked((ushort)entry.Image.Height),
                    data_offset = checked((uint)entry.DataOffset),
                    data_compressed_size = checked((uint)entry.CompressedSize),
                    data_decompressed_size = checked((uint)entry.DecompressedSize)
                };
                outtxa.Write(SpanUtil.AsBytes(ref entryHeader));
                outtxa.Write(Encoding.UTF8.GetBytes(entry.Name));
                for (var i = 0; i < nameTrailingZerosCount; i++)
                    outtxa.WriteByte(0);
            }
            
            Trace.Assert(outtxa.Position == headSize);
            
            outtxa.Flush();
        }
    }
}