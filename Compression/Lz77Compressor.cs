using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ShinDataUtil.Compression
{
    /// <summary>
    /// A compressor for a flavour of Lz77 used inside the game
    /// </summary>
    public class Lz77Compressor
    {
        private const int HashTableSize = 32771; // Close to the one used in gzip, but prime
        private const int MinimalLength = 3;
        //private const int ConvertToSet = 20;
        private readonly MaximumPrefixFinder _maximumPrefixFinder;
        private readonly byte[] _buffer = new byte[16];
        private readonly int _maximumBackOffset, _maximumLength, _offsetBits;

        public Lz77Compressor(int offsetBits)
        {
            _offsetBits = offsetBits;
            _maximumBackOffset = 1 << offsetBits;
            _maximumLength = (1 << 16 - offsetBits) + 2;
            _maximumPrefixFinder = new MaximumPrefixFinder(_maximumBackOffset, _maximumLength);
        }
        
        public unsafe (byte[], int actualength) Compress(ReadOnlySpan<byte> data)
        {
            /* corner cases */
            if (data.Length == 0)
                return (new byte[0], 0);

            if (data.Length <= 3)
                return (new[] {(byte) 0x7}.Concat(data.ToArray()).ToArray(), data.Length + 1);

            _maximumPrefixFinder.Clear(); // Or can we assume that it's clean?
            var ms = new MemoryStream();

            var maskLength = 3;
            var maskValue = 0; /* 3 literal values */
            var bufferSize = 0;
            
            _buffer[bufferSize++] = data[0];
            _buffer[bufferSize++] = data[1];
            _buffer[bufferSize++] = data[2];
            
            _maximumPrefixFinder.Add(data, 0);

            int di;
            for (var i = 3; i < data.Length; i += di)
            {
                di = 1;
                
                var r = _maximumPrefixFinder.MaximumPrefix(data, i);
                if (r == null)
                {
                    _buffer[bufferSize++] = data[i];
                    maskLength++;
                }
                else
                {
                    var (offset, length) = r.Value;
                    Debug.Assert(i - offset <= _maximumBackOffset && length <= _maximumLength);
                    (_buffer[bufferSize++], _buffer[bufferSize++]) = EncodePair(i - offset, length);
                    maskValue |= 1 << maskLength++;
                    di = length;
                }

                for (var j = i; j < i + di; j++) 
                    _maximumPrefixFinder.Add(data[(j - 2)..], j - 2);

                if (maskLength == 8)
                {
                    maskLength = 0;
                    ms.WriteByte((byte)maskValue);
                    maskValue = 0;
                    
                    ms.Write(_buffer.AsSpan()[..bufferSize]);
                    bufferSize = 0;
                }
            }
            ms.WriteByte((byte)maskValue);
            ms.Write(_buffer.AsSpan()[..bufferSize]);

            return (ms.GetBuffer(), (int)ms.Length);
        }

        private (byte, byte) EncodePair(int offset, int length)
        {
            // Precondition: offset and length are in range
            var u1 = (offset - 1) | ((length - 3) << _offsetBits);
            return ((byte)((u1 >> 8) & 0xff), (byte)(u1 & 0xff)); /* make if big endian */
        }

        private class MaximumPrefixFinder
        {
            private readonly int _maximumBackOffset;
            private readonly int _backOffsetsSize;
            private readonly int _maximumLength;
            private readonly int[] _table; /* stores offset (global) */
            private readonly int[] _backOffsets; /* stores back offsets, forming a linked list automagically */

            public MaximumPrefixFinder(int maximumBackOffset, int maximumLength)
            {
                _maximumBackOffset = maximumBackOffset;
                _backOffsetsSize = maximumBackOffset + 1;
                _maximumLength = maximumLength;
                _table = new int[HashTableSize];
                _backOffsets = new int[_backOffsetsSize];
            }
        
            public (int, int)? MaximumPrefix(ReadOnlySpan<byte> data, int offset)
            {
                if (data.Length - offset < MinimalLength)
                    return null;
            
                var currentCandidate = _table[Hash(data[offset..])];
                if (currentCandidate == 0 || offset - currentCandidate > _maximumBackOffset)
                    return null;
            
                int maxLengthFound = 0, maxLengthFoundOffset = -1;

                // ReSharper disable once ForCanBeConvertedToForeach
                while (currentCandidate != 0 && offset - currentCandidate <= _maximumBackOffset)
                {
                    if (offset - currentCandidate > _maximumBackOffset) 
                        continue;
                
                    for (var j = 0; maxLengthFound < _maximumLength; j++)
                    {
                        if (offset + j >= data.Length || j >= _maximumLength || data[currentCandidate + j] != data[offset + j])
                        {
                            if (maxLengthFound < j && j >= MinimalLength)
                            {
                                maxLengthFound = j;
                                maxLengthFoundOffset = currentCandidate;
                            }
                            break;
                        }
                    }

                    currentCandidate = _backOffsets[currentCandidate % _backOffsetsSize];
                }

                if (maxLengthFoundOffset == -1)
                    return null;
                return (maxLengthFoundOffset, maxLengthFound);
            }

            public void Add(ReadOnlySpan<byte> data, int offset)
            {
                var hash = Hash(data);
                var t = _table[hash];
                _backOffsets[offset % _backOffsetsSize] = t;
                _table[hash] = offset;
            }

            public void Clear()
            {
                for (var i = 0; i < HashTableSize; i++)
                    _table[i] = 0;
                for (var i = 0; i < _maximumBackOffset; i++)
                    _backOffsets[i] = 0;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int Hash(ReadOnlySpan<byte> data)
            {
                return (data[0] | (data[1] << 8) | (data[2] << 16)) % HashTableSize;
            }
        }
    }
}