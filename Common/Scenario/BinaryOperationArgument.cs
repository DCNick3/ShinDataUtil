using System;
using System.Diagnostics;
using ShinDataUtil.Common.Scenario;

namespace ShinDataUtil.Scenario
{
    public class BinaryOperationArgument
    {
        public BinaryOperationArgument(byte type, ushort destinationAddress, NumberSpec argument2)
        {
            type = (byte) (type & 0x7f);
            Trace.Assert(Enum.IsDefined(typeof(Operation), type));
            Type = (Operation) type;
            DestinationAddress = destinationAddress;
            Argument1 = NumberSpec.FromAddress(destinationAddress);
            Argument2 = argument2;
        }

        public BinaryOperationArgument(byte type, ushort destinationAddress, NumberSpec argument1, NumberSpec argument2)
        {
            type = (byte) (type & 0x7f);
            Trace.Assert(Enum.IsDefined(typeof(Operation), type));
            Type = (Operation) type;
            DestinationAddress = destinationAddress;
            Argument1 = argument1;
            Argument2 = argument2;
        }

        public BinaryOperationArgument(Operation type, ushort destinationAddress, NumberSpec argument2)
        {
            Type = type;
            DestinationAddress = destinationAddress;
            Argument1 = NumberSpec.FromAddress(destinationAddress);
            Argument2 = argument2;
        }

        public BinaryOperationArgument(Operation type, ushort destinationAddress, NumberSpec argument1, NumberSpec argument2)
        {
            Type = type;
            DestinationAddress = destinationAddress;
            Argument1 = argument1;
            Argument2 = argument2;
        }
        
        public static BinaryOperationArgument MovZero(ushort address)
        {
            return new BinaryOperationArgument((byte)Operation.Argument2, address, NumberSpec.FromConstant(0));
        }

        public static BinaryOperationArgument MovValue(ushort address, NumberSpec number)
        {
            return new BinaryOperationArgument((byte)Operation.Argument2, address, number);
        }

        public bool ShouldHaveFirstArgumentSeparatelyEncoded => Argument1.IsConstant || Argument1.Address != DestinationAddress;
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
            RightShift = 11,
            
            SetBit = 15,
            ResetBit = 16,
            
            TzcntUpper = 17 // Reset lower Argument2 bits and do tzcnt (WTF, man, why would you do this?)
            /* others are a pain to figure out */
        }
    }
}