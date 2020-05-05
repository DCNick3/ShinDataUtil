using System;
using System.Buffers;
using System.IO;

namespace ShinDataUtil.Decompression
{
    public abstract class ShinFile : IDisposable
    {

        public abstract long Size { get; }

        public abstract ReadOnlyMemory<byte> Data { get; }


        public abstract void Dispose();
    }

    public sealed class MappedShinFile : ShinFile
    {
        private readonly ReadOnlyMemory<byte> _data;
        private readonly MemoryManager<byte> _mm;
        
        internal MappedShinFile(MemoryManager<byte> mm)
        {
            _data = mm.Memory;
            _mm = mm;
        }

        public override long Size => _data.Length;
        public override ReadOnlyMemory<byte> Data => _data;

        public override void Dispose()
        {
            ((IDisposable) _mm)?.Dispose();
        }
    }
    
    public sealed class CachedShinFile : ShinFile
    {
        private readonly byte[] _data;

        public CachedShinFile(Stream fs, long offset, int size)
        {
            _data = new byte[size];
            fs.Seek(offset, SeekOrigin.Begin);
            fs.Read(_data, 0, size);
        }

        public override long Size => _data.LongLength;
        public override ReadOnlyMemory<byte> Data => _data;
        public override void Dispose()
        {}
    }
}