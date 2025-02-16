using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Dataflow;
using FastPngEncoderSharp;
using ShinDataUtil.Common;
using ShinDataUtil.Decompression;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

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
            
            // handle out-of-bounds source by repeating the value at the border
            var effectiveWidth = width;
            var effectiveHeight = height;
            if (dx + width > source.Width)
                effectiveWidth -= dx + width - source.Width;
            if (dy + height > source.Height)
                effectiveHeight -= dy + height - source.Height;

            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var x = dx + Math.Min(i, effectiveWidth - 1);
                    var y = dy + Math.Min(j, effectiveHeight - 1);

                    var v = source[x, y];
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
                        outputAlpha[i] = source[x, y].A;
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
            // handle out-of-bounds source by repeating the value at the border
            var effectiveWidth = width;
            var effectiveHeight = height;
            if (dx + width > source.Width)
                effectiveWidth -= dx + width - source.Width;
            if (dy + height > source.Height)
                effectiveHeight -= dy + height - source.Height;
            if (height > 0)
            {
                var firstRow = source.DangerousGetPixelRowMemory(dy).Span.Slice(dx, effectiveWidth);
                firstRow.CopyTo(MemoryMarshal.Cast<byte, Rgba32>(data)[..(effectiveWidth * 4)]);
                for (var i = effectiveWidth; i < width; i++)
                    // repeat the same stuff
                    MemoryMarshal.Cast<byte, Rgba32>(data)[i] = source[dx + effectiveWidth - 1, dy];
                data[(width * 4)..stride].Fill(0);
                data = data[stride..];

                for (var j = 1; j < effectiveHeight; j++)
                {
                    var previousRow = MemoryMarshal.Cast<Rgba32, byte>(source.DangerousGetPixelRowMemory(dy + j - 1).Span[dx..]);
                    var row = MemoryMarshal.Cast<Rgba32, byte>(source.DangerousGetPixelRowMemory(dy + j).Span[dx..]);
                    for (var i = 0; i < effectiveWidth * 4; i++)
                    {
                        data[i] = (byte) (row[i] - previousRow[i]);
                    }

                    for (var i = effectiveWidth * 4; i < width * 4; i++)
                    {
                        data[i] = (byte) (row[(effectiveWidth - 1) * 4 + i % 4] - previousRow[(effectiveWidth - 1) * 4 + i % 4]);
                    }

                    data[(width * 4)..stride].Fill(0);
                    data = data[stride..];
                }

                for (var j = effectiveHeight; j < height; j++)
                {
                    // keep it the same
                    data[..stride].Fill(0);
                    data = data[stride..];
                }
            }
        }
        
        public static bool EligibleForDictCompression(Image<Rgba32> image) => EligibleForDictCompression(image, 0, 0, image.Width, image.Height);

        public static bool EligibleForDictCompression(Image<Rgba32> image, int dx, int dy, int width, int height)
        {
            // handle out-of-bounds source by repeating the value at the border
            var effectiveWidth = width;
            var effectiveHeight = height;
            if (dx + width > image.Width)
                effectiveWidth -= dx + width - image.Width;
            if (dy + height > image.Height)
                effectiveHeight -= dy + height - image.Height;
            HashSet<Rgba32> values = new();
            for (var j = dy; j < dy + effectiveHeight; j++)
            for (var i = dx; i < dx + effectiveWidth; i++)
            {
                values.Add(image[i, j]);
            }

            return values.Count <= 256;
        }
        
        public static bool EligibleForDictCompressionWithSeparateAlpha(Image<Rgba32> image, int dx, int dy, int width, int height)
        {
            // handle out-of-bounds source by repeating the value at the border
            var effectiveWidth = width;
            var effectiveHeight = height;
            if (dx + width > image.Width)
                effectiveWidth -= dx + width - image.Width;
            if (dy + height > image.Height)
                effectiveHeight -= dy + height - image.Height;
            HashSet<Rgba32> values = new();
            for (var j = dy; j < dy + effectiveHeight; j++)
            for (var i = dx; i < dx + effectiveWidth; i++)
            {
                var v = image[i, j];
                v.A = 0;
                values.Add(v);
            }

            return values.Count <= 256;
        }

        public class FragmentCompressionConfig
        {
            public bool Quantize { get; set; }
            public bool Dither { get; set; }
            public bool LosslessAlpha { get; set; }
        }
        
        public static unsafe int EncodeImageFragment(Stream outfrag, Image<Rgba32> image,
            int dx, int dy,
            int offsetX, int offsetY,
            int width, int height,
            FragmentCompressionConfig fragmentCompressionConfig
        )
        {
            if (fragmentCompressionConfig.Quantize)
            {
                // copy the image region ignoring alpha quantize it, restore alpha
                var subimg = new Image<Rgba32>(width, height);
                for (var j = 0; j < height; j++)
                {
                    var srcRow = image.DangerousGetPixelRowMemory(Math.Min(dy + j, image.Height - 1)).Span[dx..];
                    var row = subimg.DangerousGetPixelRowMemory(j).Span;
                    for (var i = 0; i < width; i++)
                    {
                        var v = srcRow[Math.Min(i, srcRow.Length - 1)];
                        if (v.A == 0)
                            v = Color.Transparent;
                        if (fragmentCompressionConfig.LosslessAlpha)
                            v.A = 255;

                        row[i] = v;
                    }
                }

                subimg.Mutate(o =>
                {
                    o.Quantize(new WuQuantizer(new QuantizerOptions
                        {
                            Dither = fragmentCompressionConfig.Dither ? KnownDitherings.FloydSteinberg : null,
                            MaxColors = 256
                        }
                    ));
                });
                
                for (var j = 0; j < height; j++)
                {
                    var srcRow = image.DangerousGetPixelRowMemory(Math.Min(dy + j, image.Height - 1)).Span[dx..];
                    var row = subimg.DangerousGetPixelRowMemory(j).Span;
                    for (var i = 0; i < width; i++)
                    {
                        var v = srcRow[Math.Min(i, srcRow.Length - 1)];
                        if (fragmentCompressionConfig.LosslessAlpha)
                            row[i].A = v.A;
                    }
                }
                
                dx = 0;
                dy = 0;
                image = subimg;
            }
            
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

            if (fragmentCompressionConfig.Quantize)
                Trace.Assert(!useDifferentialEncoding);

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