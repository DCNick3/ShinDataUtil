namespace ShinDataUtil
{
    public struct RomHeader
    {
        public const uint Magic = 0x324d4f52U;
        public const uint Version = 1;
        
#pragma warning disable 649
        public uint magic;
        public uint version;
        public uint indexLength;
        public uint offsetMultiplier;
#pragma warning disable 169
        private long _whatever1, _whatever2;
#pragma warning restore 169
#pragma warning restore 649
    }
}