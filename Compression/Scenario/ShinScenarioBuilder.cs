using System;
using System.Buffers;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using ShinDataUtil.Scenario;

namespace ShinDataUtil.Compression.Scenario
{
    /// <summary>
    /// Build a whole scenario file by combining code and HeadInfo
    /// </summary>
    public static class ShinScenarioBuilder
    {
        private static readonly Common.ShiftJisEncoding ShiftJis = new Common.ShiftJisEncoding();
        
        private static void WriteSectionHeader(ScenarioSectionHeader header, BinaryWriter output)
        {
            output.Write(MemoryMarshal.Cast<ScenarioSectionHeader, byte>(
                    MemoryMarshal.CreateSpan(ref header, 1)
                ));
        }

        private static uint WriteGenericSection<T>(ImmutableArray<T> data, Action<T, BinaryWriter> writeOne,
            BinaryWriter output)
        {
            while (output.BaseStream.Position % 4 != 0)
                output.Write((byte)0);

            var startOffset = (int)output.BaseStream.Position;
            WriteSectionHeader(new ScenarioSectionHeader(), output);
            var dataStartOffset = (int) output.BaseStream.Position;

            foreach (var el in data) 
                writeOne(el, output);

            var endOffset = (int) output.BaseStream.Position;
            output.Seek(startOffset, SeekOrigin.Begin);
            WriteSectionHeader(new ScenarioSectionHeader
            {
                elementCount = (uint)data.Length,
                byteSize = checked((uint)(endOffset - dataStartOffset) + 4)
            }, output);

            output.Seek(endOffset, SeekOrigin.Begin);
            
            return (uint)startOffset;
        }

        private static uint WriteSimplerSection<T>(ImmutableArray<T> data, Action<T, BinaryWriter> writeOne,
            BinaryWriter output)
        {
            while (output.BaseStream.Position % 4 != 0)
                output.Write((byte)0);
            
            var startOffset = (int)output.BaseStream.Position;
            output.Write((uint)data.Length);
            
            foreach (var el in data) 
                writeOne(el, output);
            
            return (uint) startOffset;
        }

        private static void WriteString(string str, BinaryWriter output)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(ShiftJis.GetMaxByteCount(str.Length) + 1);
            try
            {   
                var used = ShiftJis.GetBytes(str, buffer);
                buffer[used] = 0;
                output.Write((byte) (used + 1));
                output.Write(buffer[..(used + 1)]);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        
        private static (uint, uint, uint, uint, uint, uint, uint, uint, uint, uint, uint) WriteHeaderInfo(ScenarioHeadInfo info, Stream output)
        {
            using var bw = new BinaryWriter(output, ShiftJis, true);

            var offset36 = WriteGenericSection(info.Section36, WriteString, bw);
            var offset40 = WriteGenericSection(info.Section40, (s, bw1) =>
            {
                WriteString(s.Item1, bw1);
                bw1.Write(s.Item2);
            }, bw);
            var offset44 = WriteGenericSection(info.Section44, (s, bw1) =>
            {
                WriteString(s.Item1, bw1);
                WriteString(s.Item2, bw1);
                bw1.Write(s.Item3);
            }, bw);
            var offset48 = WriteGenericSection(info.Section48, (s, bw1) =>
            {
                WriteString(s.Item1, bw1);
                WriteString(s.Item2, bw1);
                bw1.Write(s.Item3);
            }, bw);
            var offset52 = WriteGenericSection(info.Section52, WriteString, bw);
            var offset56 = WriteGenericSection(info.Section56, (s, bw1) =>
            {
                WriteString(s.Item1, bw1);
                bw1.Write((ushort)s.Item2);
                bw1.Write((ushort)(s.Item2 >> 16));
            }, bw);
            var offset60 = WriteGenericSection(info.Section60, (s, bw1) =>
            {
                WriteString(s.Item1, bw1);
                bw1.Write((byte)s.Item2.Length);
                bw1.Write(s.Item2);
            }, bw);
            var offset64 = WriteSimplerSection(info.Section64, (s, bw1) =>
            {
                WriteString(s.Item1, bw1);
                bw1.Write((ushort)s.Item2.Length);
                foreach (var us in s.Item2)
                    bw1.Write(us);
            }, bw);
            var offset68 = WriteSimplerSection(info.Section68, (s, bw1) =>
            {
                bw1.Write(s.Item1);
                bw1.Write(s.Item2);
                bw1.Write(s.Item3);
            }, bw);
            var offset72 = WriteGenericSection(info.Section72, (s, bw1) =>
            {
                bw1.Write(s.Item1);
                WriteString(s.Item2, bw1);
            }, bw);
            var offset76 = WriteGenericSection(info.Section76, (s, bw1) =>
            {
                bw1.Write(s.Item1);
                bw1.Write(s.Item2);
                bw1.Write(s.Item3);
                bw1.Write(s.Item4);
                switch (s.Item1)
                {
                    case 0:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                        break;
                    case 1:
                        bw1.Write(s.Item5.Value);
                        WriteString(s.Item6, bw1);
                        break;
                    default:
                        throw new InvalidDataException();
                }
            }, bw);

            return (offset36, offset40, offset44, offset48, offset52, offset56, offset60, offset64, offset68, offset72,
                offset76);
        }
        
        public static void BuildScenario(string sourceDirectory, Stream output)
        {
            output.Seek(0, SeekOrigin.Begin);
            var header = new ScenarioHeader
            {
                magic = 542264915,
                // As in the original
                // (whatever they mean)
                // I'm not really sure if the game actually uses them
                unk1 = 157167,
                unk2 = 63,
                unk3 = 129,
                unk4 = 0,
                unk5 = 0,
                unk6 = 0
            };

            output.Write(MemoryMarshal.Cast<ScenarioHeader, byte>(MemoryMarshal.CreateSpan(ref header, 1)));

            using var headInfoFile = File.OpenText(sourceDirectory + "/head_data.json");
            var headerInfo = ScenarioHeadInfo.DeserializeFrom(headInfoFile);

            var sectionOffsets = WriteHeaderInfo(headerInfo, output);

            var codeOffset = (int) output.Position;
            var align = 0;
            while (codeOffset % 16 != 0)
            {
                align++;
                codeOffset++;
            }

            for (var i = 0; i <align; i++)
                output.WriteByte(0);
            
            header.offset_36 = sectionOffsets.Item1;
            header.offset_40 = sectionOffsets.Item2;
            header.offset_44 = sectionOffsets.Item3;
            header.offset_48 = sectionOffsets.Item4;
            header.offset_52 = sectionOffsets.Item5;
            header.offset_56 = sectionOffsets.Item6;
            header.offset_60 = sectionOffsets.Item7;
            header.offset_64 = sectionOffsets.Item8;
            header.offset_68 = sectionOffsets.Item9;
            header.offset_72 = sectionOffsets.Item10;
            header.offset_76 = sectionOffsets.Item11;
            header.commands_offset = (uint)codeOffset;
            
            
            using var codeFile = File.OpenText(sourceDirectory + "/listing.asm");

            var parser = new Parser(codeFile);
            var (instr, lab) = parser.ReadAll();
            
            instr = Assembler.FixupJumpOffsets(codeOffset, instr);
            Assembler.Assemble(instr, output);

            header.size = (uint)output.Length;
            
            output.Seek(0, SeekOrigin.Begin);
            output.Write(MemoryMarshal.Cast<ScenarioHeader, byte>(MemoryMarshal.CreateSpan(ref header, 1)));
            output.Seek(codeOffset, SeekOrigin.Begin);
        }
    }
}