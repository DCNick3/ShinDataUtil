using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ShinDataUtil.Scenario;
using ShinDataUtil.Util;
using Xunit;

namespace UnitTests
{
    public class TextLayout
    {
        public static ImmutableArray<string> Data;
        
        static TextLayout()
        {
            Data = (
                from instr in SharedData.Instance.ScenarioInstructions.instructions 
                where instr.Opcode == Opcode.MSGSET 
                select instr.Data[3] 
                into message 
                select (string) message).ToImmutableArray();
        }

        [Fact]
        public void RebuildingTextLayouterEndToEndIsolated()
        {
            foreach (var message in Data)
            {
                var tl = new RebuildingTextLayouter();
                new MessageTextParser().ParseTo(message, tl);
                Assert.Equal(message, tl.Dump());
            }
        }

        [Fact]
        public void RebuildingTextLayouterEndToEndReused()
        {
            var tl = new RebuildingTextLayouter();
            var parser = new MessageTextParser();
            foreach (var message in Data)
            {
                parser.ParseTo(message, tl);
                Assert.Equal(message, tl.Dump());
            }
        }
    }
}