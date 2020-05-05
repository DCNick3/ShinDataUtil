using System;

namespace ShinDataUtil.Common.Scenario
{
    public struct NumberSpec
    {
        private NumberSpec(bool isConstant, int? value, short? address)
        {
            IsConstant = isConstant;
            Value = value;
            Address = address;
        }

        public static NumberSpec FromConstant(int value) => new NumberSpec(true, value, null);

        public static NumberSpec FromAddress(ushort address) =>
            address < 0x1000 ? FromMem1Address(address) : FromMem3Address(address - 0xfff);
        public static NumberSpec FromMem1Address(int value) => new NumberSpec(false, null, (short)value);
        public static NumberSpec FromMem3Address(int value) => throw new NotSupportedException();
        public bool IsConstant { get; }
        public int? Value { get; }
        public short? Address { get; }

        public override string ToString()
        {
            if (IsConstant)
                return $"{Value}";
            return $"@0x{Address:x4}";
        }
    }
}