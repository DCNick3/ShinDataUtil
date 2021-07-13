using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Scenario;

namespace ShinDataUtil.Decompression.Scenario
{
    public class Disassembler
    {
        private static readonly Common.ShiftJisEncoding ShiftJis = new Common.ShiftJisEncoding();
        
        private readonly ReadOnlyMemory<byte> _memory;
        private int _initialOffset;
        private int _offset;
        private DisassemblyViewBuilder _disassemblyViewBuilder;
        private readonly List<int> _interestingOffsets = new List<int>();

        private Disassembler(ReadOnlyMemory<byte> memory, int initialOffset)
        {
            _memory = memory;
            _initialOffset = initialOffset;
            _disassemblyViewBuilder = new DisassemblyViewBuilder(initialOffset, memory.Length - initialOffset);
        }
        private void Advance(int count) => _offset += count;

        private byte FeedByte()
        {
            var r= _memory.Span[_offset];
            Advance(1);
            return r;
        }
        
        private ushort FeedShort()
        {
            var r = MemoryMarshal.Read<ushort>(_memory.Span[_offset..]);
            Advance(2);
            return r;
        }

        private uint FeedInt()
        {
            var r = MemoryMarshal.Read<uint>(_memory.Span[_offset..]);
            Advance(4);
            return r;
        }

        private NumberSpec FeedNumber()
        {
            var t = FeedByte();
            if ((t & 0x80) == 0)
            {
                // does the sign extension of t, using bits [0:6] (7 bit number)
                var num = (t & 0x7f) << 25 >> 25;
                //var num = (int)(sbyte)t;                   
                //if ((t & 0x40) != 0) {
                    /* Bit magic! (Probably does zero extension stuff) */
                    //num = (int)(num << 24 | 0x80000000U) >> 24;
                //}
                return NumberSpec.FromConstant(num);
            }

            int result;
            // does the sign extension of t, using bits [0:3] (4 bit number)
            var msb = (t & 0xf) << 28 >> 28;
            switch ((t >> 4) & 7)
            {
                case 0:
                    result = FeedByte() | (msb << 8);
                    break;
                case 1:
                    var ab1 = FeedByte();
                    var ab2 = FeedByte();
                    result = ab2 | (ab1 << 8) | (msb << 16);
                    break;
                case 2:
                    ab1 = FeedByte();
                    ab2 = FeedByte();
                    var ab3 = FeedByte();
                    //result = (int)(ab3 | (ab2 | (ab1 | ((uint)result & 0xff) << 8) << 8) << 8);
                    result = ab3 | (ab2 << 8) | (ab1 << 16) | (msb << 24);
                    break;
                case 3:
                    return NumberSpec.FromMem1Address(t & 0xf);
                case 4:
                    return NumberSpec.FromMem1Address(FeedByte() | ((t & 0xf) << 8));
                case 5:
                    return NumberSpec.FromMem3Address((t & 0xf) + 1);
                default:
                    throw new ArgumentException();
            }
            return NumberSpec.FromConstant(result);
        }

        private string FeedString()
        {
            var sz = FeedByte();
            var r = _memory.Span.Slice(_offset, sz)[..^1];
            Advance(sz);
            //Console.WriteLine($"Encountered bytes: {Convert.ToBase64String(r)}");
            return ShiftJis.GetString(r);
        }

        private ImmutableArray<string> FeedStringArray()
        {
            var sz = FeedByte();
            var r =_memory.Span.Slice(_offset, sz).ToArray();
            Advance(sz);
            var list = new List<string>();
            var pi = 0;
            for (var i = 0; i < r.Length - 1; i++)
                if (r[i] == 0)
                {
                    list.Add(ShiftJis.GetString(r[pi..i]));
                    pi = i + 1;
                }
            //Console.WriteLine($"Encountered bytestring array: [{string.Join(", ", list.Select(Convert.ToBase64String))}]");
            return list.ToImmutableArray();
        }

