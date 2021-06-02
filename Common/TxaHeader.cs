namespace ShinDataUtil
{
    public struct TxaHeader
    {
#pragma warning disable 649
        // ReSharper disable InconsistentNaming
        public uint magic;
        public uint version;
        public uint file_size;
        public uint use_dict;
        public uint count;
        public uint max_decompressed_size;
        public uint index_size;
        public uint always_zero;
#pragma warning restore 649
        // ReSharper restore InconsistentNaming
    }
}