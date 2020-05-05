using System.Collections.Immutable;
using System.IO;
using Newtonsoft.Json;

namespace ShinDataUtil.Scenario
{
    /// <summary>
    /// Represents information found in scenario file besides the code
    /// </summary>
    public class ScenarioHeadInfo
    {
        public ScenarioHeadInfo(ImmutableArray<string> section36, ImmutableArray<(string, ushort)> section40, ImmutableArray<(string, string, ushort)> section44, ImmutableArray<(string, string, ushort)> section48, ImmutableArray<string> section52, ImmutableArray<(string, int)> section56, ImmutableArray<(string, byte[])> section60, ImmutableArray<(string, ushort[])> section64, ImmutableArray<(ushort, ushort, ushort)> section68, ImmutableArray<(ushort, string)> section72, ImmutableArray<(ushort, ushort, ushort, ushort, ushort?, string?)> section76)
        {
            Section36 = section36;
            Section40 = section40;
            Section44 = section44;
            Section48 = section48;
            Section52 = section52;
            Section56 = section56;
            Section60 = section60;
            Section64 = section64;
            Section68 = section68;
            Section72 = section72;
            Section76 = section76;
        }

        public ImmutableArray<string> Section36 { get; }
        public ImmutableArray<(string, ushort)> Section40 { get; }
        public ImmutableArray<(string, string, ushort)> Section44 { get; }
        public ImmutableArray<(string, string, ushort)> Section48 { get; }
        public ImmutableArray<string> Section52 { get; }
        public ImmutableArray<(string, int)> Section56 { get; }
        public ImmutableArray<(string, byte[])> Section60 { get; }
        public ImmutableArray<(string, ushort[])> Section64 { get; }
        public ImmutableArray<(ushort, ushort, ushort)> Section68 { get; }
        public ImmutableArray<(ushort, string)> Section72 { get; }
        public ImmutableArray<(ushort, ushort, ushort, ushort, ushort?, string?)> Section76 { get; }

        public void SerializeTo(TextWriter destination) => 
            JsonSerializer.Create().Serialize(new JsonTextWriter(destination), this);

        public static ScenarioHeadInfo DeserializeFrom(TextReader source) =>
            JsonSerializer.Create().Deserialize<ScenarioHeadInfo>(new JsonTextReader(source));
    }
}