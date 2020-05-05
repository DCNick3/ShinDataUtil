using System.Diagnostics;
using ShinDataUtil.Common.Scenario;

namespace ShinDataUtil.Scenario
{
    public class Opcode65
    {
        public Opcode65(byte type, ushort destinationAddress, NumberSpec argument2)
        {
            type = (byte) (type & 0x7f);
            Trace.Assert(type <= 11);
            Type = (Operation) type;
            DestinationAddress = destinationAddress;
            Argument1 = NumberSpec.FromAddress(destinationAddress);
            Argument2 = argument2;
        }

        public Opcode65(byte type, ushort destinationAddress, NumberSpec argument1, NumberSpec argument2)
        {
            type = (byte) (type & 0x7f);
            Trace.Assert(type <= 11);
            Type = (Operation) type;
            DestinationAddress = destinationAddress;
            Argument1 = argument1;
            Argument2 = argument2;
        }

        public Opcode65(Operation type, ushort destinationAddress, NumberSpec argument2)
        {
            Type = type;
            DestinationAddress = destinationAddress;
            Argument1 = NumberSpec.FromAddress(destinationAddress);
            Argument2 = argument2;
        }

        public Opcode65(Operation type, ushort destinationAddress, NumberSpec argument1, NumberSpec argument2)
        {
            Type = type;
            DestinationAddress = destinationAddress;
            Argument1 = argument1;
            Argument2 = argument2;
        }
        
        public static Opcode65 MovZero(ushort address)
        {
            return new Opcode65((byte)Operation.Zero, address, NumberSpec.FromConstant(0));
        }

        public static Opcode65 MovValue(ushort address, NumberSpec number)
        {
            return new Opcode65((byte)Operation.Argument2, address, number);
        }

        public bool ShouldHaveFirstArgumentSeparatelyEncoded => Argument1.Address != DestinationAddress;
        public Operation Type { get; }
        public NumberSpec Argument1 { get; }
        public NumberSpec Argument2 { get; }
        public ushort DestinationAddress { get; }

        public enum Operation : byte
        {
            Argument2 = 0,
            Zero = 1,
            Add = 2,
            Subtract = 3,
            Multiply = 4,
            Divide = 5,
            Remainder = 6,
            BitwiseAnd = 7,
            BitwiseOr = 8,
            BitwiseXor = 9,
            LeftShift = 10,
            RightShift = 11
            /* others are a pain to figure out */
        }
    }
}