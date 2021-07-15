using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Decompression.Scenario;
using ShinDataUtil.Scenario;

namespace ShinDataUtil.Compression.Scenario
{
    /// <summary>
    /// Implements a code generation stage for assembler (converts internal code representation to binary code)
    /// </summary>
    public static class Assembler
    {
        private static readonly Common.ShiftJisEncoding ShiftJis = new Common.ShiftJisEncoding();

        private static readonly ImmutableDictionary<char, int> FixupTable = new Dictionary<char, int>
        {
            {'\u3000', -1648}, {'\u3002', 1}, {'\u300c', 2}, {'\u300d', 3}, {'\u3001', 4}, {'\u2026', 5}, {'\u3092', 6},
            {'\u3041', 7}, {'\u3043', 8}, {'\u3045', 9}, {'\u3047', 10}, {'\u3049', 11}, {'\u3083', 12}, {'\u3085', 13},
            {'\u3087', 14}, {'\u3063', 15}, {'\u30fc', 16}, {'\u3042', 17}, {'\u3044', 18}, {'\u3046', 19},
            {'\u3048', 20}, {'\u304a', 21}, {'\u304b', 22}, {'\u304d', 23}, {'\u304f', 24}, {'\u3051', 25},
            {'\u3053', 26}, {'\u3055', 27}, {'\u3057', 28}, {'\u3059', 29}, {'\u305b', 30}, {'\u305d', 31},
            {'\u305f', 32}, {'\u3061', 33}, {'\u3064', 34}, {'\u3066', 35}, {'\u3068', 36}, {'\u306a', 37},
            {'\u306b', 38}, {'\u306c', 39}, {'\u306d', 40}, {'\u306e', 41}, {'\u306f', 42}, {'\u3072', 43},
            {'\u3075', 44}, {'\u3078', 45}, {'\u307b', 46}, {'\u307e', 47}, {'\u307f', 48}, {'\u3080', 49},
            {'\u3081', 50}, {'\u3082', 51}, {'\u3084', 52}, {'\u3086', 53}, {'\u3088', 54}, {'\u3089', 55},
            {'\u308a', 56}, {'\u308b', 57}, {'\u308c', 58}, {'\u308d', 59}, {'\u308f', 60}, {'\u3093', 61},
            {'\uff01', 62}, {'\uff1f', 63}
        }.ToImmutableDictionary();

        private static string ApplyStringFixup(string str)
        {
            return new string(str.Select(c =>
            {
                if (FixupTable.TryGetValue(c, out var t))
                    return (char) ('\uff60' + t);
                return c;
            }).ToArray());
        }
        
        private static int InstructionLength(Instruction instruction)
        {
            var ee = instruction.Encoding.Zip(instruction.Data);
            return ee.Select(_ => (int)ElementLength(instruction.Opcode, _.First, _.Second)).Sum() + 1;
        }

        private static int StringLength(Opcode opcode, string str)
        {
            if (!OpcodeDefinitions.DoesNeedStringsFixup(opcode))
                return ShiftJis.GetByteCount(str);
            var len = 0;
            var r = new char[1];
            foreach (var c in str)
            {
                //if (c == '\u3000')
                //    span[0] = '\uf8f0';
                if (FixupTable.TryGetValue(c, out var t))
                    r[0] = (char) ('\uff60' + t);
                else
                    r[0] = c;
                len += ShiftJis.GetByteCount(r);
            }

            return len;
        }
        
        private static int ElementLength(Opcode opcode, OpcodeEncodingElement encoding, dynamic value)
        {
            switch (encoding)
            {
                case OpcodeEncodingElement.Byte: return 1;
                case OpcodeEncodingElement.Short: return 2;
                case OpcodeEncodingElement.MessageId: return 3;
                case OpcodeEncodingElement.Int: return 4;
                case OpcodeEncodingElement.Address: return 2;
                case OpcodeEncodingElement.JumpOffset: return 4;
                case OpcodeEncodingElement.NumberArgument: return NumberLength((NumberSpec)value);
                case OpcodeEncodingElement.AddressArray: return 1 + 2 * ((ImmutableArray<ushort>)value).Length;
                case OpcodeEncodingElement.NumberArray:
                    return 1 + ((ImmutableArray<NumberSpec>) value).Select(NumberLength).Sum();
                case OpcodeEncodingElement.JumpOffsetArray: return 2 + 4 * ((ImmutableArray<int>) value).Length;
                case OpcodeEncodingElement.String: return 1 + StringLength(opcode, (string)value) + 1;
                case OpcodeEncodingElement.LongString: return 2 + StringLength(opcode, (string)value) + 1;
                case OpcodeEncodingElement.StringArray:
                    return 1 + ((ImmutableArray<string>) value).Select(_ => StringLength(opcode, _) + 1).Sum() + 1;
                case OpcodeEncodingElement.BitmappedNumberArguments:
                    return 1 + ((ImmutableArray<NumberSpec>) value).Select(_ => _.Value == 0 ? 0 : NumberLength(_))
                        .Sum();
                case OpcodeEncodingElement.PostfixNotationExpression: return RpneLength((PostfixExpression)value);
                case OpcodeEncodingElement.BinaryOperationArgument: return BinaryOperationArgumentLength((BinaryOperationArgument)value);
                case OpcodeEncodingElement.UnaryOperationArgument: return UnaryOperationArgumentLength((UnaryOperationArgument) value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null);
            }
        }

