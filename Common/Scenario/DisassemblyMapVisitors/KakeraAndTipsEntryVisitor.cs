using System.Collections.Generic;
using System.Linq;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Decompression.Scenario;

namespace ShinDataUtil.Scenario.DisassemblyMapVisitors
{
    public class KakeraAndTipsEntryVisitor : MapBuilderVisitor
    {
        private readonly int _epilogueAddress;
        
        public KakeraAndTipsEntryVisitor(DisassemblyMap.Builder builder, int? startPoint, int sectionId,
            int epilogueAddress) : base(builder, startPoint, sectionId)
        {
            _epilogueAddress = epilogueAddress;
        }

        protected override IEnumerable<int> GetOutEdges(int address, Instruction instr, bool shouldContinue)
        {
            if (!shouldContinue)
                yield break;
        
            if (instr.IsJump && instr.Opcode != Opcode.call)
            {
                if (instr.CodeXRefOut.Any(_ => _ == _epilogueAddress))
                    yield break;
                
                foreach (var addr in instr.CodeXRefOut)
                    if (addr != address)
                        yield return addr;
                if (!instr.CanFallThrough)
                    shouldContinue = false;
            }
            if (shouldContinue)
                yield return address;
        }
    }
}