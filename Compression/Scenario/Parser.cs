using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Scenario;

namespace ShinDataUtil.Compression.Scenario
{
    /// <summary>
    /// Implements a parsing stage for assembler (converts text to internal code representation)
    /// </summary>
    public class Parser
    {
        private static readonly int MaxOpcodeNameLength = Enum.GetNames(typeof(Opcode)).Max(_ => _.Length);

        private static readonly ImmutableDictionary<string, Opcode> OpcodeNameToOpcode
            = Enum.GetValues(typeof(Opcode)).Cast<Opcode>()
                .ToImmutableDictionary(_ => _.ToString());

        private static readonly ImmutableHashSet<string> Opcode65Mnemonics = new HashSet<string>
        {
            "add", "sub", "mul", "div", "rem", "and", "or", "xor", "lsh", "rsh",
            "mov"
        }.ToImmutableHashSet();

        private static readonly ImmutableHashSet<string> ConditionalJumpMnemonics = new HashSet<string>
        {
            "jeq", "jneq", "jge", "jg", "jle", "jl", "janz", "jaz",
        }.ToImmutableHashSet();

        private readonly TextReader _input;
        private readonly Dictionary<string, int> _labelIds = new Dictionary<string, int>();
        private readonly Dictionary<int, int> _labelIdToInstructionNumber = new Dictionary<int, int>();
        private int _currentInstructionIndex = 0;
        private int _currentLabelId = 0;
        private int _lineNumber = 0;

        public Parser(TextReader input) => _input = input;

        private string? ReadLine()
        {
            _lineNumber++;
            return _input.ReadLine()?.Trim();
        }

        private static string? TryParseLabel(string line) => line[^1] == ':' ? line[..^1] : null;

        private bool TryUseAsLabel(string line)
        {
            var l = TryParseLabel(line);
            if (l == null) return false;
            if (!_labelIds.TryGetValue(l, out var id))
                id = _labelIds[l] = _currentLabelId++;
            if (!_labelIdToInstructionNumber.TryAdd(id, _currentInstructionIndex))
                throw new ParseExceptionInternal($"Duplicate label: {l}");
            return true;
        }

        private Instruction? ReadOneInstruction()
        {
            while (true)
            {
                var line = ReadLine();
                if (line == null)
                    return null;
                if (line == "" || line[0] == '#')
                    continue;
                if (!TryUseAsLabel(line))
                    return ParseInstruction(line);
            }
        }

        private string ReadOpcodeName(ref ReadOnlySpan<char> line)
        {
            var sb = new StringBuilder(MaxOpcodeNameLength);
            while (true)
            {
                if (line.Length == 0 || char.IsWhiteSpace(line[0]))
                    return sb.ToString();

                sb.Append(line[0]);
                line = line[1..];
            }
        }

        private long ParseHexInteger(ref ReadOnlySpan<char> line)
        {
            long r = 0;
            while (true)
            {
                if (line.Length == 0)
                    return r;
                var diff1 = line[0] - '0';

                if (diff1 < 0 || diff1 > 9)
                {
                    var diff2 = line[0] - 'a';
                    if (diff2 < 0 || diff2 > 6)
                        return r;
                    r = r * 16 + 10 + diff2;
                }
                else
                    r = r * 16 + diff1;

                line = line[1..];
            }
        }

        private long ParseInteger(ref ReadOnlySpan<char> line)
        {
            int sign = 1;
            long r = 0;
            if (line.Length > 0 && line[0] == '-')
            {
                line = line[1..];
                sign = -1;
            }
            
            while (true)
            {
                if (line.Length == 0)
                    return r * sign;
                var diff = line[0] - '0';
                if (diff < 0 || diff > 9)
                    return r * sign;
                r = r * 10 + diff;
                line = line[1..];
            }
        }

        string ParseIdentifier(ref ReadOnlySpan<char> line)
        {
            var sb = new StringBuilder();
            while (true)
            {
                if (line.Length == 0)
                    return sb.ToString();
                var c = line[0];
                if (!char.IsDigit(c) && !char.IsLetter(c) && c != '_')
                    return sb.ToString();
                sb.Append(c);
                line = line[1..];
            }
        }

        int ParseIntegerWithSuffix(ref ReadOnlySpan<char> line, string suffix)
        {
            var r = checked((int) ParseInteger(ref line));
            ExpectFixedFeed(ref line, suffix);
            return r;
        }

