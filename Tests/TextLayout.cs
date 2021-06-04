using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Scenario;
using ShinDataUtil.Util;
using Xunit;

namespace UnitTests
{
    public class TextLayout
    {
        public static ImmutableArray<string> OriginalMessages;
        
        static TextLayout()
        {
            var origMsg = GetMessages(SharedData.Instance.ScenarioInstructions.instructions)
                .ToImmutableHashSet();

            OriginalMessages = origMsg.ToImmutableArray();
        }

        static IEnumerable<string> GetMessages(ImmutableArray<Instruction> instructions)
        {
            return 
                from instr in instructions
                where instr.Opcode == Opcode.MSGSET
                select instr.Data[3]
                into message
                select (string) message;
        }

        [Fact]
        public void RebuildingTextLayouterEndToEndIsolated()
        {
            foreach (var message in OriginalMessages)
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
            foreach (var message in OriginalMessages)
            {
                parser.ParseTo(message, tl);
                Assert.Equal(message, tl.Dump());
            }
        }

        [Fact]
        public void OriginalLineBreakingNonIntrusive()
        {
            var elh = new MessageEnglishLayoutHelper(SharedData.Instance.FontLayoutInfo);
            var parser = new MessageTextParser();
            foreach (var message in OriginalMessages)
            {
                parser.ParseTo(message, elh);
                Assert.Equal(message, elh.Dump());
            }
        }
    }
}