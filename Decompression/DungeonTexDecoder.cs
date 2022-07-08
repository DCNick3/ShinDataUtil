using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BCnEncoder.Decoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShinDataUtil.Decompression
{
    public class DungeonTexDecoder
    {
        public static Image<Rgba32> DecodeTex(ReadOnlySpan<byte> tex)
        {
            if (DungeonLzlrDecompressor.CheckHeader(ref tex))
            {
                var compressor = new DungeonLzlrDecompressor();
                tex = compressor.Decompress(tex);
            }

            var header = MemoryMarshal.Read<TexHeader>(tex);

            Trace.Assert(header.Magic == 0x7865742E);
            Trace.Assert(header.Depth == 1); // Texture2D
            Trace.Assert(header.Levels == 1); // ???
            Trace.Assert(header.Target == 1); // NVN_TEXTURE_TARGET_2D


            (int width, int height) size = checked(((int)header.Width, (int)header.Height));

            var data = header.GetData(tex);
            var deswizzledData = new byte[data.Length];
            
            TegraX1Swizzle.DeswizzleTexture(size.width, size.height, header.Format, data, deswizzledData);
            
            var decoder = new BcDecoder();
            var image = decoder.DecodeRawToImageRgba32(deswizzledData, size.width, size.height, 
                NVNTexture.GetCompressionFormat(header.Format));
            
            return image;
        }
    }
}