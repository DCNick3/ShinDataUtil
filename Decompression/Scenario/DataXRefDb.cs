using System.Collections.Generic;
using System.Collections.Immutable;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Scenario;

namespace ShinDataUtil.Decompression.Scenario
{
    public class DataXRefDb
    {
        private readonly ImmutableDictionary<int, ImmutableArray<DataXRef>> _xRefOut;
        private readonly ImmutableDictionary<int, ImmutableArray<DataXRef>> _xRefIn;

        private DataXRefDb(ImmutableDictionary<int, ImmutableArray<DataXRef>> xRefIn,
            ImmutableDictionary<int, ImmutableArray<DataXRef>> xRefOut)
        {
            _xRefIn = xRefIn;
            _xRefOut = xRefOut;
        }

        public ImmutableArray<DataXRef> GetXRefFrom(int address) => _xRefOut[address];
        public ImmutableArray<DataXRef> GetXRefTo(int address) => _xRefIn[address];

        public static DataXRefDb FromDisassembly(DisassemblyView disassembly)
        {
            var builder = new Builder();
            foreach (var (addr, instruction) in disassembly.EnumerateInstructions())
            foreach (var xRef in instruction.DataXRefOut)
                builder.Add(addr, xRef.Address, xRef.Type);
            return builder.Build();
        }
        
        public class Builder
        {
            private readonly Dictionary<int, List<DataXRef>> _xRefOut = new Dictionary<int, List<DataXRef>>();
            private readonly Dictionary<int, List<DataXRef>> _xRefIn = new Dictionary<int, List<DataXRef>>();

            public void Add(int from, int to, DataXRefType type)
            {
                if (!_xRefOut.TryGetValue(from, out var list))
                {
                    list = new List<DataXRef>();
                    _xRefOut[from] = list;
                }
                list.Add(new DataXRef(type, to));
                
                if (!_xRefIn.TryGetValue(to, out list))
                {
                    list = new List<DataXRef>();
                    _xRefIn[to] = list;
                }
                list.Add(new DataXRef(type, from));
            }

            public DataXRefDb Build() => new DataXRefDb(
                _xRefIn.ToImmutableDictionary(
                    x => x.Key,
                    x => x.Value.ToImmutableArray()), 
                _xRefOut.ToImmutableDictionary(
                    x => x.Key,
                    x => x.Value.ToImmutableArray()));
        }
    }
}