        private static int UnaryOperationArgumentLength(UnaryOperationArgument value)
        {
            if (value.ShouldHaveArgumentSeparatelyEncoded)
                return 1 + 2 + NumberLength(value.Argument);
            return 1 + 2;
        }

        private static int BinaryOperationArgumentLength(BinaryOperationArgument value)
        {
            if (value.ShouldHaveFirstArgumentSeparatelyEncoded)
                return 1 + 2 + NumberLength(value.Argument1) + NumberLength(value.Argument2);
            return 1 + 2 + NumberLength(value.Argument2);
        }

        private static int RpneLength(PostfixExpression value)
        {
            var r = 1;
            foreach (var element in value.Elements)
            {
                r += 1;
                if (element.Operation == PostfixExpression.Operation.Constant)
                {
                    Debug.Assert(element.NumberSpec != null, "element.numberSpec != null");
                    r += NumberLength(element.NumberSpec.Value);
                }
            }

            return r;
        }

        private static int NumberLength(NumberSpec value)
        {
            if (value.IsConstant)
            {
                Debug.Assert(value.Value != null, "value.Value != null");
                var val = value.Value.Value;
                if (val <= 63 && val >= -64) // [-2**6; 2**6 - 1]
                    return 1;
                if (val <= 2047 && val >= -2048) // [-2**11; 2**11 - 1]
                    return 2;
                if (val <= 524287 && val >= -524288) // [-2**19; 2**19 - 1]
                    return 3;
                if (val <= 134217727 && val >= -134217728) // [-2**27; 2**27 - 1]
                    return 4;
                throw new ArgumentException();
            }
            Debug.Assert(value.Address != null, "value.Address != null");
            var addr = value.Address.Value;
            if (addr < 16)
                return 1;
            if (addr < 4096)
                return 2;
            throw new ArgumentException();
        }

        private static void EncodeNumber(BinaryWriter bw, NumberSpec value)
        {
            if (value.IsConstant)
            {
                Debug.Assert(value.Value != null, "value.Value != null");
                var val = value.Value.Value;
                if (val <= 63 && val >= -64) // [-2**6; 2**6 - 1]
                    bw.Write((byte)(val & 0x7f));
                else if (val <= 2047 && val >= -2048) // [-2**11; 2**11 - 1]
                {
                    // ReSharper disable once ShiftExpressionZeroLeftOperand
                    bw.Write((byte)(0x80 | (0 << 4) | (val >> 8) & 0xf));
                    bw.Write((byte)val);
                }
                else if (val <= 524287 && val >= -524288) // [-2**19; 2**19 - 1]
                {
                    bw.Write((byte)(0x80 | (1 << 4) | (val >> 16) & 0xf));
                    bw.Write((byte)(val >> 8));
                    bw.Write((byte)val);
                }
                else if (val <= 134217727 && val >= -134217728) // [-2**27; 2**27 - 1]
                {
                    bw.Write((byte)(0x80 | (2 << 4) | (val >> 24) & 0xf));
                    bw.Write((byte)(val >> 16));
                    bw.Write((byte)(val >> 8));
                    bw.Write((byte)val);
                }
                else 
                    throw new ArgumentException();
            }
            else
            {
                Debug.Assert(value.Address != null, "value.Address != null");
                var addr = checked((ushort) value.Address.Value);
                if (!value.IsMem3)
                {
                    if (addr < 16)
                        bw.Write((byte) (0x80 | (3 << 4) | addr & 0xf));
                    else if (addr < 4096)
                    {
                        bw.Write((byte) (0x80 | (4 << 4) | (addr >> 8) & 0xf));
                        bw.Write((byte) addr);
                    }
                    else
                        throw new ArgumentException();
                }
                else
                {
                    Trace.Assert(addr >= 1 && addr <= 16);
                    bw.Write((byte) (0x80 | (5 << 4) | (addr - 1) & 0xf));
                }
            }
        }

