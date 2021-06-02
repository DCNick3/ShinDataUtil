namespace ShinDataUtil
{
    public struct TxaEntryHeader
    {
#pragma warning disable 649
        // ReSharper disable InconsistentNaming
        public ushort entry_length;
        public ushort virtual_index;
        public ushort width;
        public ushort height;
        public uint data_offset;
        public uint data_compressed_size;
        public uint data_decompressed_size;
#pragma warning restore 649
        // ReSharper restore InconsistentNaming

    }
}