using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Scenario;

namespace ShinDataUtil.Decompression.Scenario
{
    public class ScenarioIdTracer : DisassemblyVisitor
    {
        private readonly List<int> _uses = new List<int>(); 
        
        private ScenarioIdTracer(DisassemblyView view) : base(view, null)
        {
        }

        protected override bool VisitInstruction(int address, Instruction i)
        {
            var reads = i.DataXRefOutRead;
            if (reads.Contains(0)) 
                _uses.Add(address);
            
            var r = !i.DataXRefOutWrite.Contains(0);
            return r;
        }

        public static ImmutableArray<int> GetUses(DisassemblyView view)
        {
            var visitor = new ScenarioIdTracer(view);
            visitor.RunVisits();
            return visitor._uses.OrderBy(_ => _).ToImmutableArray();
        }
    }
}