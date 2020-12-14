using System.Collections.Generic;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Decompression.Scenario;

namespace ShinDataUtil.Scenario.DisassemblyMapVisitors
{
    public abstract class MapBuilderVisitor : DisassemblyVisitor
    {
        private readonly int _sectionId;

        protected int SectionId =>  _sectionId;
        protected DisassemblyMap.Builder Builder { get; }

        protected MapBuilderVisitor(DisassemblyMap.Builder builder, int? startPoint, int sectionId) 
            : base(builder.DisassemblyView, startPoint)
        {
            Builder = builder;
            _sectionId = sectionId;
        }

        protected override bool VisitInstruction(int address, Instruction i)
        {
            Builder.MarkInstructionAt(address, _sectionId);
            return true;
        }

        protected abstract override IEnumerable<int> GetOutEdges(int address, Instruction instr, bool shouldContinue);
    }
}