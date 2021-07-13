namespace ShinDataUtil.Scenario
{
    public enum OpcodeEncodingElement
    {
        Byte,
        Short,
        Address,
        Int,
        JumpOffset,
        NumberArgument,
        
        AddressArray,
        NumberArray,
        JumpOffsetArray,
        String,
        LongString,
        StringArray,
        BitmappedNumberArguments,
        PostfixNotationExpression,
        
        MessageId,

        UnaryOperationArgument, /* It's very odd anyway */
        BinaryOperationArgument /* It's very odd anyway */
    }
}