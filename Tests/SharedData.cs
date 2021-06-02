using System.Collections.Immutable;
using System.IO;
using Newtonsoft.Json;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Compression.Scenario;
using ShinDataUtil.Decompression.Scenario;

namespace UnitTests
{
    public class SharedData
    {
        static SharedData()
        {
            Instance = JsonConvert.DeserializeObject<SharedData>(
                File.ReadAllText("data_locations.json"));
            
            using var codeFile = File.OpenText(Instance.ScenarioDecodedPath + "/listing.asm");

            var asmParser = new Parser(codeFile);
            Instance.ScenarioInstructions = asmParser.ReadAll();
        }

        public static readonly SharedData Instance;
        
        public string DataRomPath { get; set; }
        public string DataRawPath { get; set; }
        public string ScenarioDecodedPath { get; set; }

        public (ImmutableArray<Instruction> instructions, 
            ImmutableDictionary<string, int> labels) ScenarioInstructions;
    }
}