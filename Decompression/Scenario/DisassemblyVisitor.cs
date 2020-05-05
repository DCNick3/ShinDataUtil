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
                
                if (!shouldContinue)
                    break;

                if (instr.IsJump)
                {
                    foreach (var addr in instr.CodeXRefOut)
                        VisitBlock(addr);
                    if (!instr.CanFallThrough)
                        break;
                }

                var next = DisassemblyView.TryGetNextInstruction(address);
                if (next == null)
                    break;
                (address, instr) = next.Value;
            }

        }
        
        public void RunVisits() => VisitBlock(_startPoint);
    }
}