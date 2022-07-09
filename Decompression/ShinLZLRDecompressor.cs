using System;
using System.Runtime.InteropServices;
using ShinDataUtil.Common;
using System.IO;
using System.Diagnostics;

namespace ShinDataUtil.Decompression
{
    public class ShinLZLRDecompressor
    {
        private static readonly uint[] offsetTable = new uint[]
        {
            0x00000001,
            0x00000003,
            0x00000007,
            0x0000000F,
            0x0000001F,
            0x0000003F,
            0x0000007F,
            0x000000FF,
            0x000001FF,
            0x000003FF,
            0x000007FF,
            0x00000FFF,
            0x00001FFF,
            0x00003FFF,
            0x00007FFF,
            0x0000FFFF,
            0x0001FFFF,
            0x0003FFFF,
            0x0007FFFF,
            0x000FFFFF,
            0x001FFFFF,
            0x003FFFFF,
            0x007FFFFF,
            0x00FFFFFF
        };
        private const int lOffset = 10;

        private MemoryStream dictStream = new MemoryStream();
        private uint currentDictBlock = 0;
        private uint bitmask = 0x80000000;
        private void NextDictBlock()
        {
            bitmask = 0x80000000;
            currentDictBlock =  (uint)dictStream.ReadByte();
            currentDictBlock |= (uint)dictStream.ReadByte() << 8;
            currentDictBlock |= (uint)dictStream.ReadByte() << 16;
            currentDictBlock |= (uint)dictStream.ReadByte() << 24;
        }
        private bool GetNextBit()
        {
            bool bit = (currentDictBlock & bitmask) > 0;
            bitmask >>= 1;
            if (bitmask == 0)
            {
                NextDictBlock();
            }
            return bit;
        }
        private uint GetNextSequenceCount()
        {
            int bitcount = 0;
            
            while (GetNextBit())
            {
                bitcount++;
            }

            uint num = GetNextNumber(bitcount + 1);

            return offsetTable[bitcount] + num;
        }
        private uint GetNextNumber(int offset)
        {
            uint bitmaskL = 1u << (offset - 1);
            uint num = 0;

            do {
                if (GetNextBit())
                {
                    num |= bitmaskL;
                }
                bitmaskL >>= 1;
            } while (bitmaskL > 0);

            return num;
        }
        public ReadOnlySpan<byte> Decompress(ReadOnlySpan<byte> data)
        {
            var header = MemoryMarshal.Read<LZLRHeader>(data);

            Trace.Assert(header.magic == LZLRHeader.DefaultMagic);

            var outData = new uint[header.unpackedSize/4];

            dictStream = new MemoryStream(data[LZLRHeader.Size..header.dataOffset].ToArray());
            NextDictBlock();

            var dataStream = new MemoryStream(data[header.dataOffset..].ToArray());

            uint currentPos = 0;

            while (currentPos*sizeof(uint) < header.unpackedSize)
            {
                bool bit = GetNextBit();
                
                // Look back
                if (bit)
                {
                    uint sequenceCount = GetNextSequenceCount();
                    for (; sequenceCount > 0; sequenceCount--)
                    {
                        uint blocksCount = GetNextSequenceCount();
                        uint backOffset = GetNextNumber(lOffset);

                        uint start = currentPos - backOffset;
                        uint end = start + blocksCount;
                        for (uint j = start; j < end; j++)
                        {
                            outData[currentPos++] = outData[j];
                        }
                    }
                }
                // Read data
                else
                {
                    uint blockCount = GetNextSequenceCount();

                    for (uint i = blockCount; i > 0; i--)
                    {
                        uint block  = (uint)dataStream.ReadByte();
                        block      |= (uint)dataStream.ReadByte() << 8;
                        block      |= (uint)dataStream.ReadByte() << 16;
                        block      |= (uint)dataStream.ReadByte() << 24;

                        outData[currentPos++] = block;
                    }
                }
            }

            return MemoryMarshal.Cast<uint, byte>(new ReadOnlySpan<uint>(outData));
        }

        public static bool CheckHeader(ref ReadOnlySpan<byte> data)
        {
            return data[0] == 'L' && data[1] == 'Z' && data[2] == 'L' && data[3] == 'R';
        }
    }
}