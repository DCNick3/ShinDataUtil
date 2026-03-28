using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using ShinDataUtil.Common;
using System.Runtime.InteropServices;

namespace ShinDataUtil.Compression
{
    public class ShinLZLRCompressor
    {
        private MemoryStream dictStream;
        private MemoryStream dataStream;
        private uint currentDictBlock;
        private int bitsInBlock;

        private void WriteBit(bool bit)
        {
            if (bit)
            {
                currentDictBlock |= 1u << (31 - bitsInBlock);
            }
            bitsInBlock++;
            if (bitsInBlock == 32)
            {
                FlushBlock();
            }
        }

        private void FlushBlock()
        {
            dictStream.WriteByte((byte)currentDictBlock);
            dictStream.WriteByte((byte)(currentDictBlock >> 8));
            dictStream.WriteByte((byte)(currentDictBlock >> 16));
            dictStream.WriteByte((byte)(currentDictBlock >> 24));
            currentDictBlock = 0;
            bitsInBlock = 0;
        }

        private void FinishBits()
        {
            if (bitsInBlock > 0)
            {
                FlushBlock();
            }
        }

        private void WriteNumber(int offset, uint num)
        {
            uint bitmaskL = 1u << (offset - 1);
            while (bitmaskL > 0)
            {
                WriteBit((num & bitmaskL) != 0);
                bitmaskL >>= 1;
            }
        }

        private void WriteSequenceCount(uint val)
        {
            int bitcount = 0;
            while (val >= ((1u << (bitcount + 1)) - 1))
            {
                if (bitcount == 23 || val < ((1u << (bitcount + 2)) - 1))
                {
                    break;
                }
                bitcount++;
            }

            for (int i = 0; i < bitcount; i++)
            {
                WriteBit(true);
            }
            WriteBit(false);

            uint offsetTableVal = (1u << (bitcount + 1)) - 1;
            WriteNumber(bitcount + 1, val - offsetTableVal);
        }

        private void FindBestMatch(ReadOnlySpan<uint> inputWords, int pos, out uint bestBackOffset, out int bestMatchLen)
        {
            bestBackOffset = 0;
            bestMatchLen = 0;
            int windowStart = Math.Max(0, pos - 1023);

            for (int candidatePos = pos - 1; candidatePos >= windowStart; candidatePos--)
            {
                int matchLen = 0;
                while (pos + matchLen < inputWords.Length && inputWords[candidatePos + matchLen] == inputWords[pos + matchLen])
                {
                    matchLen++;
                }

                if (matchLen > bestMatchLen)
                {
                    bestMatchLen = matchLen;
                    bestBackOffset = (uint)(pos - candidatePos);
                }
            }
        }

        private int EncodeMatches(ReadOnlySpan<uint> inputWords, int pos, uint bestBackOffset, int bestMatchLen)
        {
            var matches = new List<Tuple<uint, uint>>();
            matches.Add(new Tuple<uint, uint>((uint)bestMatchLen, bestBackOffset));
            pos += bestMatchLen;

            while (pos < inputWords.Length)
            {
                FindBestMatch(inputWords, pos, out uint nextBackOffset, out int nextMatchLen);
                
                if (nextMatchLen >= 1)
                {
                    matches.Add(new Tuple<uint, uint>((uint)nextMatchLen, nextBackOffset));
                    pos += nextMatchLen;
                }
                else
                {
                    break;
                }
            }

            WriteBit(true);
            WriteSequenceCount((uint)matches.Count);
            foreach (var match in matches)
            {
                WriteSequenceCount(match.Item1);
                WriteNumber(10, match.Item2);
            }
            return pos;
        }

        private int EncodeLiterals(ReadOnlySpan<uint> inputWords, int pos)
        {
            var literals = new List<uint>();
            literals.Add(inputWords[pos]);
            pos++;

            while (pos < inputWords.Length)
            {
                FindBestMatch(inputWords, pos, out _, out int matchLen);
                if (matchLen >= 1)
                {
                    break;
                }
                literals.Add(inputWords[pos]);
                pos++;
            }

            WriteBit(false);
            WriteSequenceCount((uint)literals.Count);
            foreach (uint l in literals)
            {
                dataStream.WriteByte((byte)l);
                dataStream.WriteByte((byte)(l >> 8));
                dataStream.WriteByte((byte)(l >> 16));
                dataStream.WriteByte((byte)(l >> 24));
            }
            return pos;
        }

        public ReadOnlySpan<byte> Compress(ReadOnlySpan<byte> data)
        {
            dictStream = new MemoryStream();
            dataStream = new MemoryStream();
            currentDictBlock = 0;
            bitsInBlock = 0;

            int paddedLength = (data.Length + 3) / 4 * 4;
            var paddedData = new byte[paddedLength];
            data.CopyTo(paddedData);

            ReadOnlySpan<uint> inputWords = MemoryMarshal.Cast<byte, uint>(paddedData);

            int pos = 0;
            while (pos < inputWords.Length)
            {
                FindBestMatch(inputWords, pos, out uint bestBackOffset, out int bestMatchLen);

                if (bestMatchLen >= 1)
                {
                    pos = EncodeMatches(inputWords, pos, bestBackOffset, bestMatchLen);
                }
                else
                {
                    pos = EncodeLiterals(inputWords, pos);
                }
            }

            FinishBits();

            var header = new LZLRHeader();
            header.magic = LZLRHeader.DefaultMagic;
            header.unpackedSize = data.Length;
            header.dataOffset = LZLRHeader.Size + (int)dictStream.Position;

            var outStream = new MemoryStream();
            outStream.Write(SpanUtil.AsReadOnlyBytes(ref header));
            outStream.Write(dictStream.ToArray());
            outStream.Write(dataStream.ToArray());

            return outStream.ToArray();
        }
    }
}
