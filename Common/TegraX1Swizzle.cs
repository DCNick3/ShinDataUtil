using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ShinDataUtil
{
    // the basis of the code here was borrowed from https://github.com/KillzXGaming/Switch-Toolbox/blob/3c2526bedcf6097edd42daea79d20405b3621b4d/Switch_Toolbox_Library/Texture%20Decoding/Switch/TegraX1Swizzle.cs#L9
    public class TegraX1Swizzle
    {
        /*
        public static List<uint[]> GenerateMipSizes(int Width, int Height, int Depth, int SurfaceCount, int MipCount, int ImageSize)
        {
            List<uint[]> mipMapSizes = new List<uint[]>();

            uint bpp = 16; //STGenericTexture.GetBytesPerPixel(Format);
            uint blkWidth = 4; //STGenericTexture.GetBlockWidth(Format);
            uint blkHeight = 4; //STGenericTexture.GetBlockHeight(Format);

            var arrayCount = SurfaceCount;

            for (var arrayLevel = 0; arrayLevel < arrayCount; arrayLevel++)
            {
                uint[] mipOffsets = new uint[MipCount];

                for (var mipLevel = 0; mipLevel < MipCount; mipLevel++)
                {
                    var width = Math.Max(1, Width >> mipLevel);
                    var height = Math.Max(1, Height >> mipLevel);
                    var depth = Math.Max(1, Depth >> mipLevel);

                    var size = DivRoundUp(width, blkWidth) * DivRoundUp(height, blkHeight) * bpp;
                    mipOffsets[mipLevel] = size;
                }

                mipMapSizes.Add(mipOffsets);
            }

            return mipMapSizes;
        }*/

        public static void DeswizzleTexture(int width, int height, ReadOnlySpan<byte> sourceData,
            Span<byte> targetData, bool linearTileMode = false)
        {
            var blkHeight = 4; //STGenericTexture.GetBlockHeight(Format);
            var blockHeight = GetBlockHeight(DivRoundUp(height, blkHeight));
            var blockHeightLog2 = Convert.ToString(blockHeight, 2).Length - 1;
            DeswizzleTexture(width, height, sourceData, targetData, blockHeightLog2, linearTileMode);
        }

        public static void DeswizzleTexture(int width, int height, ReadOnlySpan<byte> sourceData,
            Span<byte> targetData, int blockHeightLog2, bool linearTileMode = false)
        {
            var bpp = 16; //STGenericTexture.GetBytesPerPixel(Format);
            var blkWidth = 4; //STGenericTexture.GetBlockWidth(Format);
            var blkHeight = 4; //STGenericTexture.GetBlockHeight(Format);
            var blockHeight = GetBlockHeight(DivRoundUp(height, blkHeight));

            var mipCount = 1;
            
            var Pitch = 0;
            var DataAlignment = 512;
            var tileMode = 0;
            if (linearTileMode)
                tileMode = 1;

            var linesPerBlockHeight = (1 << blockHeightLog2) * 8;

            var arrayOffset = 0;
            var surfaceSize = 0;
            var blockHeightShift = 0;

            List<int> mipOffsets = new();

            // might be useful if we would want to decode mipmaps (now we don't really care)
            for (var mipLevel = 0; mipLevel < mipCount; mipLevel++)
            {
                var mipWidth = Math.Max(1, width >> mipLevel);
                var mipHeight = Math.Max(1, height >> mipLevel);
                
                var size = DivRoundUp(mipWidth, blkWidth) *
                           DivRoundUp(mipHeight, blkHeight) * bpp;

                if (Pow2RoundUp(DivRoundUp(mipHeight, blkWidth)) <
                    linesPerBlockHeight)
                    blockHeightShift += 1;

                var widthAligned = DivRoundUp(mipWidth, blkWidth);
                var heightAligned = DivRoundUp(mipHeight, blkHeight);

                //Calculate the mip size instead
                byte[] alignedData =
                    new byte[(RoundUp(surfaceSize, DataAlignment) - surfaceSize)];
                surfaceSize += alignedData.Length;
                mipOffsets.Add(surfaceSize);

                //Get the first mip offset and current one and the total image size
                var msize = mipOffsets[0] + sourceData.Length - mipOffsets[mipLevel];

                var data = sourceData.Slice(arrayOffset + mipOffsets[mipLevel], msize);

                Pitch = RoundUp(widthAligned * bpp, 64);
                surfaceSize += Pitch * RoundUp(heightAligned,
                    Math.Max(1, blockHeight >> blockHeightShift) * 8);

                byte[] result = Deswizzle(mipWidth, mipHeight, blkWidth, blkHeight, bpp, tileMode,
                    Math.Max(0, blockHeightLog2 - blockHeightShift), data);
                //Create a copy and use that to remove unneeded data
                Trace.Assert(targetData.Length == size);
                
                result.AsSpan().Slice(0, size).CopyTo(targetData);
                return;

                arrayOffset += sourceData.Length;
            }

            throw new InvalidOperationException();
        }


        /*---------------------------------------
         * 
         * Code ported from AboodXD's BNTX Extractor https://github.com/aboood40091/BNTX-Extractor/blob/master/swizzle.py
         * 
         *---------------------------------------*/

        public static int GetBlockHeight(int height)
        {
            var blockHeight = Pow2RoundUp(height / 8);
            if (blockHeight > 16)
                blockHeight = 16;

            return blockHeight;
        }

        public static int DivRoundUp(int n, int d)
        {
            return (n + d - 1) / d;
        }
        public static int RoundUp(int x, int y)
        {
            return ((x - 1) | (y - 1)) + 1;
        }
        public static int Pow2RoundUp(int x)
        {
            x -= 1;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        private static byte[] SwizzleInternal(int width, int height, int blkWidth, int blkHeight, int bpp, int tileMode, int blockHeightLog2, ReadOnlySpan<byte> data, bool toSwizzle)
        {
            var blockHeight = 1 << blockHeightLog2;

            width = DivRoundUp(width, blkWidth);
            height = DivRoundUp(height, blkHeight);
           
            int pitch;
            int surfSize;
            if (tileMode == 1)
            {
                pitch = width * bpp;

                //if (alignPitch)
                //    pitch = RoundUp(pitch, 32);

                surfSize = pitch * height;
            }
            else
            {
                pitch = RoundUp(width * bpp, 64);
                surfSize = pitch * RoundUp(height, blockHeight * 8);
            }

            byte[] resultBuffer = new byte[surfSize];
            var result = resultBuffer.AsSpan();

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    int tiledPos;
                    int logicalPos;

                    if (tileMode == 1)
                        tiledPos = y * pitch + x * bpp;
                    else
                        tiledPos = GetAddrBlockLinear(x, y, width, bpp, 0, blockHeight);

                    logicalPos = (y * width + x) * bpp;

                    if (tiledPos + bpp <= surfSize)
                    {
                        
                        if (toSwizzle)
                            data.Slice(logicalPos, bpp).CopyTo(result.Slice(tiledPos, bpp));
                        //Array.Copy(data, logicalPos, result, tiledPos, bpp);
                        else
                            data.Slice(tiledPos, bpp).CopyTo(result.Slice(logicalPos, bpp));
                        //Array.Copy(data, tiledPos, result, logicalPos, bpp);
                    }
                }
            }
            return resultBuffer;
        }

        public static byte[] Deswizzle(int width, int height, int blkWidth, int blkHeight, int bpp, int tileMode, int sizeRange, ReadOnlySpan<byte> data)
        {
            return SwizzleInternal(width, height, blkWidth, blkHeight, bpp, tileMode, sizeRange, data, false);
        }

        public static byte[] Swizzle(int width, int height, int blkWidth, int blkHeight, int bpp, int tileMode, int sizeRange, ReadOnlySpan<byte> data)
        {
            return SwizzleInternal(width, height, blkWidth, blkHeight, bpp, tileMode, sizeRange, data, true);
        }

        static int GetAddrBlockLinear(int x, int y, int width, int bytesPerPixel, int baseAddress, int blockHeight)
        {
            /*
              From Tega X1 TRM 
                               */
            var imageWidthInGobs = DivRoundUp(width * bytesPerPixel, 64);


            var gobAddress = (baseAddress
                               + (y / (8 * blockHeight)) * 512 * blockHeight * imageWidthInGobs
                               + (x * bytesPerPixel / 64) * 512 * blockHeight
                               + (y % (8 * blockHeight) / 8) * 512);

            x *= bytesPerPixel;

            var address = (gobAddress + ((x % 64) / 32) * 256 + ((y % 8) / 2) * 64
                           + ((x % 32) / 16) * 32 + (y % 2) * 16 + (x % 16));
            return address;
        }
    }

}