namespace ShinDataUtil.Scenario
{
    /// <summary>
    /// Represents a header of a scenario file
    /// </summary>
    public struct ScenarioHeader
    {
        // ReSharper disable InconsistentNaming
        public uint magic;
        public uint size;
        public uint unk1;
        public uint unk2;
        public uint unk3;
        public uint unk4;
        public uint unk5;
        public uint unk6;
        public uint commands_offset;
        public uint offset_36;
        public uint offset_40;
        public uint offset_44;
        public uint offset_48;
        public uint offset_52;
        public uint offset_56;
        public uint offset_60;
        public uint offset_64;
        public uint offset_68;
        public uint offset_72;
        public uint offset_76;
        // ReSharper restore InconsistentNaming
    }
}