using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ShinDataUtil.Scenario
{
    public class LabelCollection
    {
        private LabelCollection(ImmutableDictionary<int, ImmutableArray<string>> addressToLabels,
            ImmutableDictionary<string, int> labelToAddress)
        {
            AddressToLabels = addressToLabels;
            LabelToAddress = labelToAddress;
        }


        public IEnumerable<string> this[int address] => AddressToLabels[address];
        public int this[string labelName] => LabelToAddress[labelName];

        public ImmutableDictionary<int, ImmutableArray<string>> AddressToLabels { get; }
        public ImmutableDictionary<string, int> LabelToAddress { get; }

        public bool ContainsAddress(int address) => AddressToLabels.ContainsKey(address);

        public int? TryGetAddress(string labelName)
        {
            if (!LabelToAddress.TryGetValue(labelName, out var address))
                return null;
            return address;
        }
        
        public class Builder
        {
            private readonly Dictionary<int, List<string>> _addressToLabels = new Dictionary<int, List<string>>();
            private readonly Dictionary<string, int> _labelToAddress = new Dictionary<string, int>();

            public void Add(int address, string labelName)
            {
                _labelToAddress.Add(labelName, address);
                try
                {
                    if (!_addressToLabels.TryGetValue(address, out var labels))
                        _addressToLabels[address] = labels = new List<string>();
                    labels.Add(labelName);
                }
                catch (Exception)
                {
                    _labelToAddress.Remove(labelName);
                    throw;
                }
            }

            public void AddEntries(IReadOnlyDictionary<int, int> entries,
                IReadOnlyDictionary<int, string> customEntriesNames)
            {
                var revEntries = new Dictionary<int, List<int>>();
                foreach (var entry in entries)
                {
                    if (!revEntries.TryGetValue(entry.Value, out var list))
                    {
                        list = new List<int>();
                        revEntries[entry.Value] = list;
                    }
                    list.Add(entry.Key);
                }
                
                var userLabels = revEntries.Select(x => (x.Key,
                    "ENTRY_" + string.Join("_", x.Value.OrderBy(_ => _).Select(_ =>
                    {
                        if (!customEntriesNames.TryGetValue(_, out var res))
                            return _.ToString();
                        return res;
                    }))));

                foreach (var (addr, name) in userLabels)
                    Add(addr, name);
            }
            
            public LabelCollection Build()
            {
                return new LabelCollection(_addressToLabels.ToImmutableDictionary(_ => _.Key,
                    _ => _.Value.ToImmutableArray()),
                    _labelToAddress.ToImmutableDictionary());
            }
        }

        

        public IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
    
}