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


                Opcode.callex => "j,nn",
                Opcode.retex => "!",

                Opcode.getheadprop => "a,n,n",

                Opcode.SGET => "a,n",
                Opcode.SSET => "n,n",
                Opcode.WAIT => "b,n",
                Opcode.MSGINIT => "n",
                Opcode.MSGSET => "i,lstr",
                Opcode.MSGWAIT => "n",
                Opcode.MSGSIGNAL => "",
                Opcode.MSGCLOSE => "b",
                Opcode.SELECT => "s,s,a,n,str,strstr",
                Opcode.WIPE => "n,n,n,bmn",
                Opcode.WIPEWAIT => "",
                Opcode.BGMPLAY => "n,n,n,n",
                Opcode.BGMSTOP => "n",
                Opcode.BGMVOL => "n,n",
                Opcode.SEPLAY => "n,n,n,n,n,n,n",
                Opcode.SESTOP => "n,n",
                Opcode.SESTOPALL => "n",
                Opcode.SEVOL => "n,n,n",
                Opcode.SEWAIT => "n,n",
                Opcode.SEONCE => "n,n,n,n,n",
                Opcode.VOICEPLAY => "str,n,n",
                Opcode.VOICEWAIT => "n",
                Opcode.LAYERCTRL => "n,n,bmn",
                Opcode.LAYERLOAD => "n,n,n,bmn",
                Opcode.LAYERUNLOAD => "n,n",
                Opcode.LAYERWAIT => "n,nn",
                Opcode.LAYERBACK => "n",
                Opcode.LAYERSELECT => "n,n",
                Opcode.MOVIEWAIT => "n,n",
                Opcode.SLEEP => "s,n,n,n,n",
                Opcode.VSET => "n,n",
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