        private string FeedLongString()
        {
            var sz = FeedShort();
            var r = _memory.Span.Slice(_offset, sz)[..^1];
            Advance(sz);
            //Console.WriteLine($"Encountered string: {Convert.ToBase64String(r)}");
            return ShiftJis.GetString(r);
        }

        private void ValidateOffset(int offset)
        {
            if ((int)offset < 0 || offset >= _memory.Length)
                throw new InvalidOperationException();
        }

        private int FeedOffset()
        {
            var offset = checked((int)FeedInt());
            ValidateOffset(offset);
            _interestingOffsets.Add(offset);
            return offset;
        }

        public static DisassemblyView Disassemble(in ReadOnlyMemory<byte> memory, in int initialOffset)
        {
            var reader = new Disassembler(memory, initialOffset);
            
            var seenAddrMask = new BitArray(memory.Length - initialOffset);
            var seen = new SortedSet<int>();
            var toSee = new Queue<int>();
            seen.Add(initialOffset);
            toSee.Enqueue(initialOffset);
            /* just a simple BFS */
            while (toSee.Count > 0)
            {
                var offset = toSee.Dequeue();
                var (newOffsets, size) = reader.FeedOneBlock(offset);
                for (var i = offset; i < offset + size; i++)
                    seenAddrMask[i - initialOffset] = true;
                foreach (var u in newOffsets.OrderBy(x => x))
                {
                    if (seen.Contains(u)) continue;
                    seen.Add(u);
                    toSee.Enqueue(u);
                }
            }

            var nonSeenAddresses = new List<int>();
                
            for (var i = 0; i < seenAddrMask.Length; i++)
                if (!seenAddrMask[i])
                    nonSeenAddresses.Add(i + initialOffset);
            
            // TODO: why this fails?
            //Trace.Assert(nonSeenAddresses.Count < 16);

            return reader._disassemblyViewBuilder.Build();
        }
        
        public (int[] newOffsets, int size) FeedOneBlock(in int offset)
        {
            _offset = offset;
            ValidateOffset(offset);
            
            _interestingOffsets.Clear();
            while (true)
            {
                var startOffset = _offset;
                if (_disassemblyViewBuilder.HasVisited(_offset))
                    break;
                var (continueBlock, opcode, data) = FeedOneInstruction();
                _disassemblyViewBuilder.Visit(startOffset, _offset - startOffset, opcode, data.ToImmutableArray());
                
                if (!continueBlock)
                    break;
            }
            
            return (_interestingOffsets.ToArray(), _offset - offset);
        }

        private dynamic FeedElement(OpcodeEncodingElement element)
        {
            byte tempByte;
            switch (element)
            {
                case OpcodeEncodingElement.Byte: return FeedByte();
                case OpcodeEncodingElement.Address:
                case OpcodeEncodingElement.Short: return FeedShort();
                case OpcodeEncodingElement.Int: return FeedInt();
                case OpcodeEncodingElement.JumpOffset: return FeedOffset();
                case OpcodeEncodingElement.NumberArgument: return FeedNumber();

                case OpcodeEncodingElement.AddressArray:
                    return Enumerable.Range(0, FeedByte()).Select(_ => FeedShort()).ToImmutableArray();
                case OpcodeEncodingElement.NumberArray:
                    return Enumerable.Range(0, FeedByte()).Select(_ => FeedNumber()).ToImmutableArray();
                case OpcodeEncodingElement.JumpOffsetArray:
                    var sz = FeedShort();
                    return Enumerable.Range(0, sz).Select(_ => FeedOffset()).ToImmutableArray();
                case OpcodeEncodingElement.String: return FeedString();
                case OpcodeEncodingElement.LongString: return FeedLongString();
                case OpcodeEncodingElement.StringArray: return FeedStringArray();
                case OpcodeEncodingElement.BitmappedNumberArguments:
                    tempByte = FeedByte();
                    /* TODO: check if all of the opcodes have zero as default */
                    return Enumerable.Range(0, 8)
                        .Select(_ => (tempByte & (1 << _)) != 0 ? FeedNumber() : NumberSpec.FromConstant(0)).ToImmutableArray();
                case OpcodeEncodingElement.PostfixNotationExpression:
                    var rpne = new PostfixExpressionBuilder();
                    while (true)
                    {
                        tempByte = FeedByte();
                        if ((tempByte & 0x80) != 0) // bit 7 - stop bit 
                            return rpne.Build();
                        if (tempByte == 0) 
                            rpne.AddConstant(FeedNumber());
                        else if (tempByte < 0x20) // operations with values >= 0x20 are ignored
                            rpne.AddOperation(tempByte);
                    }
                case OpcodeEncodingElement.MessageId:
                    return FeedByte() | (FeedByte() << 8) | (FeedByte() << 16);
                case OpcodeEncodingElement.UnaryOperationArgument:
                    tempByte = FeedByte();
                    if ((tempByte & 0x80) != 0)
                        return new UnaryOperationArgument(tempByte, FeedShort(), FeedNumber());
                    else 
                        return new UnaryOperationArgument(tempByte, FeedShort());
                case OpcodeEncodingElement.BinaryOperationArgument:
                    tempByte = FeedByte();
                    if ((tempByte & 0x80) != 0)
                        return new BinaryOperationArgument(tempByte, FeedShort(), FeedNumber(), FeedNumber());
                    else 
                        return new BinaryOperationArgument(tempByte, FeedShort(), FeedNumber());
                default:
                    throw new ArgumentOutOfRangeException(nameof(element), element, null);
            }
        }

