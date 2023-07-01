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
                "uo" => OpcodeEncodingElement.UnaryOperationArgument,
                _ => throw new ArgumentException()
            };
        }

        private const string Encodings = @"
EXIT=b,n!
uo=uo
bo=bo
exp=a,rpn
jc=b,n,n,j
j=j!
gosub=j
retsub=!
jt=n,jj
rnd=a,n,n
push=nn
pop=aa
call=j,nn
return=!

SGET=a,n
SSET=n,n
WAIT=b,n
MSGINIT=n,n,n
MSGSET=mi,b,n,lstr
MSGWAIT=n
MSGSIGNAL=
MSGSYNC=n,n
MSGCLOSE=b
MSGFACE=
LOGSET=lstr
SELECT=s,s,a,n,str,strstr
WIPE=n,n,n,bmn
WIPEWAIT=
BGMPLAY=n,n,n,n
BGMSTOP=n
BGMVOL=n,n
BGMWAIT=n
BGMSYNC=n
SEPLAY=n,n,n,n,n,n,n
SESTOP=n,n
SESTOPALL=n
SEVOL=n,n,n
SEPAN=n,n,n
SEWAIT=n,n
SEONCE=n,n,n,n,n
VOICEPLAY=str,n,n
VOICEWAIT=n
SAVEINFO=n,str
AUTOSAVE=
EVBEGIN=n
EVEND=
TROPHY=n
LAYERLOAD=n,n,n,bmn
LAYERUNLOAD=n,n
LAYERCTRL=n,n,bmn
LAYERWAIT=n,nn
LAYERBACK=n
LAYERSELECT=n,n
MOVIEWAIT=n,n
TIPSGET=nn
CHARSELECT=s,a,n
OTSUGET=n
CHART=b,nn
SNRSEL=n
KAKERA=
KAKERAGET=n,nn
QUIZ=a,n,n,n
FAKESELECT=
UNLOCK=b,nn
KGET=s,n
KSET=n,n
DEBUGOUT=str,nn";
        
        private static readonly HashSet<Opcode> NeedsStringFixup = new HashSet<Opcode>
        {
            Opcode.MSGSET, Opcode.LOGSET, Opcode.SELECT, Opcode.SAVEINFO
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