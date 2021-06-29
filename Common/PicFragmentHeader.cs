namespace ShinDataUtil
{
    public struct PicFragmentHeader
    {
#pragma warning disable 649
        public ushort compressionFlags;
        public ushort opaqueVertexCount;
        public ushort transparentVertexCount;
        public ushort alignmentAfterHeader;
        public ushort offsetX;
        public ushort offsetY;
        public ushort width;
        public ushort height;
        public ushort compressedSize;
        public ushort unknown_bool;
#pragma warning restore 649

        public bool UseDifferentialEncoding
        {
            // the stored value is inverted!
            get => (compressionFlags >> 1 & 1) == 0;
            set => compressionFlags = (ushort) (value ? compressionFlags & ~(1 << 1) : compressionFlags | (1 << 1));
        }

        public bool UseSeparateAlpha
        {
            // the stored value is inverted!
            get => (compressionFlags >> 0 & 1) == 0;
            set => compressionFlags = (ushort) (value ? compressionFlags & ~(1 << 0) : compressionFlags | (1 << 0));
        }
    }
}