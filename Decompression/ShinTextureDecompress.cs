using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ShinDataUtil.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace ShinDataUtil.Common
{
}

namespace ShinDataUtil.Decompression
{
    public static class ShinTextureDecompress
    {
        public static void DecodeDict(Image<Rgba32> destination, int dx, int dy, int width, int height,
            ReadOnlySpan<Rgba32> dictionary, ReadOnlySpan<byte> input, int inputStride, ReadOnlySpan<byte> inputAlpha)
        {
            Trace.Assert(inputAlpha.Length == 0 || inputAlpha.Length == input.Length);
            Trace.Assert(dictionary.Length == 0x100);
            Trace.Assert(input.Length == inputStride * height);
            Trace.Assert(width <= inputStride);
            
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var v = dictionary[input[i]];
                    if (inputAlpha.Length > 0)
                    {
                        // 255 seen in higurashi, 0 seen in umineko
                        // are they using different authoring tools?
                        Debug.Assert(v.A == 0);
                        v.A = inputAlpha[i];
                    }

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
                var firstRow = MemoryMarshal.Cast<Rgba32, byte>(destination.DangerousGetPixelRowMemory(dy)[dx..].Span);
                input[..(width*4)].CopyTo(firstRow);
                input = input[inputStride..];

                for (int j = 1; j < height; j++)
                {
                    var previousRow = MemoryMarshal.Cast<Rgba32, byte>(destination.DangerousGetPixelRowMemory(dy + j - 1)[dx..].Span);
                    var row = MemoryMarshal.Cast<Rgba32, byte>(destination.DangerousGetPixelRowMemory(dy + j)[dx..].Span);
                    for (var i = 0; i < width * 4; i++)
                        row[i] = (byte)(previousRow[i] + input[i]);
                    input = input[inputStride..];
                }
            }
        }

        public static unsafe (PicVertexEntry[], int opaqueVertexCount, int transparentVertexCount) 
            DecodeImageFragment(Image<Rgba32> destination, int dx, int dy, ReadOnlySpan<byte> fragment)
        {
            var header = MemoryMarshal.Read<PicFragmentHeader>(fragment);
            var dataOffset = header.alignmentAfterHeader * 2 +
                             (header.opaqueVertexCount + header.transparentVertexCount) * sizeof(PicVertexEntry) +
                             sizeof(PicFragmentHeader);
            var data = fragment.Slice(dataOffset); // MaGiC

            var vertexData = MemoryMarshal.Cast<byte, PicVertexEntry>(
                fragment.Slice(sizeof(PicFragmentHeader)))
                .Slice(0, header.opaqueVertexCount + header.transparentVertexCount);

            Trace.Assert(header.unknown_bool == 0 || header.unknown_bool == 1);
            Trace.Assert(dataOffset % 16 == 0);
            
            var vertices = vertexData.ToArray();
            /* we don't really care about them, as they are only needed for more effective rendering */

            var differentialStride = (header.width * 4 + 0xf) & 0x7ffffff0;
            var dictionaryStride = (header.width + 3) & 0x7ffffffc;
            
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
            var header = MemoryMarshal.Read<PicFragmentHeader>(fragment);
            return (header.width, header.height);
        }

        public static (int, int) GetImageFragmentOffset(ReadOnlySpan<byte> fragment)
        {
            var header = MemoryMarshal.Read<PicFragmentHeader>(fragment);
            return (header.offsetX, header.offsetY);
        }
    }
}