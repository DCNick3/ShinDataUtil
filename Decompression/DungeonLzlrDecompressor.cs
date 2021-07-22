using System;

namespace ShinDataUtil.Decompression
{
    public class DungeonLzlrDecompressor
    {
        public static void Decompress(Span<byte> output, ReadOnlySpan<byte> input)
        {
            
        }

        public static void DecompressIfNeeded(ref ReadOnlySpan<byte> data)
        {
            if (data[0] == 'L' && data[1] == 'Z' && data[2] == 'L' && data[3] == 'R')
                throw new NotImplementedException();
        }
    }
}