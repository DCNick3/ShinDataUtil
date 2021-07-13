using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ShinDataUtil.Common.Scenario;

namespace ShinDataUtil.Scenario
{
    public static class OpcodeDefinitions
    {
        private static OpcodeEncodingElement GetElement(string s)
        {
            return s switch
            {
                "b" => OpcodeEncodingElement.Byte,
                "s" => OpcodeEncodingElement.Short,
                "a" => OpcodeEncodingElement.Address,
                "i" => OpcodeEncodingElement.Int,
                "j" => OpcodeEncodingElement.JumpOffset,
                "n" => OpcodeEncodingElement.NumberArgument,
                "aa" => OpcodeEncodingElement.AddressArray,
                "nn" => OpcodeEncodingElement.NumberArray,
                "jj" => OpcodeEncodingElement.JumpOffsetArray,
                "str" => OpcodeEncodingElement.String,
                "lstr" => OpcodeEncodingElement.LongString,
                "strstr" => OpcodeEncodingElement.StringArray,
                "bmn" => OpcodeEncodingElement.BitmappedNumberArguments,
                "rpn" => OpcodeEncodingElement.PostfixNotationExpression, 
                "mi" => OpcodeEncodingElement.MessageId,
                "bo" => OpcodeEncodingElement.BinaryOperationArgument,
                _ => throw new ArgumentException()
            };
        }

        private const string Encodings = @"
EXIT=n!

bo=bo
exp=a,rpn
jc=b,n,n,j
j=j!
call=j
ret=!
jt=n,jj
callt=n,jj
rnd=a,n,n
push=nn
pop=aa


OPCODE79=j,nn
OPCODE80=!

OPCODE83=a,n,n

OPCODE128=s,n
OPCODE129=n,n
OPCODE130=b,n
OPCODE131=n
OPCODE132=i,lstr
OPCODE133=n
OPCODE134=
OPCODE135=b
OPCODE136=s,s,s,n,str,strstr
OPCODE137=n,n,n,bmn
OPCODE138=
OPCODE144=n,n,n,n
OPCODE145=n
OPCODE146=n,n
OPCODE149=n,n,n,n,n,n,n
OPCODE150=n,n
OPCODE151=n
OPCODE152=n,n,n
OPCODE154=n,n
OPCODE155=n,n,n,n,n
OPCODE156=str,n,n
OPCODE158=n
OPCODE195=n,n,bmn
OPCODE193=n,n,n,bmn
OPCODE194=n,n
OPCODE196=n,nn
OPCODE197=n
OPCODE198=n,n
OPCODE199=n,n
OPCODE224=s,n,n,n,n
OPCODE225=n,n
DEBUGOUT=str,nn";
        
        private static readonly HashSet<Opcode> NeedsStringFixup = new HashSet<Opcode>
        {
            //Opcode.MSGSET, Opcode.LOGSET, Opcode.SELECT, Opcode.SAVEINFO
        };

        private static readonly OpcodeEncodingElement[][] EncodingElements = new OpcodeEncodingElement[256][];
        private static readonly bool[] IsUnconditionalJumpMap = new bool[256];
        
        static OpcodeDefinitions()
        {
            foreach (var entry in Encodings.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('=');
                Trace.Assert(parts.Length == 2);
                Trace.Assert(Enum.TryParse<Opcode>(parts[0], out var opcode));
                var alwaysJump = parts[1].EndsWith("!");
                if (alwaysJump)
                    parts[1] = parts[1][..^1];
                var elements = parts[1].Length == 0 
                    ? new OpcodeEncodingElement[0] 
                    : parts[1].Split(',').Select(GetElement).ToArray();
                EncodingElements[(int)opcode] = elements;
                IsUnconditionalJumpMap[(int)opcode] = alwaysJump;
            }
        }
        
        public static OpcodeEncodingElement[] GetEncoding(Opcode opcode)
        {
            return EncodingElements[(int) opcode] ?? throw new NotImplementedException();
        }

        public static bool IsJump(Opcode opcode) => IsUnconditionalJumpMap[(int) opcode] || GetEncoding(opcode).Any(_ =>
            _ == OpcodeEncodingElement.JumpOffset || _ == OpcodeEncodingElement.JumpOffsetArray);

        public static bool IsUnconditionalJump(Opcode opcode) => IsUnconditionalJumpMap[(int) opcode];

        public static JumpCondition DecodeJumpCondition(Instruction instruction)
        {
            Trace.Assert(instruction.Opcode == Opcode.jc);
            var type = (byte) instruction.Data[0];
            return ((type & 0x80) != 0, type & 0x7f) switch
            {
                (false, 0) => JumpCondition.Equal,
                (true, 1) => JumpCondition.Equal,
                (false, 1) => JumpCondition.NotEqual,
                (true, 0) => JumpCondition.NotEqual,
                (false, 2) => JumpCondition.GreaterOrEqual,
                (false, 3) => JumpCondition.Greater,
                (false, 4) => JumpCondition.LessOrEqual,
                (false, 5) => JumpCondition.Less,
                (false, 6) => JumpCondition.BitwiseAndNotZero, /* Jump if bitwise And is Not Zero */
                (true, 6) => JumpCondition.BitwiseAndZero, /* Jump if bitwise And is Zero */
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static bool DoesNeedStringsFixup(Opcode opcode) => NeedsStringFixup.Contains(opcode);
    }
}