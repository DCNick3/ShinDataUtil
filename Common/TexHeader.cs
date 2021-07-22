using System;

namespace ShinDataUtil
{
    public struct TexHeader
    {
        public uint Magic;
        public uint Format;
        public uint Target;
        public uint Width;
        public uint Height;
        public uint Depth;
        public uint Levels;
        public uint DataOffset;
        public uint DataSize;

        public ReadOnlySpan<byte> GetData(ReadOnlySpan<byte> texData) => 
            texData.Slice(checked((int)DataOffset), checked((int)DataSize));
    }
}