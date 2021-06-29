namespace ShinDataUtil
{
    public struct PicVertexEntry
    {
#pragma warning disable 649
        public ushort fromX;
        public ushort fromY;
        public ushort toX;
        public ushort toY;
#pragma warning restore 649

        public bool Contains(int i, int j)
        {
            return i >= fromX && i < toX && j >= fromY && j < toY;
        }
    }
}