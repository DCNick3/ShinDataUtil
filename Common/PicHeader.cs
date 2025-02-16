namespace ShinDataUtil
{
    public struct PicHeader
    {
#pragma warning disable 649
        public uint magic;
        public uint version;
        public uint fileSize;
        public ushort originX;
        public ushort originY;
        public ushort effectiveWidth;
        public ushort effectiveHeight;
        public uint field20;
        public uint entryCount;
        public uint pictureId;
        /// Scale in units of 1/4096
        public uint scale;
#pragma warning restore 649
    }
}