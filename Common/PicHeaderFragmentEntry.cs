using System;

namespace ShinDataUtil
{
    public struct PicHeaderFragmentEntry
    {
#pragma warning disable 649
        public ushort x;
        public ushort y;
        public uint offset; /* from the beginning of the picture file */
        public uint size;
#pragma warning restore 649
        public ReadOnlySpan<byte> GetData(ReadOnlySpan<byte> file) => file[(int)offset..(int)(offset + size)];
    }
}