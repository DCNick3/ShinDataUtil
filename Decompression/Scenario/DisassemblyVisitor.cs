using System.Collections.Generic;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Scenario;

namespace ShinDataUtil.Decompression.Scenario
{
    public class DisassemblyVisitor
    {
        private readonly HashSet<int> _visitedInstructions = new HashSet<int>();
        private readonly int _startPoint;
        
        public DisassemblyVisitor(DisassemblyView view, int? startPoint)
        {
            if (startPoint == null)
                startPoint = view.BeginAddress;
            _startPoint = startPoint.Value;
            DisassemblyView = view;
        }
        
        protected DisassemblyView DisassemblyView { get; }

        protected virtual bool VisitInstruction(int address, Instruction i) => true;

        protected virtual IEnumerable<int> GetOutEdges(int address, Instruction instr, bool shouldContinue)
        {
            if (!shouldContinue)
                yield break;
        
            if (instr.IsJump)
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
        
        private void VisitBlock(int address)
        {
            if (_visitedInstructions.Contains(address))
                return;

            var instr = DisassemblyView[address];
            while (true)
            {
                if (_visitedInstructions.Contains(address))
                    break;

                var shouldContinue = VisitInstruction(address, instr);
                _visitedInstructions.Add(address);
                
                var shouldFallThrough = false;
                foreach (var outAddr in GetOutEdges(address, instr, shouldContinue))
                {
                    if (outAddr == address) // A little crutch...
                        shouldFallThrough = true;
                    else
                        VisitBlock(outAddr);
                }
                
                if (!shouldFallThrough)
                    break;
                
                var next = DisassemblyView.TryGetNextInstruction(address);
                if (next == null)
                    break;
                
                (address, instr) = next.Value;
            }

        }
        
        public void RunVisits() => VisitBlock(_startPoint);
    }
}