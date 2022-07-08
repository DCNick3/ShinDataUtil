using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using ShinDataUtil.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace ShinDataUtil.Compression
{
    public static class DungeonTexEncoder
    {
        public static ReadOnlySpan<byte> Encode(Image<Rgba32> image, NVNTexFormat format, uint levels)
        {
            var encoder = new BcEncoder();

            encoder.OutputOptions.Format = NVNTexture.GetCompressionFormat(format);
            encoder.OutputOptions.GenerateMipMaps = levels > 1;
            encoder.OutputOptions.Quality = CompressionQuality.Balanced;

            var texDataMips = encoder.EncodeToRawBytes(image);
            //currently only 1st mip
            var swizzledData = new byte[texDataMips[0].Length];
            TegraX1Swizzle.SwizzleTexture(image.Width, image.Height, format, texDataMips[0], swizzledData);

            TexHeader texHeader = new TexHeader();
            texHeader.Magic = TexHeader.DefaultMagic;
            texHeader.Format = format;
            texHeader.Target = 1; //all konosuba textures have it
            texHeader.Width = (uint)image.Width;
            texHeader.Height = (uint)image.Height;
            texHeader.Depth = 1; //Same as target
            texHeader.Levels = 1/*(uint)texDataMips.Length*/;
            texHeader.DataOffset = 256; //Value from random texture, IDK for what extended header padding may be used
            texHeader.DataSize = (uint)swizzledData.Length;

            var headerData = SpanUtil.AsReadOnlyBytes(ref texHeader);
            
            var outStream = new MemoryStream();

            outStream.Write(headerData);
            outStream.Write(new byte[texHeader.DataOffset - TexHeader.HeaderSize]); //padding
            
            outStream.Write(swizzledData);

            return outStream.ToArray();
        }
    }
}
