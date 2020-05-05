using ShinDataUtil.Scenario;

namespace ShinDataUtil.Common.Scenario
{
    public struct DataXRef
    {
        public DataXRef(DataXRefType type, int address)
        {
            Type = type;
            Address = address;
        }
        
        public DataXRefType Type { get; }
        public int Address { get; }
    }
}