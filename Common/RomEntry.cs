namespace ShinDataUtil.Common
{
    /// <summary>
    /// Represents a raw entry (either file of directory) stored in rom file
    /// </summary>
    public struct RomEntry
    {
        // These are raw values stored in file
#pragma warning disable 649
        private uint _e0, _e1, _e2;
#pragma warning restore 649
        public bool IsDirectory
        {
            get => _e0 >> 31 != 0;
            set => _e0 = (uint) ((_e0 & 0x7fffffff) | ((value ? 1 : 0) << 31));
        }

        /* from the beginning of the entry */
        public int NameOffset
        {
            get => checked((int)(_e0 & 0x7fffffff));
            set => _e0 = (uint)((_e0 & 0x80000000) | (value & 0x7fffffff));
        }

        public long RawDataOffset
        {
            /* from the beginning of the archive file */
            get => _e1;
            set => _e1 = checked((uint)value);
        }

        public int DataSize
        {
            get => checked((int) _e2);
            set => _e2 = checked((uint)value);
        }
    }
}