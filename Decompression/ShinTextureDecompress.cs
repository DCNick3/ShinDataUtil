using System;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace ShinDataUtil.Decompression
{
    public static class ShinTextureDecompress
    {
        public static void DecodeDict(Image<Rgba32> destination, int dx, int dy, int width, int height,
            ReadOnlySpan<Rgba32> dictionary, ReadOnlySpan<byte> input, int inputStride, ReadOnlySpan<byte> inputAlpha)
        {
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var v = dictionary[input[i]];
                    if (inputAlpha.Length > 0)
                        v.A = inputAlpha[i];
                    destination[dx + i, dy + j] = v;
                }

                input = input[inputStride..];
                if (inputAlpha.Length != 0)
                    inputAlpha = inputAlpha[inputStride..];
            }
        }

        public static void DecodeDifferential(Image<Rgba32> destination, int dx, int dy, int width, int height, 
            ReadOnlySpan<byte> input, int inputStride)
        {
            if (height > 0)
            {
                var firstRow = MemoryMarshal.Cast<Rgba32, byte>(destination.GetPixelRowSpan(dy)[dx..]);
                input[..(width*4)].CopyTo(firstRow);
                input = input[inputStride..];

                for (int j = 1; j < height; j++)
                {
                    var previousRow = MemoryMarshal.Cast<Rgba32, byte>(destination.GetPixelRowSpan(dy + j - 1)[dx..]);
                    var row = MemoryMarshal.Cast<Rgba32, byte>(destination.GetPixelRowSpan(dy + j)[dx..]);
                    for (var i = 0; i < width * 4; i++)
                        row[i] = (byte)(previousRow[i] + input[i]);
                    input = input[inputStride..];
                }
            }
        }

        public static (VertexEntry[], int opaqueVertexCount, int transparentVertexCount) 
            DecodeImageFragment(Image<Rgba32> destination, int dx, int dy, ReadOnlySpan<byte> fragment)
        {
            var header = MemoryMarshal.Read<FragmentHeader>(fragment);
            var data = fragment[(header.alignmentAfterHeader * 2 + (header.opaqueVertexCount + header.transparentVertexCount) * 8 + 0x14)..]; // MaGiC

            var vertexData = MemoryMarshal.Cast<byte, VertexEntry>(fragment[0x14..(0x14 + (header.opaqueVertexCount + header.transparentVertexCount) * 8)]);
            
            var vertices = vertexData.ToArray();
            /* we don't really care about them, as they are only needed for more effective rendering */

            var differentialStride = header.width * 4 + 0xf & 0x7ffffff0;
            var dictionaryStride = header.width + 3 & 0x7ffffffc;
            
            // This holds for pictures, but not for bustup
            //Trace.Assert(header.offsetX == 0 && header.offsetY == 0);
            
            if (header.compressedSize != 0)
            {
                int outSize;
                if (header.UseDifferentialEncoding)
                    /* alignment magic */
                    outSize = differentialStride * header.height;
                else
                {
                    /* alignment magic */
                    outSize = dictionaryStride * header.height; /* One dictionary index is 1 byte */
                    if (header.UseSeparateAlpha)
                        outSize *= 2; /* for each pixel we have 1 additional byte for alpha value */
                    outSize += 0x400; /* dictionary size */
                }

                var outBuffer = new byte[outSize];
                Lz77Decompressor.Decompress(outBuffer, data, 12);
                data = outBuffer;
            }


            if (header.UseDifferentialEncoding)
            {
                DecodeDifferential(destination, dx, dy, header.width, header.height, data, differentialStride);
            }
            else
            {
                ReadOnlySpan<byte> inputAlpha = ReadOnlySpan<byte>.Empty;
                if (header.UseSeparateAlpha)
                    inputAlpha = data[(0x400 + dictionaryStride * header.height)..];
                
                /* first goes a dictionary of size 0x400, then (dictionaryStride * header.height) dict indices follow
                   after that goes optional separate alpha channel data */
                
                DecodeDict(destination, dx, dy, header.width, header.height,
                    MemoryMarshal.Cast<byte, Rgba32>(data[..0x400]),
                    data[0x400..(0x400 + dictionaryStride * header.height)], dictionaryStride, inputAlpha);
            }

            return (vertices, header.opaqueVertexCount, header.transparentVertexCount);
        }
        
        public static (int, int) GetImageFragmentSize(ReadOnlySpan<byte> fragment)
        {
            var header = MemoryMarshal.Read<FragmentHeader>(fragment);
            return (header.width, header.height);
        }

        public static (int, int) GetImageFragmentOffset(ReadOnlySpan<byte> fragment)
        {
            var header = MemoryMarshal.Read<FragmentHeader>(fragment);
            return (header.offsetX, header.offsetY);
        }
        
        private struct FragmentHeader
        {
#pragma warning disable 649
            public ushort compressionFlags;
            public ushort opaqueVertexCount;
            public ushort transparentVertexCount;
            public ushort alignmentAfterHeader;
            public ushort offsetX;
            public ushort offsetY;
            public ushort width;
            public ushort height;
            public ushort compressedSize;
#pragma warning restore 649

            public bool UseDifferentialEncoding => (compressionFlags >> 1 & 1) == 0;
            public bool UseSeparateAlpha => (compressionFlags & 1) == 0;
        }

        public struct VertexEntry
        {
#pragma warning disable 649
            public ushort fromX;
            public ushort fromY;
            public ushort toX;
            public ushort toY;
#pragma warning restore 649

            public bool Contains(int i, int j)
            {
                return i >= fromX && i < toX && j >= fromY && j < toY;
            }
        }

    }
}