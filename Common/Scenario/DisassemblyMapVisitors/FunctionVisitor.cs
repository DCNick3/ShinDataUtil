using System.Collections.Generic;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Decompression.Scenario;

namespace ShinDataUtil.Scenario.DisassemblyMapVisitors
{
    public class FunctionVisitor : MapBuilderVisitor
    {
        public FunctionVisitor(DisassemblyMap.Builder builder, int? startPoint, int sectionId) 
            : base(builder, startPoint, sectionId)
        {
        }

        protected sealed override IEnumerable<int> GetOutEdges(int address, Instruction instr, bool shouldContinue)
        {
            if (!shouldContinue)
                yield break;
        
            if (instr.IsJump && instr.Opcode != Opcode.call)
            {
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