using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using ShinDataUtil.Common;
using System.Runtime.InteropServices;

namespace ShinDataUtil.Compression
{
    class ShinLZLRCompressor
    {
        private MemoryStream mapStream = new MemoryStream();
        private MemoryStream dataStream = new MemoryStream();

        private uint currentMapBlock = 0;
        private int currentBit = 0;
        public void PushBit(bool bit)
        {
            if (bit)
            {
                currentMapBlock |= 1u << currentBit;
            }
            currentBit++;

            if (currentBit == 32)
            {
                mapStream.WriteByte((byte)currentMapBlock);
                mapStream.WriteByte((byte)(currentMapBlock >> 8));
                mapStream.WriteByte((byte)(currentMapBlock >> 16));
                mapStream.WriteByte((byte)(currentMapBlock >> 24));
                currentMapBlock = 0;
                currentBit = 0;
            }
        }

        public void PushBlock(uint block)
        {
            dataStream.WriteByte((byte)block);
            dataStream.WriteByte((byte)(block >> 8));
            dataStream.WriteByte((byte)(block >> 16));
            dataStream.WriteByte((byte)(block >> 24));
        }
        public ReadOnlySpan<byte> Compress(ReadOnlySpan<byte> data)
        {
            // Currently I dont know if there are LZLR-compressed files that not 4-byte aligned
            // This compression probably used only for textures
            Trace.Assert(data.Length % 4 == 0);

            mapStream = new MemoryStream();
            dataStream = new MemoryStream();

            var header = new LZLRHeader();
            header.magic = LZLRHeader.DefaultMagic;
            header.unpackedSize = data.Length; 

            var intData = MemoryMarshal.Cast<byte, uint>(data);

            // WIP
            // Direct copying, compression effectiveness below than zero, but it works 
            for (int i = 0; i < intData.Length; i++)
            {
                PushBit(false);
                PushBit(false);
                PushBit(false);

                PushBlock(intData[i]);
            }

            header.dataOffset = LZLRHeader.Size + (int)mapStream.Position;

            var outStream = new MemoryStream();

            outStream.Write(SpanUtil.AsReadOnlyBytes(ref header));
            outStream.Write(mapStream.ToArray());
            outStream.Write(dataStream.ToArray());

            return outStream.ToArray();
        }
    }
}