        private static void EncodeArray<T>(BinaryWriter bw, ImmutableArray<T> data, Action<BinaryWriter, T> encodeOne)
        {
            bw.Write(checked((byte)data.Length));
            foreach (var el in data) 
                encodeOne(bw, el);
        }

        private static void EncodeLongerArray<T>(BinaryWriter bw, ImmutableArray<T> data, Action<BinaryWriter, T> encodeOne)
        {
            bw.Write(checked((ushort)data.Length));
            foreach (var el in data) 
                encodeOne(bw, el);
        }

        private static void EncodeStringCore(BinaryWriter bw, Opcode opcode, string str, Action<BinaryWriter, int> lengthEnc)
        {
            if (OpcodeDefinitions.DoesNeedStringsFixup(opcode))
                str = ApplyStringFixup(str);
            var buffer = ArrayPool<byte>.Shared.Rent(ShiftJis.GetMaxByteCount(str.Length) + 1);
            try
            {   
                var used = ShiftJis.GetBytes(str, buffer);
                buffer[used] = 0;
                lengthEnc(bw, used + 1);
                bw.Write(buffer[..(used + 1)]);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        
        private static void EncodeString(BinaryWriter bw, Opcode opcode, string value)
        {
            EncodeStringCore(bw, opcode, value, (bw, l) => bw.Write((byte)l));
        }

        private static void EncodeLongString(BinaryWriter bw, Opcode opcode, string value)
        {
            EncodeStringCore(bw, opcode, value, (bw, l) => bw.Write((ushort)l));
        }

        private static void EncodeStringArray(BinaryWriter bw, Opcode opcode, ImmutableArray<string> value)
        {
            var ms = new MemoryStream();
            var bw1 = new BinaryWriter(ms, Encoding.UTF8, true);

            foreach (var s in value)
                EncodeStringCore(bw1, opcode, s, (writer, i) => { });
            bw1.Write((byte)0);
            bw1.Close();
            
            bw.Write(checked((byte)ms.Length));
            bw.Write(ms.GetBuffer()[..(int)ms.Length]);
        }

        private static void EncodeBitmappedNumberArguments(BinaryWriter bw, ImmutableArray<NumberSpec> value)
        {
            byte b = 0;
            Debug.Assert(value.Length == 8);
            for (var i = 0; i < 8; i++)
            {
                if (value[i].Value != 0)
                    b |= (byte)(1 << i);
            }
            bw.Write(b);
            for (var i = 0; i < 8; i++)
            {
                if ((b & (1 << i)) != 0)
                    EncodeNumber(bw, value[i]);
            }
        }

        private static void EncodeRpne(BinaryWriter bw, PostfixExpression value)
        {
            foreach (var element in value.Elements)
            {
                bw.Write((byte)element.Operation);
                if (element.Operation == PostfixExpression.Operation.Constant)
                {
                    Debug.Assert(element.NumberSpec != null, "element.numberSpec != null");
                    EncodeNumber(bw, element.NumberSpec.Value);
                }
            }
            bw.Write((byte)0x80); /* end-of-expression marker */
        }

        private static void EncodeBinaryOperation(BinaryWriter bw, BinaryOperationArgument value)
        {
            if (value.ShouldHaveFirstArgumentSeparatelyEncoded)
            {
                var type = (byte) (0x80 | (int) value.Type);
                bw.Write(type);
                bw.Write(value.DestinationAddress);
                EncodeNumber(bw, value.Argument1);
                EncodeNumber(bw, value.Argument2);
            }
            else
            {
                var type = (byte) (value.Type);
                bw.Write(type);
                bw.Write(value.DestinationAddress);
                EncodeNumber(bw, value.Argument2);
            }
        }

        private static void EncodeUnaryOperation(BinaryWriter bw, UnaryOperationArgument value)
        {
            if (value.ShouldHaveArgumentSeparatelyEncoded)
            {
                var type = (byte) (0x80 | (int) value.Type);
                bw.Write(type);
                bw.Write(value.DestinationAddress);
                EncodeNumber(bw, value.Argument);
            }
            else
            {
                var type = (byte) (value.Type);
                bw.Write(type);
                bw.Write(value.DestinationAddress);
            }
        }
        
        private static void EncodeElement(BinaryWriter bw, Opcode opcode, OpcodeEncodingElement encoding, dynamic value)
        {
            switch (encoding)
            {
                case OpcodeEncodingElement.Byte: bw.Write((byte)value); break;
                case OpcodeEncodingElement.Short: bw.Write((ushort)value); break;
                case OpcodeEncodingElement.MessageId:
                    var i = (int) value;
                    bw.Write((byte)i);
                    bw.Write((byte)(i >> 8));
                    bw.Write((byte) (i >> 16));
                    break;
                case OpcodeEncodingElement.Int: bw.Write((int)value); break;
                case OpcodeEncodingElement.Address: bw.Write((ushort)value); break;
                case OpcodeEncodingElement.JumpOffset: bw.Write((int)value); break;
                case OpcodeEncodingElement.NumberArgument: EncodeNumber(bw, (NumberSpec) value); break;
                case OpcodeEncodingElement.AddressArray: 
                    EncodeArray(bw, (ImmutableArray<ushort>) value, (bw1, el) => bw1.Write(el)); break;
                case OpcodeEncodingElement.NumberArray:
                    EncodeArray(bw, (ImmutableArray<NumberSpec>) value, EncodeNumber); break;
                case OpcodeEncodingElement.JumpOffsetArray:
                    EncodeLongerArray(bw, (ImmutableArray<int>) value, (bw1, el) => bw1.Write(el)); break;
                case OpcodeEncodingElement.String: EncodeString(bw, opcode, (string) value); break;
                case OpcodeEncodingElement.LongString: EncodeLongString(bw, opcode, (string) value); break;
                case OpcodeEncodingElement.StringArray: EncodeStringArray(bw, opcode, (ImmutableArray<string>)value); break;
                case OpcodeEncodingElement.BitmappedNumberArguments:
                    EncodeBitmappedNumberArguments(bw, (ImmutableArray<NumberSpec>) value); break;
                case OpcodeEncodingElement.PostfixNotationExpression: EncodeRpne(bw, (PostfixExpression) value); break;
                case OpcodeEncodingElement.BinaryOperationArgument: EncodeBinaryOperation(bw, (BinaryOperationArgument) value); break;
                case OpcodeEncodingElement.UnaryOperationArgument: EncodeUnaryOperation(bw, (UnaryOperationArgument) value); break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null);
            }
        }

        private static void EncodeInstruction(BinaryWriter bw, Instruction instruction)
        {
            bw.Write((byte)instruction.Opcode);
            foreach (var (encoding, data) in instruction.Encoding.Zip(instruction.Data))
                EncodeElement(bw, instruction.Opcode, encoding, data);
        }
        
        public static ImmutableArray<Instruction> FixupJumpOffsets(int baseOffset,
            IReadOnlyList<Instruction> instructions)
        {
            var instructionOffsets = new int[instructions.Count];
            var offset = baseOffset;
            for (var i = 0; i < instructions.Count; i++)
            {
                instructionOffsets[i] = offset;
                offset += InstructionLength(instructions[i]);
            }

            //17325232

            return instructions.Select(instr =>
            {
                foreach (var (j, element) in instr.Encoding.Select((x, j) => (j, x)))
                {
                    if (element == OpcodeEncodingElement.JumpOffset)
                    {
                        instr = instr.ChangeData(instr.Data.SetItem(j, instructionOffsets[instr.Data[j]]));
                    }
                    else if (element == OpcodeEncodingElement.JumpOffsetArray)
                    {
                        ImmutableArray<int> addresses = instr.Data[j];
                        addresses = addresses.Select(_ => instructionOffsets[_]).ToImmutableArray();
                        instr = instr.ChangeData(instr.Data.SetItem(j, addresses));
                    }
                }

                return instr;
            }).ToImmutableArray();
        }
        
        public static void Assemble(IReadOnlyList<Instruction> instructions, Stream output)
        {
            var bw = new BinaryWriter(output, ShiftJis, true);

            foreach (var instruction in instructions)
            {
                var pos0 = bw.BaseStream.Position;
                EncodeInstruction(bw, instruction);
                var pos1 = bw.BaseStream.Position;
                var actualLength = pos1 - pos0;
                var predictedLength = InstructionLength(instruction);
                if (actualLength != predictedLength)
                    Debugger.Break();
                Debug.Assert(actualLength == predictedLength);
                // this code is useful for diffing
                /*var exp = 0x00006d72;
                if (pos0 <= exp && pos1 > exp)
                {
                    var p = pos0 + 1;
                    var dataOffsets = instruction.Encoding.Select((x, i) =>
                    {
                        var pp = p;
                        p += ElementLength(instruction.Opcode, x, instruction.Data[i]);
                        return pp;
                    }).ToImmutableArray();
                    Debugger.Break();
                }*/
            }
            
            bw.Close();
        }
    }
}