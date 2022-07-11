using System;

namespace ShinDataUtil
{
    public struct TexHeader
    {
        public uint Magic;
        public NVNTexFormat Format;
        public uint Target;
        public uint Width;
        public uint Height;
        public uint Depth;
        public uint Levels;
        public uint DataOffset;
        public uint DataSize;

        public static int HeaderSize => sizeof(uint)*9;
        public static uint DefaultMagic => 0x7865742E;
        public ReadOnlySpan<byte> GetData(ReadOnlySpan<byte> texData) => 
            texData.Slice(checked((int)DataOffset), checked((int)DataSize));
    }
}