        private string ApplyStringFixup(string str)
        {
            var fixupTable = new[]
            {
                '\u3000', '\u3002', '\u300c', '\u300d', '\u3001', '\u2026', '\u3092', '\u3041', '\u3043', '\u3045',
                '\u3047', '\u3049', '\u3083', '\u3085', '\u3087', '\u3063', '\u30fc', '\u3042', '\u3044', '\u3046',
                '\u3048', '\u304a', '\u304b', '\u304d', '\u304f', '\u3051', '\u3053', '\u3055', '\u3057', '\u3059',
                '\u305b', '\u305d', '\u305f', '\u3061', '\u3064', '\u3066', '\u3068', '\u306a', '\u306b', '\u306c',
                '\u306d', '\u306e', '\u306f', '\u3072', '\u3075', '\u3078', '\u307b', '\u307e', '\u307f', '\u3080',
                '\u3081', '\u3082', '\u3084', '\u3086', '\u3088', '\u3089', '\u308a', '\u308b', '\u308c', '\u308d',
                '\u308f', '\u3093', '\uff01', '\uff1f'
            };

            return new string(str.Select(x =>
                {
                    if (x == '\uf8f0')
                        return '\u3000';
                    if (x >= 0xff61 && x - 0xff61 < 0x3f)
                        return fixupTable[x - 0xff60];
                    return x;
                }).ToArray());
        }

        private dynamic[] ApplyStringsFixup(dynamic[] data)
        {
            return data.Select(x =>
            {
                return x switch
                {
                    string x1 => ApplyStringFixup(x1),
                    ImmutableArray<string> x2 => x2.Select(ApplyStringFixup).ToImmutableArray(),
                    _ => x
                };
            }).ToArray();
        }

        private (bool, Opcode, dynamic[]) FeedOneInstruction()
        {
            var opcode = (Opcode)FeedByte();
            //Console.WriteLine($"0x{_offset-1:x6} {opcode}");
            
            var encoding = OpcodeDefinitions.GetEncoding(opcode);
            var data = encoding.Select(FeedElement).ToArray();
            var j = OpcodeDefinitions.IsJump(opcode);
            if (j && !OpcodeDefinitions.IsUnconditionalJump(opcode))
                _interestingOffsets.Add(_offset);
            
            //Console.WriteLine($"0x{_offset-1:x6} {opcode} {string.Join(", ", data)}");
            
            /* various instruction-specific fixups */
            
            if (OpcodeDefinitions.DoesNeedStringsFixup(opcode))
                data = ApplyStringsFixup(data);

            return (!j, opcode, data);
        }
    }
}