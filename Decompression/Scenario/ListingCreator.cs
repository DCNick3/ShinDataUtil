using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Scenario;
using static ShinDataUtil.Scenario.Opcode65.Operation;

namespace ShinDataUtil.Decompression.Scenario
{
    public class ListingCreator
    {
        private static readonly Common.ShiftJisEncoding ShiftJis = new Common.ShiftJisEncoding();
        private readonly Dictionary<int, string> _labels;

        private ListingCreator(int beginAddress)
        {
            _labels = new Dictionary<int, string>()
            {
                { beginAddress, "lab_begin" }
            };
        }
        
        private string FormatNumber(NumberSpec number)
        {
            if (number.IsConstant)
                return $"{number.Value}n";
            Trace.Assert(number.Address.HasValue);
            return $"@0x{number.Address ?? 0x0:x4}";
        }

        private string FormatJumpOffset(int offset)
        {
            if (_labels.TryGetValue(offset, out var name))
                return name;
            return $"0x{offset:x6}j";
        }

        private string FormatBinaryOperation(Instruction instruction)
        {
            Opcode65 opcode65 = instruction.Data[0];
            if (opcode65.Type == Argument2)
                return $"mov {FormatNumber(NumberSpec.FromAddress(opcode65.DestinationAddress))}, {FormatNumber(opcode65.Argument2)}";
            if (opcode65.Type == Zero)
                return $"mov {FormatNumber(NumberSpec.FromAddress(opcode65.DestinationAddress))}, 0";

            return opcode65.Type switch
            {
                Add => "add",
                Subtract => "sub",
                Multiply => "mul",
                Divide => "div",
                Remainder => "rem",
                BitwiseAnd => "and",
                BitwiseOr => "or",
                BitwiseXor => "xor",
                LeftShift => "lsh",
                RightShift => "rsh",
                _ => throw new ArgumentOutOfRangeException(),
            } + $" {FormatNumber(NumberSpec.FromAddress(opcode65.DestinationAddress))}, " + FormatNumber(opcode65.Argument1) + ", " + FormatNumber(opcode65.Argument2);
        }

        private string FormatConditionalJump(Instruction instruction)
        {
            var type = (byte) instruction.Data[0];
            var s = "j" + OpcodeDefinitions.DecodeJumpCondition(instruction) switch
            {
                JumpCondition.Equal => "eq",
                JumpCondition.NotEqual => "neq",
                JumpCondition.GreaterOrEqual => "ge",
                JumpCondition.Greater => "g",
                JumpCondition.LessOrEqual => "le",
                JumpCondition.Less => "l",
                JumpCondition.BitwiseAndNotZero => "anz", /* Jump if bitwise And is Not Zero */
                JumpCondition.BitwiseAndZero => "az", /* Jump if bitwise And is Zero */
                _ => throw new ArgumentOutOfRangeException(),
            };
            return s + $" {FormatNumber(instruction.Data[1])}, {FormatNumber(instruction.Data[2])}, " +
                   $"{FormatJumpOffset(instruction.Data[3])}";
        }

        //public static HashSet<ImmutableArray<byte>> arra = new HashSet<ImmutableArray<byte>>();

        private string FormatString(string data)
        {
            var decoded = data;//ShiftJis.GetString(data.AsSpan());
            decoded = decoded.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"\"{decoded}\"";
        }

        private string FormatPostfixExpression(PostfixExpression data)
        {
            var sb = new StringBuilder();

            foreach (var element in data.Elements)
            {
                if (element.Operation == PostfixExpression.Operation.Constant)
                {
                    Debug.Assert(element.NumberSpec != null, "element.numberSpec != null");
                    sb.Append($"{FormatNumber(element.NumberSpec.Value)} ");
                }
                else
                {
                    var c = element.Operation switch
                    {
                        PostfixExpression.Operation.Add => "+",
                        PostfixExpression.Operation.Subtract => "-",
                        PostfixExpression.Operation.Multiply => "*",
                        PostfixExpression.Operation.Divide => "/",
                        PostfixExpression.Operation.Remainder => "%",
                        PostfixExpression.Operation.Equals => "==",
                        PostfixExpression.Operation.Greater => ">",
                        PostfixExpression.Operation.Less => "<",
                        PostfixExpression.Operation.Negate => "neg",
                        PostfixExpression.Operation.AbsoluteValue => "abs",
                        PostfixExpression.Operation.BitwiseAnd => "&",
                        PostfixExpression.Operation.BitwiseNot => "~",
                        PostfixExpression.Operation.BitwiseOr => "|",
                        PostfixExpression.Operation.BitwiseXor => "^",
                        PostfixExpression.Operation.LeftShift => "<<",
                        PostfixExpression.Operation.RightShift => ">>",
                        _ => throw new ArgumentException(),
                    };
                    sb.Append($"{c} ");
                }
            }

            if (sb.Length > 0)
                sb.Remove(sb.Length - 1, 1);
            
            return sb.ToString();
        }
        