        ushort ParseAddress(ref ReadOnlySpan<char> line)
        {
            ExpectFixedFeed(ref line, "@0x");
            var lineOld = line;
            var r = ParseHexInteger(ref line);
            /*if (r == 55)
            {
                ParseHexInteger(ref lineOld);
            }*/
            return checked((ushort) r);
        }

        string ParseString(ref ReadOnlySpan<char> line)
        {
            ExpectFixedFeed(ref line, '"');
            var sb = new StringBuilder();
            while (true)
            {
                var c = line[0];
                line = line[1..];
                switch (c)
                {
                    case '"':
                        return sb.ToString();
                    case '\\':
                        c = line[0];
                        line = line[1..];
                        sb.Append(c);
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
        }

        string ParseStringWithPrefix(ref ReadOnlySpan<char> line, char prefix)
        {
            ExpectFixedFeed(ref line, prefix);
            return ParseString(ref line);
        }


        ImmutableArray<T> ParseArray<T>(ref ReadOnlySpan<char> line, ParseDataPiece<T> feedOne)
        {
            ExpectFixedFeed(ref line, '[');
            FeedWhitespace(ref line);
            var r = new List<T>(8);
            while (true)
            {
                if (line[0] == ']')
                {
                    line = line[1..];
                    return r.ToImmutableArray();
                }

                r.Add(feedOne(ref line));
                FeedWhitespace(ref line);
                if (line[0] != ']')
                    ExpectFixedFeed(ref line, ',');
                FeedWhitespace(ref line);
            }
        }

        ImmutableArray<T> ParseArrayWithPrefix<T>(ref ReadOnlySpan<char> line, ParseDataPiece<T> feedOne, string prefix)
        {
            ExpectFixedFeed(ref line, prefix);
            return ParseArray(ref line, feedOne);
        }

        int ParseJumpOffset(ref ReadOnlySpan<char> line)
        {
            /* only labels are allowed */
            var label = ParseIdentifier(ref line);
            if (!_labelIds.TryGetValue(label, out var id))
                id = _labelIds[label] = _currentLabelId++;
            return id;
        }

        NumberSpec ParseNumber(ref ReadOnlySpan<char> line)
        {
            if (line[0] == '@')
                return NumberSpec.FromAddress(ParseAddress(ref line));
            return NumberSpec.FromConstant(ParseIntegerWithSuffix(ref line, "n"));
        }

        delegate T ParseDataPiece<T>(ref ReadOnlySpan<char> line);

        private dynamic ParseData(ref ReadOnlySpan<char> line, OpcodeEncodingElement encodingElement)
        {

            if (line.Length == 0)
                throw new ParseExceptionInternal($"Expected {encodingElement}, but found end-of-line");
            switch (encodingElement)
            {
                case OpcodeEncodingElement.Byte:
                    return checked((byte) ParseIntegerWithSuffix(ref line, "b"));
                case OpcodeEncodingElement.Short:
                    return checked((ushort) ParseIntegerWithSuffix(ref line, "s"));
                case OpcodeEncodingElement.Int:
                    return ParseIntegerWithSuffix(ref line, "i");
                case OpcodeEncodingElement.Address:
                    return ParseAddress(ref line);
                case OpcodeEncodingElement.JumpOffset:
                    return ParseJumpOffset(ref line);
                case OpcodeEncodingElement.NumberArgument:
                    return ParseNumber(ref line);
                case OpcodeEncodingElement.String:
                    return ParseStringWithPrefix(ref line, 'S');
                case OpcodeEncodingElement.LongString:
                    return ParseStringWithPrefix(ref line, 'L');
                case OpcodeEncodingElement.AddressArray:
                    return ParseArrayWithPrefix(ref line, ParseAddress, "a");
                case OpcodeEncodingElement.StringArray:
                    return ParseArrayWithPrefix(ref line, ParseString, "S");
                case OpcodeEncodingElement.NumberArray:
                    return ParseArrayWithPrefix(ref line, ParseNumber, "n");
                case OpcodeEncodingElement.JumpOffsetArray:
                    return ParseArrayWithPrefix(ref line, ParseJumpOffset, "j");
                case OpcodeEncodingElement.BitmappedNumberArguments:
                    return ParseArrayWithPrefix(ref line, ParseNumber, "bmn");
                case OpcodeEncodingElement.PostfixNotationExpression:
                    return ParseRpne(ref line);
                
                case OpcodeEncodingElement.MessageId:
                    return ParseIntegerWithSuffix(ref line, "mi");
                default:
                    throw new ArgumentOutOfRangeException(nameof(encodingElement), encodingElement, null);
            }
        }

        private PostfixExpression ParseRpne(ref ReadOnlySpan<char> line)
        {
            var builder = new PostfixExpressionBuilder();
            ExpectFixedFeed(ref line, "rpne{");

            while (true)
            {
                FeedWhitespace(ref line);
                var c = line[0];
                switch (c)
                {
                    case '@':
                    case { } when '0' <= c && c <= '9':
                        builder.AddConstant(ParseNumber(ref line));
                        continue;
                    case '-' when '0' <= line[1] && line[1] <= '9':
                        builder.AddConstant(ParseNumber(ref line));
                        continue;
                    case '-':
                        builder.AddOperation(PostfixExpression.Operation.Subtract);
                        break;
                    case '+':
                        builder.AddOperation(PostfixExpression.Operation.Add);
                        break;
                    case '*':
                        builder.AddOperation(PostfixExpression.Operation.Multiply);
                        break;
                    case '/':
                        builder.AddOperation(PostfixExpression.Operation.Divide);
                        break;
                    case '%':
                        builder.AddOperation(PostfixExpression.Operation.Remainder);
                        break;
                    case '=':
                        ExpectFixedFeed(ref line, "==");
                        builder.AddOperation(PostfixExpression.Operation.Equals);
                        continue;
                    case '>' when line[1] != '>':
                        builder.AddOperation(PostfixExpression.Operation.Greater);
                        break;
                    case '<' when line[1] != '<':
                        builder.AddOperation(PostfixExpression.Operation.Less);
                        break;
                    case 'n':
                        ExpectFixedFeed(ref line, "neg");
                        builder.AddOperation(PostfixExpression.Operation.Negate);
                        continue;
                    case 'a':
                        ExpectFixedFeed(ref line, "abs");
                        builder.AddOperation(PostfixExpression.Operation.AbsoluteValue);
                        continue;
                    case '>':
                        ExpectFixedFeed(ref line, ">>");
                        builder.AddOperation(PostfixExpression.Operation.RightShift);
                        continue;
                    case '<':
                        ExpectFixedFeed(ref line, "<<");
                        builder.AddOperation(PostfixExpression.Operation.LeftShift);
                        continue;
                    case '&':
                        builder.AddOperation(PostfixExpression.Operation.BitwiseAnd);
                        break;
                    case '|':
                        builder.AddOperation(PostfixExpression.Operation.BitwiseOr);
                        break;
                    case '^':
                        builder.AddOperation(PostfixExpression.Operation.BitwiseXor);
                        break;
                    case '~':
                        builder.AddOperation(PostfixExpression.Operation.BitwiseNot);
                        break;
                    case '}':
                        break;
                    default:
                        throw new ParseExceptionInternal($"Can't parse rpne on character {c}");
                }

                line = line[1..];
                if (c == '}')
                    break;
            }

            return builder.Build();
        }

        private void FeedWhitespace(ref ReadOnlySpan<char> line)
        {
            while (line.Length != 0 && char.IsWhiteSpace(line[0]))
                line = line[1..];
        }

        private void ExpectFixedFeed(ref ReadOnlySpan<char> line, char c)
        {
            if (line.Length == 0)
                throw new ParseExceptionInternal($"Expected {c}, but found end-of-line");
            var f = line[0];
            if (c != f)
                throw new ParseExceptionInternal($"Expected {c}, but found {f}");
            line = line[1..];
        }

        private void ExpectFixedFeed(ref ReadOnlySpan<char> line, string c)
        {
            if (line.Length < c.Length)
                throw new ParseExceptionInternal($"Expected {c}, but found end-of-line");
            var f = line[..c.Length];
            if (!f.SequenceEqual(c))
                throw new ParseExceptionInternal($"Expected {c}, but found {new string(line[..c.Length].ToArray())}");
            line = line[c.Length..];
        }

        private Instruction ParseInstruction(ReadOnlySpan<char> line)
        {
            var opcodeName = ReadOpcodeName(ref line);

            if (ConditionalJumpMnemonics.Contains(opcodeName))
            {
                var res = ParseConditionalJump(ref line, opcodeName);
                _currentInstructionIndex++;
                return res;
            }

            if (Opcode65Mnemonics.Contains(opcodeName))
            {
                var res = ParseOpcode65(ref line, opcodeName);
                _currentInstructionIndex++;
                return res;
            }

            if (!OpcodeNameToOpcode.TryGetValue(opcodeName, out var opcode))
                throw new ParseExceptionInternal($"Unknown opcode: {opcodeName}");

            var encoding = OpcodeDefinitions.GetEncoding(opcode);
            var data = new dynamic[encoding.Length];

            foreach (var (i, x) in encoding.Select((x, i) => (i, x)))
            {
                FeedWhitespace(ref line);
                data[i] = ParseData(ref line, x);
                FeedWhitespace(ref line);
                if (i != encoding.Length - 1)
                {
                    ExpectFixedFeed(ref line, ',');
                    FeedWhitespace(ref line);
                }
            }

            FeedWhitespace(ref line);
            if (line.Length != 0)
                throw new ParseExceptionInternal("Junk at end of line");

            _currentInstructionIndex++;
            return new Instruction(opcode, data.ToImmutableArray());
        }

        private Instruction ParseOpcode65(ref ReadOnlySpan<char> line, string opcodeName)
        {
            ushort destination;
            if (opcodeName == "mov")
            {
                FeedWhitespace(ref line);
                destination = ParseAddress(ref line);
                ExpectFixedFeed(ref line, ',');
                FeedWhitespace(ref line);
                if (line[0] == '@')
                {
                    var t = ParseNumber(ref line);
                    return new Instruction(Opcode.bo, new dynamic[]
                    {
                        BinaryOperationArgument.MovValue(destination, t)
                    }.ToImmutableArray());
                }
                var v = ParseInteger(ref line);
                if (line.Length == 0 || line[0] != 'n')
                {
                    if (v != 0)
                        throw new ParseExceptionInternal("Invalid mov source specification");
                    
                    return new Instruction(Opcode.bo, new dynamic[]
                    {
                        BinaryOperationArgument.MovZero(destination)
                    }.ToImmutableArray());
                }
                ExpectFixedFeed(ref line, 'n');

                var num = NumberSpec.FromConstant((int)v);
                return new Instruction(Opcode.bo, new dynamic[]
                {
                    BinaryOperationArgument.MovValue(destination, num)
                }.ToImmutableArray());
            }

            var type = opcodeName switch
            {
                "add" => BinaryOperationArgument.Operation.Add,
                "sub" => BinaryOperationArgument.Operation.Subtract,
                "mul" => BinaryOperationArgument.Operation.Multiply,
                "div" => BinaryOperationArgument.Operation.Divide,
                "rem" => BinaryOperationArgument.Operation.Remainder,
                "and" => BinaryOperationArgument.Operation.BitwiseAnd,
                "or" => BinaryOperationArgument.Operation.BitwiseOr,
                "xor" => BinaryOperationArgument.Operation.BitwiseXor,
                "lsh" => BinaryOperationArgument.Operation.LeftShift,
                "rsh" => BinaryOperationArgument.Operation.RightShift,
                _ => throw new ArgumentException(nameof(opcodeName)),
            };
            
            FeedWhitespace(ref line);
            destination = ParseAddress(ref line);
            ExpectFixedFeed(ref line, ',');
            FeedWhitespace(ref line);
            FeedWhitespace(ref line);
            var arg1 = ParseNumber(ref line);
            ExpectFixedFeed(ref line, ',');
            FeedWhitespace(ref line);
            FeedWhitespace(ref line);
            var arg2 = ParseNumber(ref line);
            FeedWhitespace(ref line);

            if (arg1.Address == destination)
                return new Instruction(Opcode.bo, new dynamic[]
                {
                    new BinaryOperationArgument(type, destination, arg2),
                }.ToImmutableArray());
            
            return new Instruction(Opcode.bo, new dynamic[]
            {
                new BinaryOperationArgument(type, destination, arg1, arg2),
            }.ToImmutableArray());
            
        }

        private Instruction ParseConditionalJump(ref ReadOnlySpan<char> line, string opcodeName)
        {
            var jumpCondition = opcodeName switch
            {
                "jeq" => JumpCondition.Equal,
                "jneq" => JumpCondition.NotEqual,
                "jge" => JumpCondition.GreaterOrEqual,
                "jg" => JumpCondition.Greater,
                "jle" => JumpCondition.LessOrEqual,
                "jl" => JumpCondition.Less,
                "janz" => JumpCondition.BitwiseAndNotZero,
                "jaz" => JumpCondition.BitwiseAndZero,
                _ => throw new ArgumentOutOfRangeException(),
            };
            var jumpType = jumpCondition switch
            {
                JumpCondition.Equal => 0,
                JumpCondition.NotEqual => 1,
                JumpCondition.GreaterOrEqual => 2,
                JumpCondition.Greater => 3,
                JumpCondition.LessOrEqual => 4,
                JumpCondition.Less => 5,
                JumpCondition.BitwiseAndNotZero => 6,
                JumpCondition.BitwiseAndZero => 0x80 | 6,
                _ => throw new ArgumentOutOfRangeException()
            };

            FeedWhitespace(ref line);
            var ar1 = ParseNumber(ref line);
            FeedWhitespace(ref line);
            ExpectFixedFeed(ref line, ',');
            FeedWhitespace(ref line);
            var ar2 = ParseNumber(ref line);
            FeedWhitespace(ref line);
            ExpectFixedFeed(ref line, ',');
            FeedWhitespace(ref line);
            var addr = ParseJumpOffset(ref line);
            
            return new Instruction(Opcode.jc, new dynamic[]
            {
                jumpType,
                ar1, ar2, addr
            }.ToImmutableArray());
        }

        private ImmutableDictionary<string, int> ProcessLabels()
        {
            var res = new Dictionary<string, int>();
            foreach (var p in _labelIds)
            {
                if (_labelIdToInstructionNumber.TryGetValue(p.Value, out var iNumber))
                    res.Add(p.Key, iNumber);
                else
                    throw new ParseExceptionInternal($"Label {p.Value} referenced, but not defined");
            }

            return res.ToImmutableDictionary();
        }

        /* this is not so efficient, as assembling can be done without reading all the file into memory (in 2 passes)
           but this is easier to implement =) */
        public (ImmutableArray<Instruction> instructions, ImmutableDictionary<string, int> labels) ReadAll()
        {
            try
            {
                var instructions = new List<Instruction>();
                while (true)
                {
                    var i = ReadOneInstruction();
                    if (i == null)
                        break;
                    instructions.Add(i.Value);
                }

                var finalLabels = ProcessLabels();

                for (var i = 0; i < instructions.Count; i++)
                {
                    foreach (var (j, element) in instructions[i].Encoding
                        .Select((x, j) => (j, x)))
                    {
                        if (element == OpcodeEncodingElement.JumpOffset)
                        {
                            instructions[i] = instructions[i].ChangeData(instructions[i].Data.SetItem(j, 
                                _labelIdToInstructionNumber[instructions[i].Data[j]]));
                        }
                        else if (element == OpcodeEncodingElement.JumpOffsetArray)
                        {
                            ImmutableArray<int> addresses = instructions[i].Data[j];
                            addresses = addresses.Select(_ => _labelIdToInstructionNumber[_]).ToImmutableArray();
                            instructions[i] = instructions[i].ChangeData(instructions[i].Data.SetItem(j,
                                addresses));
                        }
                    }
                }

                return (instructions.ToImmutableArray(), finalLabels);
            }
            catch (ParseExceptionInternal e)
            {
                Console.WriteLine(e);
                throw new ParseException($"Parse exception occured at line {_lineNumber}", e);
            }
        }

        private class ParseExceptionInternal : Exception
        {
            public ParseExceptionInternal()
            {
            }

            protected ParseExceptionInternal(SerializationInfo? info, StreamingContext context) : base(info, context)
            {
            }

            public ParseExceptionInternal(string? message) : base(message)
            {
            }

            public ParseExceptionInternal(string? message, Exception? innerException) : base(message, innerException)
            {
            }
        }

        public class ParseException : Exception
        {
            public ParseException()
            {
            }

            protected ParseException(SerializationInfo? info, StreamingContext context) : base(info, context)
            {
            }

            public ParseException(string? message) : base(message)
            {
            }

            public ParseException(string? message, Exception? innerException) : base(message, innerException)
            {
            }
        }
    }
}