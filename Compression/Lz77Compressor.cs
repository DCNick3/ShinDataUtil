using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
        private readonly HashTable _hashTable = new HashTable();
        private readonly byte[] _buffer = new byte[16];
        private readonly int _maximumBackOffset, _maximumLength, _offsetBits;

        public Lz77Compressor(int offsetBits)
        {
            _offsetBits = offsetBits;
            _maximumBackOffset = 1 << offsetBits;
            _maximumLength = (1 << 16 - offsetBits) + 2;
        }
        
        public unsafe (byte[], int actualength) Compress(ReadOnlySpan<byte> data)
        {
            /* corner cases */
            if (data.Length == 0)
                return (new byte[0], 0);

            if (data.Length <= 3)
                return (new[] {(byte) 0x7}.Concat(data.ToArray()).ToArray(), data.Length + 1);

            _hashTable.Clear(); // Or can we assume that it's clean?
            var ms = new MemoryStream();

            var maskLength = 3;
            var maskValue = 0; /* 3 literal values */
            var bufferSize = 0;
            
            _buffer[bufferSize++] = data[0];
            _buffer[bufferSize++] = data[1];
            _buffer[bufferSize++] = data[2];
            
            _hashTable.Add(data, 0);

            int di;
            for (var i = 3; i < data.Length; i += di)
            {
                di = 1;
                
                var r = MaximumPrefix(data, i);
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
                {
                    if (j - _maximumBackOffset >= 0)
                        _hashTable.Remove(data[(j - _maximumBackOffset)..], j - _maximumBackOffset);
                    _hashTable.Add(data[(j - 2)..], j - 2);
                }

                if (maskLength == 8)
                {
                    maskLength = 0;
                    ms.WriteByte((byte)maskValue);
                    maskValue = 0;
                    
                    ms.Write(_buffer[..bufferSize]);
                    bufferSize = 0;
                }
            }
            ms.WriteByte((byte)maskValue);
            ms.Write(_buffer[..bufferSize]);

            return (ms.GetBuffer(), (int)ms.Length);
        }

        private (byte, byte) EncodePair(int offset, int length)
        {
            // Precondition: offset and length are in range
            var u1 = (offset - 1) | ((length - 3) << _offsetBits);
            return ((byte)((u1 >> 8) & 0xff), (byte)(u1 & 0xff)); /* make if big endian */
        }
        
        private (int, int)? MaximumPrefix(ReadOnlySpan<byte> data, int offset)
        {
            if (data.Length - offset < MinimalLength)
                return null;
            
            var bucket = _hashTable.Find(data[offset..]);
            if (bucket.Count == 0)
                return null;
            
            int maxLengthFound = 0, maxLengthFoundOffset = -1;

            //var node = bucket.First;
            
            //foreach (var currentCandidate in bucket)
            //while (node != null)
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < bucket.Count; i++)
            {
                var currentCandidate = bucket[i]; //node.Value;
                //node = node.Next;
                
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
            }

            if (maxLengthFoundOffset == -1)
                return null;
            return (maxLengthFoundOffset, maxLengthFound);
        }

        private class HashTable
        {
            private readonly List<int>[] _table; /* stores offset (global) */

            public HashTable()
            {
                _table = new List<int>[HashTableSize];
                for (var i = 0; i < HashTableSize; i++)
                    _table[i] = new List<int>();
            }
            
            public List<int> Find(ReadOnlySpan<byte> data) => _table[Hash(data)];

            public void Add(ReadOnlySpan<byte> data, int offset)
            {
                var hash = Hash(data);
                var t = _table[hash];
                //if (t is List<int> && t.Count >= ConvertToSet) 
                //    t = _table[hash] = new HashSet<int>(t);
                t.Add(offset);
            }

            public void Remove(ReadOnlySpan<byte> data, int offset) => _table[Hash(data)].Remove(offset);

            public void Clear()
            {
                for (var i = 0; i < HashTableSize; i++)
                    _table[i].Clear();
            }
            
            private static int Hash(ReadOnlySpan<byte> data)
            {
                return (data[0] | (data[1] << 8) | (data[2] << 16)) % HashTableSize;
            }
        }
    }
}