        private string FormatElement(Opcode opcode, int index, OpcodeEncodingElement element, dynamic data)
        {
            return element switch
            {
                OpcodeEncodingElement.Byte => $"{data}b",
                OpcodeEncodingElement.Short => $"{data}s",
                OpcodeEncodingElement.Address => FormatNumber(NumberSpec.FromAddress(data)),
                OpcodeEncodingElement.Int => $"{data}i",
                OpcodeEncodingElement.String => $"S{FormatString(data)}",
                OpcodeEncodingElement.LongString => $"L{FormatString(data)}",
                OpcodeEncodingElement.NumberArgument => FormatNumber(data),
                OpcodeEncodingElement.JumpOffset => FormatJumpOffset(data),
                OpcodeEncodingElement.AddressArray => $"a[{string.Join(",", ((ImmutableArray<ushort>) data).Select(NumberSpec.FromAddress).Select(FormatNumber))}]",
                OpcodeEncodingElement.StringArray => $"S[{string.Join(",", ((ImmutableArray<string>)data).Select(FormatString))}]",
                OpcodeEncodingElement.NumberArray =>
                $"n[{string.Join(",", ((ImmutableArray<NumberSpec>) data).Select(FormatNumber))}]",
                OpcodeEncodingElement.JumpOffsetArray =>
                $"j[{string.Join(",", ((ImmutableArray<int>) data).Select(FormatJumpOffset))}]",
                OpcodeEncodingElement.PostfixNotationExpression => $"rpne{{{FormatPostfixExpression(data)}}}",
                OpcodeEncodingElement.BitmappedNumberArguments => 
                $"bmn[{string.Join(",", ((ImmutableArray<NumberSpec>) data).Select(FormatNumber))}]",
                OpcodeEncodingElement.MessageId => $"{data}mi",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private string FormatInstruction(Instruction instruction)
        {
            if (instruction.Opcode == Opcode.bo)
                return FormatBinaryOperation(instruction);
            if (instruction.Opcode == Opcode.jc)
                return FormatConditionalJump(instruction);
            
            var sb = new StringBuilder();
            sb.Append(instruction.Opcode);

            if (instruction.Data.Length > 0)
            {
                sb.Append(" ");
                // ReSharper disable once InvokeAsExtensionMethod
                sb.Append(string.Join(", ", Enumerable.Zip(instruction.Encoding, instruction.Data)
                    .Select((_, i) => FormatElement(instruction.Opcode, i, _.First, _.Second))));
            }

            return sb.ToString();
        }
        
        public static ListingAddressMap CreateListing(DisassemblyView view, 
            IReadOnlyDictionary<int, string> userLabels, TextWriter textWriter)
        {
            var creator = new ListingCreator(view.BeginAddress);
            foreach (var (addr, name) in userLabels) 
                creator._labels[addr] = name;

            foreach (var (_, instr) in view.EnumerateInstructions())
            foreach (var jumpOffset in instr.CodeXRefOut)
            {
                if (userLabels.ContainsKey(jumpOffset)) 
                    continue;
                var prefix = instr.Opcode switch
                {
                    Opcode.call => "FUN_",
                    _ => "LAB_",
                };
                if (creator._labels.TryGetValue(jumpOffset, out var lab))
                    if (lab.StartsWith("FUN_"))
                        continue;
                creator._labels[jumpOffset] = $"{prefix}{jumpOffset:x6}";
            }

            var mapBuilder = new ListingAddressMap.Builder();
            foreach (var (addr, instr) in view.EnumerateInstructions())
            {
                if (creator._labels.TryGetValue(addr, out var name))
                {
                    mapBuilder.EmitLine(addr);
                    textWriter.WriteLine($"{name}:");
                }

                mapBuilder.EmitLine(addr);
                textWriter.WriteLine($"        {creator.FormatInstruction(instr)}");
            }

            return mapBuilder.Build();
        }

        public static string CreateListing(DisassemblyView view, 
            IReadOnlyDictionary<int, string> userLabels)
        {
            var sb = new StringWriter();
            CreateListing(view, userLabels, sb);
            return sb.ToString();
        }
    }
}