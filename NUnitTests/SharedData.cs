using System.Collections.Immutable;
using System.IO;
using Newtonsoft.Json;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Compression.Scenario;
using ShinDataUtil.Decompression;

namespace NUnitTests
{
    public class SharedData
    {
        static SharedData()
        {
            Instance = JsonConvert.DeserializeObject<SharedData>(
                File.ReadAllText("data_locations.json"));
            
            Instance.ScenarioInstructions = ReadInstructions(Instance.ScenarioDecodedPath);
            Instance.FontLayoutInfo = ShinFontExtractor.GetLayoutInfo(File.ReadAllBytes(Instance.FontPath));
            Instance.GameArchive = new FileReadableGameArchive(Instance.DataRomPath);
        }

        static (ImmutableArray<Instruction> instructions, ImmutableDictionary<string, int> labels) ReadInstructions(string path)
        {
            using var codeFile = File.OpenText(path);

            var asmParser = new Parser(codeFile);
            return asmParser.ReadAll();
        }

        public static readonly SharedData Instance;
        
        public string DataRomPath { get; set; }
        public string DataRawPath { get; set; }
        public string ScenarioDecodedPath { get; set; }
        public string FontPath { get; set; }

        public (ImmutableArray<Instruction> instructions, 
            ImmutableDictionary<string, int> labels) ScenarioInstructions;
        public ReadableGameArchive GameArchive { get; set; }
        
        public ShinFontExtractor.LayoutInfo FontLayoutInfo;
    }
}