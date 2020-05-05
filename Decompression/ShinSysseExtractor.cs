using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ShinDataUtil.Decompression
{
    public class ShinSysseExtractor
    {
        private static readonly (int, int)[] AudioDecodeTable = new[]
        {
            (0,   0),
            (60,  0),
            (115, -52),
            (98,  -55),
            (122, -60)
        };

        private static void DecodeChunk(ReadOnlySpan<byte> chunkData, Span<short> destination, 
            ref int stateOld, ref int stateNew)
        {
            var pieceHeader = chunkData[0];
            var pieceData = chunkData[1..16];

            var tableIndex = pieceHeader >> 4;
            var sampleBitOffset = pieceHeader & 0xf;

            var (tableValue1, tableValue2) = AudioDecodeTable[tableIndex];

            for (var i = 0; i < 30; i++)
            {
                var sampleData = i % 2 == 0 ? pieceData[i / 2] & 0xf : pieceData[i / 2] >> 4;

                if (sampleData >= 8)
                    sampleData = (int)(sampleData | 0xfffffff0U);

                var additional = stateNew * tableValue1 + stateOld * tableValue2;
                // Mmm, I love magic numbers
                if (additional + 32 >= 0)
                    additional += 32;
                else
                    additional += 95;

                sampleData = (sampleData << sampleBitOffset) + (additional >> 6);

                if (sampleData < short.MinValue)
                    sampleData = short.MinValue;
                if (sampleData > short.MaxValue)
                    sampleData = short.MaxValue;

                destination[0] = (short)sampleData;
                destination = destination.Slice(1);
                
                stateOld = stateNew;
                stateNew = sampleData;
            }
        }
        
        private static unsafe long DecodeEntry(ReadOnlySpan<byte> data, Stream outStream)
        {
            var header = MemoryMarshal.Read<EntryHeader>(data);
            
            Trace.Assert(header.magic == 827343937);
            Trace.Assert(header.file_size == data.Length);
            Trace.Assert(header.channel_count == 1 || header.channel_count == 2);
            if (header.channel_count == 1)
                Trace.Assert(header.file_size % 16 == 0);
            else
                Trace.Assert((header.file_size - 16) % 32 == 0);
            
            data = data.Slice(sizeof(EntryHeader));
            
            const int wavHeaderSize = 44;
            
            var bw = new BinaryWriter(outStream);

            var outDataSize = 2 * header.sample_count * header.channel_count;
            
            // as per http://www.topherlee.com/software/pcm-tut-wavformat.html
            bw.Write(1179011410U); // RIFF signature
            bw.Write(wavHeaderSize - 8 + outDataSize); // RIFF file size
            bw.Write(1163280727U); // WAVE signature
            
            bw.Write(544501094U); // fmt chunk header
            bw.Write(16); // length of fmt chunk
            bw.Write((ushort)1); // PCM
            bw.Write(header.channel_count); // channel count
            bw.Write((int)header.sample_rate); // sample rate
            bw.Write(2 * header.sample_rate * header.channel_count); // bytes per second
            bw.Write(header.channel_count);
            bw.Write((ushort)16); // bits per sample
            
            bw.Write(1635017060U); // data chunk header
            bw.Write(outDataSize); // data size

            if (header.channel_count == 1)
            {
                var stateNew = 0;
                var stateOld = 0;
                var outBuffer = new short[30];
                while (data.Length > 0)
                {
                    DecodeChunk(data[..16], outBuffer, ref stateOld, ref stateNew);
                    data = data[16..];
                    foreach (var t in outBuffer)
                        bw.Write(t);
                }
            }
            else
            {
                var stateNewL = 0;
                var stateOldL = 0;
                var stateNewR = 0;
                var stateOldR = 0;
                var outBufferL = new short[30];
                var outBufferR = new short[30];
                while (data.Length > 0)
                {
                    DecodeChunk(data[..16], outBufferL, ref stateOldL, ref stateNewL);
                    data = data[16..];
                    DecodeChunk(data[..16], outBufferR, ref stateOldR, ref stateNewR);
                    data = data[16..];
                    foreach (var (l, r) in outBufferL.Zip(outBufferR))
                    {
                        bw.Write(l);
                        bw.Write(r);
                    }
                }
            }

            return outDataSize + wavHeaderSize;
        }
        
        public static unsafe long Extract(ReadOnlySpan<byte> data, string destinationDirectory)
        {
            var header = MemoryMarshal.Read<Header>(data);

            Trace.Assert(header.magic == 1163090259);
            Trace.Assert(header.file_size == data.Length);

            var n = checked((int)header.entry_count);

            var entries = MemoryMarshal.Cast<byte, Entry>(data.Slice(sizeof(Header), n * sizeof(Entry)));

            long writtenBytes = 0;
            
            foreach (var entry in entries)
            {
                using var f = File.Open(Path.Combine(destinationDirectory, entry.Name + ".wav"), 
                    FileMode.Create, FileAccess.Write);
                checked
                {
                    var entryData = data.Slice((int) entry.offset, (int) entry.size);
                    //f.Write(entryData);
                    writtenBytes += DecodeEntry(entryData, f);
                }
            }
            
            return writtenBytes;
        }

    }

    public struct Header
    {
#pragma warning disable 649
        // ReSharper disable InconsistentNaming
        // ReSharper disable UnassignedField.Global
        public uint magic;
        public uint file_size;
        public uint entry_count;
#pragma warning restore 649
        // ReSharper restore InconsistentNaming
        // ReSharper restore UnassignedField.Global
    }

    public unsafe struct Entry
    {
#pragma warning disable 649
        // ReSharper disable InconsistentNaming
        // ReSharper disable UnassignedField.Global
        public fixed byte name[16];
        public uint offset;
        public uint size;
#pragma warning restore 649
        // ReSharper restore InconsistentNaming
        // ReSharper restore UnassignedField.Global

        public string Name
        {
            get
            {
                var sz = 0;
                while (name[sz] != 0)
                {
                    sz++;
                    Trace.Assert(sz < 16);
                }

                fixed (byte* nameData = name)
                    return Encoding.UTF8.GetString(nameData, sz);
            }
        }
    }

    public struct EntryHeader
    {
#pragma warning disable 649
        // ReSharper disable InconsistentNaming
        // ReSharper disable UnassignedField.Global
        public uint magic;
        public uint file_size;
        public ushort channel_count;
        public ushort sample_rate;
        public uint sample_count;
#pragma warning restore 649
        // ReSharper restore InconsistentNaming
        // ReSharper restore UnassignedField.Global
    }
}