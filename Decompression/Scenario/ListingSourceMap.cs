using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ShinDataUtil.Decompression.Scenario
{
    public sealed class ListingSourceMap
    {
        private ImmutableArray<(int, int)> _addressToLineMapping;
        private ImmutableArray<int> _lineToAddressMapping;
        
        private ListingSourceMap(ImmutableArray<(int, int)> addressToLineMapping, ImmutableArray<int> lineToAddressMapping)
        {
            _addressToLineMapping = addressToLineMapping;
            _lineToAddressMapping = lineToAddressMapping;
        }

        public int GetLine(int fileOffset)
        {
            var index = ~_addressToLineMapping.BinarySearch((fileOffset, -1));
            if (index == _addressToLineMapping.Length)
                return _addressToLineMapping[^1].Item2 + 1;

            if (index > 0 && _addressToLineMapping[index].Item1 > fileOffset)
            {
                index--;
                while (index > 0 && _addressToLineMapping[index - 1].Item1 == _addressToLineMapping[index].Item1)
                    index--; 
            }

            return _addressToLineMapping[index].Item2 + 1;
        }

        public int GetAddress(int line)
        {
            return _lineToAddressMapping[line - 1];
        }

        public class Builder
        {
            private readonly SortedSet<(int, int)> _addressToLineMapping = new SortedSet<(int, int)>();
            private readonly List<int> _lineToAddressMapping = new List<int>();
            private int _line = 0;
            
            public void EmitLine(int startAddress)
            {
                _addressToLineMapping.Add((startAddress, _line));
                _lineToAddressMapping.Add(startAddress);
                _line++;
            }

            public ListingSourceMap Build() => new ListingSourceMap(
                _addressToLineMapping.ToImmutableArray(),
                _lineToAddressMapping.ToImmutableArray());
        }
    }
}