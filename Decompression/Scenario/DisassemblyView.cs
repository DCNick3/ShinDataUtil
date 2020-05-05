using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Scenario;

namespace ShinDataUtil.Decompression.Scenario
{
    public class DisassemblyViewBuilder
    {
        private readonly int _offset;
        private readonly List<Instruction> _instructions;
        private readonly (int?, short)[] _map;
        
        public DisassemblyViewBuilder(int initialOffset, int size)
        {
            _instructions = new List<Instruction>();
            _offset = initialOffset;
            _map = new (int?, short)[size];
        }

        public bool HasVisited(int address) => _map[address - _offset].Item1.HasValue;

        public void Visit(int address, int size, Opcode opcode, ImmutableArray<dynamic> data)
        {
            if (HasVisited(address))
                throw new InvalidOperationException();
            var id = _instructions.Count;
            _instructions.Add(new Instruction(opcode, data));
            for (var i = 0; i < size; i++) 
                _map[address + i - _offset] = (id, checked((short) -i));
        }

        public DisassemblyView Build()
        {
            return new DisassemblyView(_offset, _instructions.ToImmutableArray(), _map.ToImmutableArray());
        }
    }

    public class DisassemblyView
    {
        internal DisassemblyView(int offset, ImmutableArray<Instruction> instructions, ImmutableArray<(int?, short)> map)
        {
            _offset = offset;
            _instructions = instructions;
            _map = map;
        }
        
        private int _offset;
        private ImmutableArray<Instruction> _instructions;
        private ImmutableArray<(int?, short)> _map;

        public int BeginAddress => _offset;
        public int EndAddress => _offset + _map.Length;
        public int Size => _map.Length;

        public Instruction this[int address] => GetInstructionContaining(address).Item2;

        public Instruction GetInstructionAt(int address)
        {
            var r = TryGetInstructionAt(address);
            if (r == null) throw new IndexOutOfRangeException();
            return r.Value;
        }

        public (int, Instruction) GetInstructionContaining(int address)
        {
            var r = TryGetInstructionContaining(address);
            if (r == null) throw new IndexOutOfRangeException();
            return r.Value;
        }

        public (int, Instruction) GetNextInstruction(int address)
        {
            var r = TryGetNextInstruction(address);
            if (r == null) throw new IndexOutOfRangeException();
            return r.Value;
        }

        public (int, Instruction)? TryGetNextInstruction(int address)
        {
            if (address < BeginAddress)
                return (BeginAddress, GetInstructionAt(BeginAddress));
            var instr = TryGetInstructionContaining(address);
            if (instr == null) return null;
            var initialAddr = instr.Value.Item1;
            var currentAddr = initialAddr;
            while (true)
            {
                var (startAddr, currentInstr) = GetInstructionContaining(currentAddr++);
                if (startAddr != initialAddr)
                    return (startAddr, currentInstr);
            }
        }

        public int? TryGetNextInstructionAddress(int address) => TryGetNextInstruction(address)?.Item1;
        
        public (int, Instruction)? TryGetInstructionContaining(in int address)
        {
            if (address < BeginAddress || address >= EndAddress)
                return null;
            var (id, offset) = _map[address - _offset];
            if (id == null) return null;
            return (address + offset, _instructions[id.Value]);
        }

        public Instruction? TryGetInstructionAt(int address)
        {
            var t = TryGetInstructionContaining(address);
            if (t == null) return null;
            if (t.Value.Item1 != address) return null;
            return t.Value.Item2;
        }

        public ImmutableDictionary<Opcode, int> GetInstructionStats()
        {
            var dict = new Dictionary<Opcode, int>();
            foreach (var instruction in _instructions)
            {
                dict.TryGetValue(instruction.Opcode, out var count);
                dict[instruction.Opcode] = count + 1;
            }
            return dict.ToImmutableDictionary();
        }

        public IEnumerable<int> EnumerateInstructionAddresses()
        {
            for (var i = 0; i < Size; i++)
                if (_map[i].Item1 != null && _map[i].Item2 == 0)
                    yield return i + _offset;
        }

        public IEnumerable<(int, Instruction)> EnumerateInstructions()
        {
            // ReSharper disable once PossibleInvalidOperationException
            return EnumerateInstructionAddresses().Select(i => (i, GetInstructionAt(i)));
        }
    }
}