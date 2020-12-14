using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using ShinDataUtil.Scenario;
using ShinDataUtil.Scenario.DisassemblyMapVisitors;

namespace ShinDataUtil.Decompression.Scenario
{
    public class DisassemblyMap
    {
        public DisassemblyMap(ImmutableDictionary<string, Section> nameToSection,
            ImmutableDictionary<int, Section> addressToSection, 
            ImmutableArray<((int, int), Section)> knownRegions, ImmutableDictionary<int, Section> regionStarts, ImmutableDictionary<int, Section> regionEnds)
        {
            NameToSection = nameToSection;
            AddressToSection = addressToSection;
            KnownRegions = knownRegions;
            RegionStarts = regionStarts;
            RegionEnds = regionEnds;
        }
        
        public static readonly DisassemblyMap Empty = new DisassemblyMap(
            ImmutableDictionary<string, Section>.Empty,
            ImmutableDictionary<int, Section>.Empty, 
            ImmutableArray<((int, int), Section)>.Empty,
            ImmutableDictionary<int, Section>.Empty,
            ImmutableDictionary<int, Section>.Empty);
        
        public ImmutableDictionary<string, Section> NameToSection { get; }
        public ImmutableDictionary<int, Section> AddressToSection { get; }
        public ImmutableArray<((int, int), Section)> KnownRegions { get; }
        public ImmutableDictionary<int, Section> RegionStarts { get; }
        public ImmutableDictionary<int, Section> RegionEnds { get; }
        

        public Section? GetSectionWithRegionStartingAt(in int addr)
        {
            if (RegionStarts.TryGetValue(addr, out var res))
                return res;
            return null;
        }

        public Section? GetSectionWithRegionEndingAt(in int addr)
        {
            if (RegionEnds.TryGetValue(addr, out var res))
                return res;
            return null;
        }
        
        public class Section
        {
            public Section(string name, ImmutableArray<(int begin, int end)> addressRegions)
            {
                Name = name;
                AddressRegions = addressRegions;
            }

            public string Name { get; set; }
            public ImmutableArray<(int begin, int end)> AddressRegions { get; set; }

            public override string ToString()
            {
                return $"Section \"{Name}\"";
            }
        }

        public class Builder
        {
            public DisassemblyView DisassemblyView { get; }

            private int _nextSectionId;
            private readonly Dictionary<string, int> _sectionToId = new Dictionary<string, int>();
            private readonly Dictionary<int, int> _instrAddressToSection = new Dictionary<int, int>();
            private readonly int? _tipsEndAddress;
            private readonly int? _kakeraEndAddress;
        
            public Builder(DisassemblyView disassemblyView, LabelCollection labels)
            {
                DisassemblyView = disassemblyView;
                _tipsEndAddress = labels.TryGetAddress("TIPS_END");
                _kakeraEndAddress = labels.TryGetAddress("KAKERA_END");
            }

            public int GetSectionId(string sectionName)
            {
                if (_sectionToId.TryGetValue(sectionName, out var id))
                    return id;
                return _sectionToId[sectionName] = _nextSectionId++;
            }
        
            public void MarkInstructionAt(int address, int sectionId)
            {
                _instrAddressToSection.Add(address, sectionId);
            }

            public void MarkInstructionContaining(int address, int sectionId)
            {
                MarkInstructionAt(DisassemblyView.GetInstructionContaining(address).Item1, sectionId);
            }

            public void MarkFunctionAt(int address, int sectionId)
            {
                var visitor = new FunctionVisitor(this, address, sectionId);
                visitor.RunVisits();
            }

            public void MarkFunctionAt(int address, string sectionName) =>
                MarkFunctionAt(address, GetSectionId(sectionName));

            public void MarkTipsEntry(int address, int sectionId)
            {
                var visitor = new KakeraAndTipsEntryVisitor(this, address, sectionId, _tipsEndAddress 
                                                                             ?? throw new InvalidOperationException()
                );
                visitor.RunVisits();
            }

