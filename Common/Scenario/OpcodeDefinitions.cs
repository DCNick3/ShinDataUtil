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
                "uo" => OpcodeEncodingElement.UnaryOperationArgument,
                "bo" => OpcodeEncodingElement.BinaryOperationArgument,
                _ => throw new ArgumentException()
            };
        }

        private static string? GetOpcodeEncodingString(Opcode opcode)
        {
            return opcode switch
            {
                Opcode.EXIT => "n!",

                Opcode.uo => "uo",
                Opcode.bo => "bo",
                Opcode.exp => "a,rpn",
                Opcode.jc => "b,n,n,j",
                Opcode.j => "j!",
                Opcode.call => "j",
                Opcode.ret => "!",
                Opcode.jt => "n,jj",
                Opcode.callt => "n,jj",
                Opcode.rnd => "a,n,n",
                Opcode.push => "nn",
                Opcode.pop => "aa",


                Opcode.OPCODE79 => "j,nn",
                Opcode.OPCODE80 => "!",

                Opcode.OPCODE83 => "a,n,n",

                Opcode.OPCODE128 => "s,n",
                Opcode.OPCODE129 => "n,n",
                Opcode.OPCODE130 => "b,n",
                Opcode.OPCODE131 => "n",
                Opcode.OPCODE132 => "i,lstr",
                Opcode.OPCODE133 => "n",
                Opcode.OPCODE134 => "",
                Opcode.OPCODE135 => "b",
                Opcode.OPCODE136 => "s,s,s,n,str,strstr",
                Opcode.OPCODE137 => "n,n,n,bmn",
                Opcode.OPCODE138 => "",
                Opcode.OPCODE144 => "n,n,n,n",
                Opcode.OPCODE145 => "n",
                Opcode.OPCODE146 => "n,n",
                Opcode.OPCODE149 => "n,n,n,n,n,n,n",
                Opcode.OPCODE150 => "n,n",
                Opcode.OPCODE151 => "n",
                Opcode.OPCODE152 => "n,n,n",
                Opcode.OPCODE154 => "n,n",
                Opcode.OPCODE155 => "n,n,n,n,n",
                Opcode.OPCODE156 => "str,n,n",
                Opcode.OPCODE158 => "n",
                Opcode.OPCODE195 => "n,n,bmn",
                Opcode.OPCODE193 => "n,n,n,bmn",
                Opcode.OPCODE194 => "n,n",
                Opcode.OPCODE196 => "n,nn",
                Opcode.OPCODE197 => "n",
                Opcode.OPCODE198 => "n,n",
                Opcode.OPCODE199 => "n,n",
                Opcode.OPCODE224 => "s,n,n,n,n",
                Opcode.OPCODE225 => "n,n",
                Opcode.DEBUGOUT => "str,nn",
                _ => null
            };
        }
        
        private static readonly HashSet<Opcode> NeedsStringFixup = new HashSet<Opcode>
        {
            //Opcode.MSGSET, Opcode.LOGSET, Opcode.SELECT, Opcode.SAVEINFO
        };

        private static readonly OpcodeEncodingElement[][] EncodingElements = new OpcodeEncodingElement[256][];
        private static readonly bool[] IsUnconditionalJumpMap = new bool[256];
        
        static OpcodeDefinitions()
        {
            for (var i = 0; i < 256; i++)
            {
                var val = GetOpcodeEncodingString((Opcode) i);
                if (val == null) continue;
                
                var alwaysJump = val.EndsWith("!");
                if (alwaysJump)
                    val = val[..^1];
                var elements = val.Length == 0 
                    ? new OpcodeEncodingElement[0] 
                    : val.Split(',').Select(GetElement).ToArray();
                EncodingElements[i] = elements;
                IsUnconditionalJumpMap[i] = alwaysJump;
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
                (false, 7) => JumpCondition.BitSet,
                (true, 7) => JumpCondition.BitZero,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static bool DoesNeedStringsFixup(Opcode opcode) => NeedsStringFixup.Contains(opcode);
    }
}