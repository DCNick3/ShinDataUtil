using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace ShinDataUtil.Compression
{
    /// <summary>
    /// A compressor for textures. WIP
    /// </summary>
    public class TextureCompressor
    {
        private Image<Rgba32> _image;
        private Rectangle _boundingRectangle;
        
        public TextureCompressor(Image<Rgba32> image)
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