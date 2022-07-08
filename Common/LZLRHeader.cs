using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShinDataUtil.Common
{
    public struct LZLRHeader
    {
        public uint magic;
        public int unpackedSize;
        public int dataOffset;

        public static int Size => sizeof(int)*3;
    }
}
