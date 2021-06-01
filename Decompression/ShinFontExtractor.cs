using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FastPngEncoderSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShinDataUtil.Decompression
{
    public static class ShinFontExtractor
    {
        public static unsafe long Extract(ReadOnlySpan<byte> data, string destinationFolder)
        {
            var header = MemoryMarshal.Read<Header>(data);

            Trace.Assert(header.magic == 0x34544e46);
            Trace.Assert(header.version == 1);
            Trace.Assert(data.Length == header.size);
            
            var offsetTable = MemoryMarshal.Cast<byte, int>(data[0x10..0x40010]);

            var seen = new HashSet<int>();
            var written = 0L;
            
            var count = 0;
            foreach (var (index, offset) in offsetTable.ToArray().Select((x, i) => (i, x)))
            {
                if (seen.Contains(offset))
                    continue;
                seen.Add(offset);

                var elementHeader = MemoryMarshal.Read<ElementHeader>(data[offset..]);
                var dataOffset = offset + sizeof(ElementHeader);

                ReadOnlySpan<byte> elementData = default;

                if (elementHeader.compressed_size != 0)
                {
                    var elementDataBuffer = new byte[elementHeader.TexelsSize];
                    var compressedData = data[dataOffset..(dataOffset + elementHeader.compressed_size)];
                    Lz77Decompressor.Decompress(elementDataBuffer, compressedData, 10);

                    elementData = elementDataBuffer;
                }
                else
                    elementData = data[dataOffset..(dataOffset + elementHeader.TexelsSize)];

                var mipmap1Offset = 0;
                var mipmap2Offset = elementHeader.Area;
                var mipmap3Offset = mipmap2Offset + (elementHeader.Area / 4);
                var mipmap4Offset = mipmap3Offset + (elementHeader.Area / 16);
                var mipmap5Offset = mipmap4Offset + (elementHeader.Area / 64); // There is actually no such thing =)

                var mipmap1data = elementData[mipmap1Offset..mipmap2Offset];
                var mipmap2data = elementData[mipmap2Offset..mipmap3Offset];
                var mipmap3data = elementData[mipmap3Offset..mipmap4Offset];
                var mipmap4data = elementData[mipmap4Offset..mipmap5Offset];
                
                var image1 = Image.LoadPixelData<Gray8>(mipmap1data, elementHeader.width, elementHeader.height);
                var image2 = Image.LoadPixelData<Gray8>(mipmap2data, elementHeader.width / 2, elementHeader.height / 2);
                var image3 = Image.LoadPixelData<Gray8>(mipmap3data, elementHeader.width / 4, elementHeader.height / 4);
                var image4 = Image.LoadPixelData<Gray8>(mipmap4data, elementHeader.width / 8, elementHeader.height / 8);

                //using var f = File.Open(, FileMode.Create, FileAccess.Write);

                var path1 = Path.Combine(destinationFolder, $"{index:00000000}-1.png");
                var path2 = Path.Combine(destinationFolder, $"{index:00000000}-2.png");
                var path3 = Path.Combine(destinationFolder, $"{index:00000000}-3.png");
                var path4 = Path.Combine(destinationFolder, $"{index:00000000}-4.png");
                
                // Save first mipmap level
                FastPngEncoder.WritePngToFile(path1, image1);
                FastPngEncoder.WritePngToFile(path2, image2);
                FastPngEncoder.WritePngToFile(path3, image3);
                FastPngEncoder.WritePngToFile(path4, image4);

                written += new FileInfo(path1).Length;
                written += new FileInfo(path2).Length;
                written += new FileInfo(path3).Length;
                written += new FileInfo(path4).Length;

                count++;
            }
            
            return 0;
        }
        
        private struct Header
        {
            // ReSharper disable InconsistentNaming
#pragma warning disable 649
            public uint magic;
            public uint version;
            public uint size;
            public ushort height;
            public ushort width;
            // ReSharper restore InconsistentNaming
#pragma warning restore 649
        }
        
        private struct ElementHeader
        {
            // ReSharper disable InconsistentNaming
#pragma warning disable 649
            public sbyte f_0;
            public sbyte f_1;
            public byte f_2;
            public byte f_3;
            public byte virt_width;
            public byte unused;
            public byte width;
            public byte height;
            public ushort compressed_size;
            // ReSharper restore InconsistentNaming
#pragma warning restore 649

            public int Area => height * width;
            public int TexelsSize => Area + (Area >> 2) + (Area >> 4) + (Area >> 6);
        }
    }
}