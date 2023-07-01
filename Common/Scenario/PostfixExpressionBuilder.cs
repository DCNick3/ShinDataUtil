using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using ShinDataUtil.Common.Scenario;

namespace ShinDataUtil.Scenario
{
    public class PostfixExpressionBuilder
    {
        private List<PostfixExpression.Element> _elements = new List<PostfixExpression.Element>();

        public IReadOnlyList<PostfixExpression.Element> Elements => _elements;

        public void AddElement(PostfixExpression.Element element) => _elements.Add(element);
        public void AddConstant(NumberSpec number) => AddElement(new PostfixExpression.Element(number));
        public void AddOperation(int operationNumber) => AddOperation((PostfixExpression.Operation)operationNumber);
        
        public void AddOperation(PostfixExpression.Operation operation) =>
            AddElement(new PostfixExpression.Element(operation));

        public PostfixExpression Build() => new PostfixExpression(_elements);
    }

    public class PostfixExpression
    {
        public PostfixExpression(IEnumerable<Element> elements)
        {
            Elements = elements.ToImmutableArray();
        }
        
        public ImmutableArray<Element> Elements { get; }

        public struct Element
        {
            public readonly NumberSpec? NumberSpec;
            public readonly Operation Operation;

            public Element(Operation operation)
            {
                Trace.Assert((int)operation <= 26);
                this.Operation = operation;
                NumberSpec = null;
            }

            public Element(NumberSpec numberSpec)
            {
                Operation = Operation.Constant;
                NumberSpec = numberSpec;
            }
        }

        /* For logical operations truth value is -1 (weird, but ok) */
        public enum Operation : byte
        {
            Constant = 0,
            Add = 1,
            Subtract = 2,
            Multiply = 3,
            Divide = 4,
            Remainder = 5,
            LeftShift = 6,
            RightShift = 7,
            BitwiseAnd = 8,
            BitwiseOr = 9,
            BitwiseXor = 10,
            Negate = 11,
            BitwiseNot = 12,
            AbsoluteValue = 13,
            Equals = 14,
            NotEquals = 15,
            GreaterOrEqual = 16,
            Greater = 17,
            LessOrEqual = 18,
            Less = 19,
            EqualToZero = 20,
            NotEqualToZero = 21,
            BothNotEqualToZero = 22,
            AnyNotEqualToZero = 23,
            SelectTwo = 24,
            MultiplyReal = 25,
            DivideReal = 26,
            /* Do we really need more? */
        }
    }
}