            public void MarkKakeraEntry(int address, int sectionId)
            {
                var visitor = new KakeraAndTipsEntryVisitor(this, address, sectionId, _kakeraEndAddress 
                                                                                      ?? throw new InvalidOperationException()
                );
                visitor.RunVisits();
            }
            
            public void MarkTipsEntry(int address, string sectionName) =>
                MarkTipsEntry(address, GetSectionId(sectionName));
            
            public void MarkKakeraEntry(int address, string sectionName) =>
                MarkKakeraEntry(address, GetSectionId(sectionName));

            public void MarkRegion(int beginAddress, int endAddress, int sectionId)
            {
                while (beginAddress < endAddress)
                {
                    MarkInstructionAt(beginAddress, sectionId);
                    var next = DisassemblyView.TryGetNextInstruction(beginAddress);
                    if (next == null)
                        throw new EndOfStreamException();
                    beginAddress = next.Value.Item1;
                }
            }

            public void MarkFixedNumberOfInstructions(int beginAddress, int instructionCount, int sectionId)
            {
                for (var i = 0; i < instructionCount; i++)
                {
                    MarkInstructionAt(beginAddress, sectionId);
                    var next = DisassemblyView.TryGetNextInstruction(beginAddress);
                    if (next == null)
                        throw new EndOfStreamException();
                    beginAddress = next.Value.Item1;
                }
            }

            public void MarkRegion(int beginAddress, int endAddress, string sectionName) =>
                MarkRegion(beginAddress, endAddress, GetSectionId(sectionName));

            public void MarkFixedNumberOfInstructions(int beginAddress, int instructionCount, string sectionName) =>
                MarkFixedNumberOfInstructions(beginAddress, instructionCount, GetSectionId(sectionName));

            private Section BuildSection(string name, SortedSet<int> addresses)
            {
                var regions = new List<(int, int)>();
                while (addresses.Count > 0)
                {
                    var startAddress = addresses.Min;
                    var currentAddress = startAddress;
                    while (addresses.Remove(currentAddress))
                    {
                        var next = DisassemblyView.TryGetNextInstruction(currentAddress);
                        if (next == null)
                        {
                            currentAddress += DisassemblyView.GetInstructionSize(currentAddress);
                            break;
                        }

                        currentAddress = next.Value.Item1;
                    }
                    regions.Add((startAddress, currentAddress));
                }

                return new Section(name, regions.ToImmutableArray());
            }
            
            public DisassemblyMap Build()
            {
                var addresses = Enumerable.Range(0, _nextSectionId).Select(_ => new SortedSet<int>()).ToArray();
                foreach (var (addr, sectionId) in _instrAddressToSection)
                    addresses[sectionId].Add(addr);
                var names = new string[_nextSectionId];
                foreach (var (name, id) in _sectionToId) 
                    names[id] = name;
                var sections = addresses.Select((x, i) => BuildSection(names[i], x)).ToArray();
                
                var addressToSection = new Dictionary<int, Section>();
                var nameToSection = new Dictionary<string, Section>();
                foreach (var (addr, sectionId) in _instrAddressToSection)
                    addressToSection[addr] = sections[sectionId];
                foreach (var (name, id) in _sectionToId) 
                    nameToSection[name] = sections[id];

                var knownRegions = new List<((int, int), Section)>();
                foreach (var section in sections)
                    foreach (var region in section.AddressRegions)
                        knownRegions.Add((region, section));
                
                knownRegions.Sort();
                
                var regionStarts = new Dictionary<int, Section>();
                var regionEnds = new Dictionary<int, Section>();
                foreach (var ((start, end), section) in knownRegions)
                {
                    regionStarts.Add(start, section);
                    regionEnds.Add(end, section);
                }
                
                return new DisassemblyMap(nameToSection.ToImmutableDictionary(), 
                        addressToSection.ToImmutableDictionary(),
                        knownRegions.ToImmutableArray(),
                        regionStarts.ToImmutableDictionary(),
                        regionEnds.ToImmutableDictionary()
                    );
            }
        }
    }

}