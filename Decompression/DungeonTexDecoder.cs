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
        public static Image<Rgba32> DecodeTex(ReadOnlySpan<byte> tex, bool verbose = false)
        {
            if (ShinLZLRDecompressor.CheckHeader(ref tex))
            {
                var decompressor = new ShinLZLRDecompressor();
                tex = decompressor.Decompress(tex);
            }

            var header = MemoryMarshal.Read<TexHeader>(tex);

            Trace.Assert(header.Magic == TexHeader.DefaultMagic);
            Trace.Assert(header.Depth == 1); // Texture2D
            Trace.Assert(header.Target == 1); // NVN_TEXTURE_TARGET_2D

            (int width, int height) size = checked(((int)header.Width, (int)header.Height));

            var neededDataSize = NVNTexture.GetByteSize(header.Format, size.width, size.height);
            
            if (verbose)
            {
                Console.WriteLine($"Texture format  {header.Format}");
                Console.WriteLine($"Levels          {header.Levels}");
                Console.WriteLine($"Data size       {header.DataSize} (needed {neededDataSize})");
            }

            var data = header.GetData(tex);
            var deswizzledData = new byte[data.Length];
            
            TegraX1Swizzle.DeswizzleTexture(size.width, size.height, header.Format, data, deswizzledData);
            
            var decoder = new BcDecoder();
            // due to swizzling limitations, some textures will end up being larger than actually needed by the block compressor
            // slice the array to the needed size, otherwise BCnEncoder trips up on the padding zeroes.
            var image = decoder.DecodeRawToImageRgba32(deswizzledData[..neededDataSize], size.width, size.height, 
                NVNTexture.GetCompressionFormat(header.Format));
            
            return image;
        }
    }
}