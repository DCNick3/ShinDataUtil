using System.Diagnostics;
using ShinDataUtil.Common.Scenario;

namespace ShinDataUtil.Scenario
{
    public class UnaryOperationArgument
    {
        public UnaryOperationArgument(byte type, ushort destinationAddress)
        {
            type = (byte) (type & 0x7f);
            Trace.Assert(type <= 11);
            Type = (Operation) type;
            DestinationAddress = destinationAddress;
            Argument1 = NumberSpec.FromAddress(destinationAddress);
        }

        public UnaryOperationArgument(byte type, ushort destinationAddress, NumberSpec argument1)
        {
            type = (byte) (type & 0x7f);
            Trace.Assert(type <= 11);
            Type = (Operation) type;
            DestinationAddress = destinationAddress;
            Argument1 = argument1;
        }

        public UnaryOperationArgument(Operation type, ushort destinationAddress)
        {
            Type = type;
            DestinationAddress = destinationAddress;
            Argument1 = NumberSpec.FromAddress(destinationAddress);
        }

        public UnaryOperationArgument(Operation type, ushort destinationAddress, NumberSpec argument1)
        {
            Type = type;
            DestinationAddress = destinationAddress;
            Argument1 = argument1;
        }
        
        public bool ShouldHaveFirstArgumentSeparatelyEncoded => Argument1.Address != DestinationAddress;
        public Operation Type { get; }
        public NumberSpec Argument1 { get; }
        public ushort DestinationAddress { get; }

        public enum Operation : byte
        {
            Zero = 0,
            XorFFFF = 1,
            Negate = 2,
            Not = 3,
        }
    }
}