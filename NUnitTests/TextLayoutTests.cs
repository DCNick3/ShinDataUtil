using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Scenario;
using ShinDataUtil.Util;

namespace NUnitTests
{
    public class TextLayoutTests
    {
        public static ImmutableArray<string> OriginalMessages;
        
        static TextLayoutTests()
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

        [Test]
        public void RebuildingTextLayouterEndToEndIsolated()
        {
            foreach (var message in OriginalMessages)
            {
                var tl = new RebuildingTextLayouter();
                new MessageTextParser().ParseTo(message, tl);
                Assert.That(message, Is.EqualTo(tl.Dump()));
            }
        }

        [Test]
        public void RebuildingTextLayouterEndToEndReused()
        {
            var tl = new RebuildingTextLayouter();
            var parser = new MessageTextParser();
            foreach (var message in OriginalMessages)
            {
                parser.ParseTo(message, tl);
                Assert.That(message, Is.EqualTo(tl.Dump()));
            }
        }

        /*[Test]
        public void OriginalLineBreakingNonIntrusive()
        {
            var elh = new MessageEnglishLayoutHelper(SharedData.Instance.FontLayoutInfo);
            var parser = new MessageTextParser();
            foreach (var message in OriginalMessages)
            {
                parser.ParseTo(message, elh);
                Assert.Equal(message, elh.Dump());
            }
        }*/
    }
}