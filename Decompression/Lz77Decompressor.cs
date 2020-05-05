using System;

namespace ShinDataUtil.Decompression
{
    public static class Lz77Decompressor
    {
        public static void Decompress(Span<byte> output, ReadOnlySpan<byte> input, int offsetBits)
        {
            var outOffset = 0;
            
            while (input.Length > 0)
            {
                var map = input[0];
                input = input[1..];
                for (int i = 0; i < 8; i++)
                {
                    if (input.Length == 0)
                        return;

                    if (((map >> i) & 1) == 0)
                    {
                        /* literal value */
                        output[outOffset] = input[0];
                        input = input[1..];
                        outOffset++;
                    }
                    else
                    {
                        /* back seek */
                        var backseekSpec = (ushort)((input[0] << 8)| input[1]); // big endian Oo

                        /*  MSB  XXXXXXXX          YYYYYYYY LSB
                            val  len               backOffset
                            size (16-backseekBits) offsetBits
                         */
                        
                        var len = (backseekSpec >> offsetBits) + 3;
                        var backOffset = (backseekSpec & (1 << offsetBits) - 1) + 1;

                        for (var j = 0; j < len; j++) 
                            output[outOffset + j] = output[outOffset - backOffset + j];

                        outOffset += len;
                        input = input[2..];
                    }
                }
            }
        }

    }
}