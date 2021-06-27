using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace ShinDataUtil.Compression
{
    /// <summary>
    /// A compressor for textures. WIP
    /// </summary>
    public class ShinTextureCompress
    {
        private Image<Rgba32> _image;
        private Rectangle _boundingRectangle;

        // TODO: implement separate alpha channel
        public static void EncodeDict(Image<Rgba32> source, int dx, int dy, int width, int height,
            Span<Rgba32> dictionary, Span<byte> data, int stride)
        {
            Dictionary<Rgba32, byte> dictIndices = new();

            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    var v = source[i + dx, j + dy];
                    if (!dictIndices.TryGetValue(v, out var val))
                    {
                        val = checked((byte) dictIndices.Count);
                        dictionary[val] = v;
                        dictIndices[v] = val;
                    }

                    data[i] = val;
                }
                
                data = data[stride..];
            }
            
            Trace.Assert(data.Length == 0);
        }

        public static void EncodeDifferential(Image<Rgba32> source, int dx, int dy, int width, int height,
            Span<byte> data, int stride)
        {
            if (height > 0)
            {
                var firstRow = MemoryMarshal.Cast<Rgba32, byte>(source.GetPixelRowSpan(dy)[dx..]);
                firstRow.CopyTo(data[..(width*4)]);
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
        
        public ShinTextureCompress(Image<Rgba32> image)
        {
            _image = image;
            
            int minX = int.MaxValue, minY = int.MaxValue,
                maxX = int.MinValue, maxY = int.MinValue;

            for (var j = 0; j < image.Height; j++)
            {
                var row = image.GetPixelRowSpan(j);
                for (var i = 0; i < image.Width; i++)
                {
                    if (row[i].A <= 0)
                        continue;
                    minX = Math.Min(minX, i);
                    maxX = Math.Max(maxX, i);
                    minY = Math.Min(minY, j);
                    maxY = Math.Max(maxY, j);
                }
            }

            if (minX == int.MaxValue)
                _boundingRectangle = Rectangle.Empty;
            else
                _boundingRectangle = new Rectangle(minX, minY,
                    maxX - minX + 1, maxY - minY + 1);
            
            
        }
        
        
    }
}