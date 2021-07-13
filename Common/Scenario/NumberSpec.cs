using System;

namespace ShinDataUtil.Common.Scenario
{
    public struct NumberSpec
    {
        private NumberSpec(bool isConstant, bool isMem3, int? value, short? address)
        {
            IsConstant = isConstant;
            IsMem3 = isMem3;
            Value = value;
            Address = address;
        }

        public static NumberSpec FromConstant(int value) => new NumberSpec(true, false, value, null);

        public static NumberSpec FromAddress(ushort address) =>
            address < 0x1000 ? FromMem1Address(address) : FromMem3Address(address - 0xfff);
        public static NumberSpec FromMem1Address(int value) => new NumberSpec(false, false, null, (short)value);
        public static NumberSpec FromMem3Address(int value) => new NumberSpec(false, true, null, (short)value);
        public bool IsConstant { get; }
        public bool IsMem3 { get; }
        public int? Value { get; }
        public short? Address { get; }

        public override string ToString()
        {
            if (IsConstant)
                return $"{Value}";
            if (!IsMem3)
                return $"@0x{Address:x4}";
            return $"l@0x{Address:x4}";
        }
    }
}