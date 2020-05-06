using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ShinDataUtil.Scenario;

namespace ShinDataUtil.Common.Scenario
{
    public struct Instruction
    {
        public Opcode Opcode { get; }
        public ImmutableArray<dynamic> Data { get; }
        
        public Instruction(Opcode opcode, ImmutableArray<dynamic> data)
        {
            Opcode = opcode;
            Data = data;
        }

        public IEnumerable<int> CodeXRefOut
        {
            get
            {
                foreach (var (i, x) in Encoding.Select((x, i) => (i, x)))
                    if (x == OpcodeEncodingElement.JumpOffset)
                        yield return Data[i];
                    else if (x == OpcodeEncodingElement.JumpOffsetArray)
                        foreach (int offset in Data[i])
                            yield return offset;
            }
        }
        public bool IsJump => OpcodeDefinitions.IsJump(Opcode);
        public bool CanFallThrough => !OpcodeDefinitions.IsUnconditionalJump(Opcode);

        public IReadOnlyList<OpcodeEncodingElement> Encoding => OpcodeDefinitions.GetEncoding(Opcode);

        public IEnumerable<DataXRef> DataXRefOut
        {
            get
            {
                static IEnumerable<DataXRef> HandleNumber(NumberSpec n)
                {
                    var address = n.Address;
                    if (address != null)
                        yield return new DataXRef(DataXRefType.Read, address.Value);
                }
                foreach (var (i, x) in Encoding.Select((x, i) => (i, x)))
                {
                    switch (x)
                    {
                        case OpcodeEncodingElement.Address:
                            yield return new DataXRef(DataXRefType.Write, Data[i]);
                            break;
                        case OpcodeEncodingElement.AddressArray:
                        {
                            foreach (short s in Data[i])
                                yield return new DataXRef(DataXRefType.Write, s);
                            break;
                        }
                        case OpcodeEncodingElement.NumberArgument:
                        {
                            foreach (var xRef in HandleNumber(Data[i]))
                                yield return xRef;
                            break;
                        }
                        case OpcodeEncodingElement.NumberArray:
                        {
                            foreach (NumberSpec n in Data[i])
                            foreach (var xRef in HandleNumber(n))
                                yield return xRef;
                            break;
                        }
                        case OpcodeEncodingElement.BinaryOperationArgument:
                        {
                            var data = (BinaryOperationArgument) Data[i];
                            foreach (var xRef in HandleNumber(data.Argument1))
                                yield return xRef;
                            foreach (var xRef in HandleNumber(data.Argument2))
                                yield return xRef;
                            yield return new DataXRef(DataXRefType.Write, data.DestinationAddress);
                            break;
                        }
                        case OpcodeEncodingElement.PostfixNotationExpression:
                        {
                            PostfixExpression data = Data[i];
                            foreach (var element in data.Elements)
                                if (element.NumberSpec != null)
                                    foreach (var xRef in HandleNumber(element.NumberSpec.Value))
                                        yield return xRef;
                            break;
                        }
                        case OpcodeEncodingElement.BitmappedNumberArguments:
                        {
                            foreach (var xRef in ((ImmutableArray<NumberSpec>)Data[i]).SelectMany(HandleNumber))
                                yield return xRef;
                            break;
                        }
                    }
                }
            }
        }

        public IEnumerable<int> DataXRefOutRead => DataXRefOut
            .Where(_ => _.Type == DataXRefType.Read)
            .Select(_ => _.Address);

        public IEnumerable<int> DataXRefOutWrite => DataXRefOut
            .Where(_ => _.Type == DataXRefType.Write)
            .Select(_ => _.Address);

        public JumpCondition JumpCondition => OpcodeDefinitions.DecodeJumpCondition(this);

        public Instruction ChangeData(ImmutableArray<dynamic> data) => new Instruction(Opcode, data);
    }
}