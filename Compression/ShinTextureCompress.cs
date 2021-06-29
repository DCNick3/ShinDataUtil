using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FastPngEncoderSharp;
using ShinDataUtil.Common;
using ShinDataUtil.Decompression;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace ShinDataUtil.Compression
{
    public class ShinTextureCompress
    {

        public static void EncodeDict(Image<Rgba32> source, int dx, int dy, int width, int height,
            Span<Rgba32> outputDict, Span<byte> output, int stride, Span<byte> outputAlpha)
        {
            Dictionary<Rgba32, byte> dictIndices = new();

            Trace.Assert(outputAlpha.Length == 0 || outputAlpha.Length == output.Length);
            Trace.Assert(outputDict.Length == 0x100);
            Trace.Assert(output.Length == stride * height);
            Trace.Assert(width <= stride);
            
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var v = source[i + dx, j + dy];
                    if (outputAlpha.Length > 0)
                        v.A = 255;
                    if (!dictIndices.TryGetValue(v, out var val))
                    {
                        val = checked((byte) dictIndices.Count);
                        outputDict[val] = v;
                        dictIndices[v] = val;
                    }

                    output[i] = val;
                    if (outputAlpha.Length != 0)
                        outputAlpha[i] = source[i + dx, j + dy].A;
                }
                
                output = output[stride..];
                if (outputAlpha.Length != 0)
                    outputAlpha = outputAlpha[stride..];
            }
            
            Trace.Assert(output.Length == 0);
        }

        public static void EncodeDifferential(Image<Rgba32> source, int dx, int dy, int width, int height,
            Span<byte> data, int stride)
        {
            if (height > 0)
            {
                var firstRow = MemoryMarshal.Cast<Rgba32, byte>(source.GetPixelRowSpan(dy).Slice(dx, width));
                firstRow.CopyTo(data[..(width * 4)]);
                data = data[stride..];

                for (int j = 1; j < height; j++)
                {
                    var previousRow = MemoryMarshal.Cast<Rgba32, byte>(source.GetPixelRowSpan(dy + j - 1)[dx..]);
                    var row = MemoryMarshal.Cast<Rgba32, byte>(source.GetPixelRowSpan(dy + j)[dx..]);
                    for (var i = 0; i < width * 4; i++)
                    {
                        data[i] = (byte) (row[i] - previousRow[i]);
                    }

                    data = data[stride..];
                }
            }
        }
        
        public static bool EligibleForDictCompression(Image<Rgba32> image) => EligibleForDictCompression(image, 0, 0, image.Width, image.Height);

        public static bool EligibleForDictCompression(Image<Rgba32> image, int dx, int dy, int width, int height)
        {
            HashSet<Rgba32> values = new();
            for (var j = dy; j < dy + height; j++)
            for (var i = dx; i < dx + width; i++)
            {
                values.Add(image[i, j]);
            }

            return values.Count <= 256;
        }
        
        public static bool EligibleForDictCompressionWithSeparateAlpha(Image<Rgba32> image, int dx, int dy, int width, int height)
        {
            HashSet<Rgba32> values = new();
            for (var j = dy; j < dy + height; j++)
            for (var i = dx; i < dx + width; i++)
            {
                var v = image[i, j];
                v.A = 0;
                values.Add(v);
            }

            return values.Count <= 256;
        }

        public static unsafe int EncodeImageFragment(Stream outfrag, Image<Rgba32> image,
            int dx, int dy,
            int offsetX, int offsetY,
            int width, int height
        )
        {
            var differentialStride = (width * 4 + 0xf) & 0x7ffffff0;
            var dictionaryStride = (width + 3) & 0x7ffffffc;

            bool useDifferentialEncoding;
            bool useSeparateAlpha;

            if (EligibleForDictCompression(image, dx, dy, width, height))
            {
                useDifferentialEncoding = false;
                useSeparateAlpha = false;
            }
            else if (EligibleForDictCompressionWithSeparateAlpha(image, dx, dy, width, height))
            {
                useDifferentialEncoding = false;
                useSeparateAlpha = true;
            }
            else
            {
                useDifferentialEncoding = true;
                useSeparateAlpha = false; // don't care, but C# does
            }


            int decompressedSize;
            if (useDifferentialEncoding)
                /* alignment magic */
                decompressedSize = differentialStride * height;
            else
            {
                /* alignment magic */
                decompressedSize = dictionaryStride * height; /* One dictionary index is 1 byte */
                if (useSeparateAlpha)
                    decompressedSize *= 2; /* for each pixel we have 1 additional byte for alpha value */
                decompressedSize += 0x400; /* dictionary size */
            }

            var decompressedBuffer = new byte[decompressedSize];

            if (useDifferentialEncoding)
                EncodeDifferential(image, dx, dy, width, height, decompressedBuffer, differentialStride);
            else
            {
                var alpha = Span<byte>.Empty;
                if (useSeparateAlpha)
                    alpha = decompressedBuffer.AsSpan()[(0x400 + dictionaryStride * height)..];

                /* first goes a dictionary of size 0x400, then (dictionaryStride * header.height) dict indices follow
               after that goes optional separate alpha channel data */

                EncodeDict(image, dx, dy, width, height,
                    MemoryMarshal.Cast<byte, Rgba32>(decompressedBuffer.AsSpan()[..0x400]),
                    decompressedBuffer.AsSpan().Slice(0x400, dictionaryStride * height), dictionaryStride, alpha);
            }

            var (compressedBuffer, compressedSize) = new Lz77Compressor(12).Compress(decompressedBuffer);

            // this will work for now
            var vertices = ImmutableArray.Create(
                new PicVertexEntry
                {
                    fromX = 0,
                    fromY = 0,
                    toX = checked((ushort) (width - 2)),
                    toY = checked((ushort) (height - 2))
                });
            ushort opaqueVerticesCount = 0;
            ushort transparentVerticesCount = 1;
            Trace.Assert(vertices.Length == (opaqueVerticesCount + transparentVerticesCount));

            var useCompressed = compressedSize < decompressedSize;
            if (compressedSize > ushort.MaxValue)
            {
                // a very scary kludge
                // if we can't fit with compressed data - put uncompressed xD
                useCompressed = false;
            }
            
            var header = new PicFragmentHeader
            {
                height = checked((ushort) height),
                width = checked((ushort) width),
                offsetX = checked((ushort) offsetX),
                offsetY = checked((ushort) offsetY),
                opaqueVertexCount = opaqueVerticesCount,
                transparentVertexCount = transparentVerticesCount,
                unknown_bool =
                    0, // I don't really know what it means, game ignores it, most fragments have it set to zero
                compressedSize = checked((ushort) (useCompressed ? compressedSize : 0)),

                UseDifferentialEncoding = useDifferentialEncoding,
                UseSeparateAlpha = useSeparateAlpha
            };

            var dataOffsetWithoutAlignment = sizeof(PicFragmentHeader) +
                                             sizeof(PicVertexEntry) *
                                             (opaqueVerticesCount + transparentVerticesCount);
            var dataAlignment = (0x10 - dataOffsetWithoutAlignment % 0x10) % 0x10;
            Trace.Assert(dataAlignment % 2 == 0);
            header.alignmentAfterHeader = checked((ushort) (dataAlignment / 2));

            var p1 = outfrag.Position;
            
            outfrag.Write(SpanUtil.AsBytes(ref header));
            outfrag.Write(MemoryMarshal.Cast<PicVertexEntry, byte>(vertices.AsSpan()));

            Trace.Assert(dataOffsetWithoutAlignment == outfrag.Position - p1);

            for (var i = 0; i < dataAlignment; i++)
                outfrag.WriteByte(0);
            if (useCompressed)
                outfrag.Write(compressedBuffer.AsSpan()[..compressedSize]);
            else
                outfrag.Write(decompressedBuffer.AsSpan()[..decompressedSize]);

            return dataOffsetWithoutAlignment + dataAlignment + (useCompressed ? compressedSize : decompressedSize);
        }



    }
}