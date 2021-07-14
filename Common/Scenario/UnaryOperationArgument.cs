using System;
using System.Diagnostics;
using ShinDataUtil.Common.Scenario;

namespace ShinDataUtil.Scenario
{
    public class UnaryOperationArgument
    {
        public UnaryOperationArgument(byte type, ushort destinationAddress)
        {
            type = (byte) (type & 0x7f);
            Trace.Assert(Enum.IsDefined(typeof(Operation), type));
            Type = (Operation) type;
            DestinationAddress = destinationAddress;
            Argument = NumberSpec.FromAddress(destinationAddress);
        }

        public UnaryOperationArgument(byte type, ushort destinationAddress, NumberSpec argument)
        {
            type = (byte) (type & 0x7f);
            Trace.Assert(Enum.IsDefined(typeof(Operation), type));
            Type = (Operation) type;
            DestinationAddress = destinationAddress;
            Argument = argument;
        }

        public UnaryOperationArgument(Operation type, ushort destinationAddress, NumberSpec argument)
        {
            Trace.Assert(Enum.IsDefined(typeof(Operation), type));
            Type = type;
            DestinationAddress = destinationAddress;
            Argument = argument;   
        }

        public bool ShouldHaveArgumentSeparatelyEncoded => !Argument.IsConstant || Argument.Address != DestinationAddress;
        public Operation Type { get; }
        public NumberSpec Argument { get; }
        public ushort DestinationAddress { get; }

        public enum Operation : byte
        {
            Negate = 2,
            Abs = 3,
            Sin = 4,
            Cos = 5,
            Tan = 6,
            ASin = 7,
            ACos = 8,
            ATan = 9,
            Popcnt = 10,
            Tzcnt = 11, // count trailing zero bits
            /* others are a pain to figure out */
        }
    }
}