namespace ShinDataUtil.Scenario
{
    /// <summary>
    /// Represents a generic section header for scenario file
    /// (some sections use different one)
    /// </summary>
    public struct ScenarioSectionHeader
    {
#pragma warning disable 649
        // ReSharper disable InconsistentNaming
        public uint byteSize;
        public uint elementCount;
    }
}