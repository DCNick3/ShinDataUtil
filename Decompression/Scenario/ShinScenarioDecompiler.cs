using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Scenario;

namespace ShinDataUtil.Decompression.Scenario
{
    public static unsafe class ShinScenarioDecompiler
    {
        private static readonly Common.ShiftJisEncoding ShiftJis = new Common.ShiftJisEncoding();

        private static string ReadString(ref ReadOnlySpan<byte> data)
        {
            var l = data[0];
            var s = ShiftJis.GetString(data.Slice(1, l - 1));
            data = data[(l + 1)..];
            return s;
        }

        private static byte[] ReadByteString(ref ReadOnlySpan<byte> data)
        {
            var l = data[0];
            var s = data.Slice(1, l).ToArray();
            data = data[(l + 1)..];
            return s;
        }

        private static T Read<T>(ref ReadOnlySpan<byte> data) where T : unmanaged
        {
            var r = MemoryMarshal.Read<T>(data);
            data = data[sizeof(T)..];
            return r;
        }

        private static List<string> HandleStringsSection(ReadOnlySpan<byte> data)
        {
            checked
            {
                var r = new List<string>();
                var header = MemoryMarshal.Read<ScenarioSectionHeader>(data);
                data = data.Slice(sizeof(ScenarioSectionHeader), (int) header.byteSize);
                for (var i = 0; i < header.elementCount; i++)
                    r.Add(ReadString(ref data));

                return r;
            }
        }
// TODO: use generics like in scenario build code to reduce size of this mess 
#region "Section handlers"
        private static List<(string, ushort)> Handle40(ReadOnlySpan<byte> data)
        {
            checked
            {
                var r = new List<(string, ushort)>();
                var header = MemoryMarshal.Read<ScenarioSectionHeader>(data);
                data = data.Slice(sizeof(ScenarioSectionHeader), (int) header.byteSize);
                for (var i = 0; i < header.elementCount; i++)
                {
                    var s = ReadString(ref data);
                    var n = Read<ushort>(ref data);

                    //Trace.Assert(n == 0xffff);
                    r.Add((s, n));
                }

                return r;
            }
        }

        private static List<(string, string, ushort)> Handle44(ReadOnlySpan<byte> data)
        {
            checked
            {
                var r = new List<(string, string, ushort)>();
                var header = MemoryMarshal.Read<ScenarioSectionHeader>(data);
                data = data.Slice(sizeof(ScenarioSectionHeader), (int) header.byteSize);
                for (var i = 0; i < header.elementCount; i++)
                {
                    var s1 = ReadString(ref data);
                    var s2 = ReadString(ref data);
                    var us = Read<ushort>(ref data);

                    r.Add((s1, s2, us));
                }

                return r;
            }
        }

        private static List<(string, string, ushort)> Handle48(ReadOnlySpan<byte> data)
        {
            checked
            {
                var r = new List<(string, string, ushort)>();
                var header = MemoryMarshal.Read<ScenarioSectionHeader>(data);
                data = data.Slice(sizeof(ScenarioSectionHeader), (int) header.byteSize);
                for (var i = 0; i < header.elementCount; i++)
                {
                    var s1 = ReadString(ref data);
                    var s2 = ReadString(ref data);
                    var us = Read<ushort>(ref data);

                    Trace.Assert(us == 0xffff);

                    r.Add((s1, s2, us));
                }

                return r;
            }
        }

        private static List<(string s, int ul)> Handle56(ReadOnlySpan<byte> data)
        {
            checked
            {
                var r = new List<(string s, int ul)>();
                var header = MemoryMarshal.Read<ScenarioSectionHeader>(data);
                data = data.Slice(sizeof(ScenarioSectionHeader), (int) header.byteSize);
                for (var i = 0; i < header.elementCount; i++)
                {
                    var s = ReadString(ref data);
                    var us1 = Read<ushort>(ref data);
                    var us2 = Read<ushort>(ref data);
                    var ul = (us2 << 16) | us1;

                    //Trace.Assert(us2 == 0xffff);

                    r.Add((s, ul));
                }

                return r;
            }
        }

        private static List<(string, byte[])> Handle60(ReadOnlySpan<byte> data)
        {
            checked
            {
                var r = new List<(string s, byte[] bs)>();
                var header = MemoryMarshal.Read<ScenarioSectionHeader>(data);
                data = data.Slice(sizeof(ScenarioSectionHeader), (int) header.byteSize);
                for (var i = 0; i < header.elementCount; i++)
                {
                    var s = ReadString(ref data);
                    var bs = ReadByteString(ref data);

                    r.Add((s, bs));
                }

                return r;
            }
        }

        private static List<(string, ushort[])> Handle64(ReadOnlySpan<byte> data)
        {
            checked
            {
                var r = new List<(string s, ushort[])>();
                var count = Read<uint>(ref data);
                for (var i = 0; i < count; i++)
                {
                    var s = ReadString(ref data);
                    var us1 = Read<ushort>(ref data);
                    var list = new List<ushort>();

                    for (var j = 0; j < us1; j++)
                        list.Add(Read<ushort>(ref data));

                    r.Add((s, list.ToArray()));
                }

                return r;
            }
        }

        private static List<(ushort, ushort, ushort)> Handle68(ReadOnlySpan<byte> data)
        {
            checked
            {
                var r = new List<(ushort, ushort, ushort)>();
                var count = Read<uint>(ref data);
                for (var i = 0; i < count; i++)
                {
                    var us1 = Read<ushort>(ref data);
                    var us2 = Read<ushort>(ref data);
                    var us3 = Read<ushort>(ref data);
                    r.Add((us1, us2, us3));
                }

                return r;
            }
        }

        private static List<(ushort, string)> Handle72(ReadOnlySpan<byte> data)
        {
            checked
            {
                var r = new List<(ushort, string)>();
                var header = MemoryMarshal.Read<ScenarioSectionHeader>(data);
                data = data.Slice(sizeof(ScenarioSectionHeader), (int) header.byteSize);
                for (var i = 0; i < header.elementCount; i++)
                {
                    var us = Read<ushort>(ref data);
                    var s = ReadString(ref data);

                    r.Add((us, s));
                }

                return r;
            }
        }

        private static List<(ushort, short, short, ushort, ushort?, string?)> Handle76(ReadOnlySpan<byte> data)
        {
            checked
            {
                var r = new List<(ushort, short, short, ushort, ushort?, string?)>();
                var header = MemoryMarshal.Read<ScenarioSectionHeader>(data);
                data = data.Slice(sizeof(ScenarioSectionHeader), (int) header.byteSize);
                for (var i = 0; i < header.elementCount; i++)
                {
                    var us1 = Read<ushort>(ref data);
                    var us2 = Read<short>(ref data);
                    var us3 = Read<short>(ref data);
                    var us4 = Read<ushort>(ref data);
                    
                    switch (us1)
                    {                        
                        case 0:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                            r.Add((us1, us2, us3, us4, null, null));
                            break;
                        case 1:
                            r.Add((us1, us2, us3, us4, Read<ushort>(ref data), ReadString(ref data)));
                            break;
                        default:
                            throw new InvalidDataException();
                    }
                }

                return r;
            }
        }

#endregion
        private static ScenarioHeadInfo ReadHead(ReadOnlySpan<byte> data, ScenarioHeader header)
        {
            // ??
            var section36 = HandleStringsSection(data.Slice((int)header.offset_36)).ToImmutableArray();
                
            // Looks like picture names
            var section40 = Handle40(data.Slice((int) header.offset_40)).ToImmutableArray();
                
            // Looks like bustup names
            // The ushort is the character index (used for lip sync) 
            var section44 = Handle44(data.Slice((int) header.offset_44)).ToImmutableArray();

            // Looks like bgm names
            var section48 = ImmutableArray<(string, string, ushort)>.Empty; //Handle48(data.Slice((int) header.offset_48)).ToImmutableArray();
                
            // Looks line sfx names
            var section52 = HandleStringsSection(data.Slice((int) header.offset_52)).ToImmutableArray();

            // Looks like movie names
            var section56 = Handle56(data.Slice((int) header.offset_56)).ToImmutableArray();

            // Used for (broken) character mute functionality
            // Contains information about which sounds contain whose voices
            var section60 = Handle60(data.Slice((int) header.offset_60)).ToImmutableArray();
                
            // Looks like CG names
            var section64 = ImmutableArray<(string, ushort[])>.Empty;//Handle64(data.Slice((int) header.offset_64)).ToImmutableArray();

            // ??????                
            var section68 = ImmutableArray<(ushort, ushort, ushort)>.Empty;//Handle68(data.Slice((int) header.offset_68)).ToImmutableArray();

            // Looks like tips names
            var section72 = ImmutableArray<(ushort, string)>.Empty;//Handle72(data.Slice((int) header.offset_72)).ToImmutableArray();

            // Looks like chapter names
            // Chart data =)
            var section76 = ImmutableArray<(ushort, short, short, ushort, ushort?, string?)>.Empty;//Handle76(data.Slice((int) header.offset_76)).ToImmutableArray();

            return new ScenarioHeadInfo(section36, section40, section44, section48, section52, section56,
                section60, section64, section68, section72, section76);
        }
        
        private static void PrintStats(IEnumerable<Instruction> instructions)
        {
            var usages = new Dictionary<Opcode, int>();
            foreach (var instruction in instructions)
            {
                usages.TryGetValue(instruction.Opcode, out var count);
                usages[instruction.Opcode] = count + 1;
            }

            foreach (var pair in usages.OrderByDescending(_ => _.Value))
                Console.WriteLine($"{pair.Key,20} -> {pair.Value}");
        }

        private static void FillKnownLabels(DisassemblyView disassemblyView, LabelCollection.Builder l)
        {
            l.Add(0x016bb3, "SHOW_SNRSEL_0");
            l.Add(0x016bd3, "SHOW_SNRSEL_1");
            l.Add(0x016bf3, "SHOW_SNRSEL_2");
            l.Add(0x016c13, "SHOW_SNRSEL_3");
            l.Add(0x016e33, "FUN_WIPE");
            l.Add(0x17711f, "QUIZ_LV1_PASS");
            l.Add(0x1771af, "QUIZ_LV1_FAIL");
            l.Add(0x17721d, "QUIZ_LV2_PASS");
            l.Add(0x17727d, "QUIZ_LV2_FAIL");
            l.Add(0x1772eb, "QUIZ_LV3_PASS");
            l.Add(0x17734b, "QUIZ_LV3_FAIL");
            l.Add(0x1773b9, "QUIZ_LV4_PASS");
            l.Add(0x17743d, "QUIZ_LV4_FAIL");
            l.Add(0x17825d, "QUIZ_LV5_PASS");
            l.Add(0x1782b9, "QUIZ_LV5_FAIL");
            l.Add(0x178327, "QUIZ_LV6_PASS");
            l.Add(0x17834c, "QUIZ_LV6_FAIL");
            l.Add(0x1783bb, "QUIZ_LV7_PASS");
            l.Add(0x1783e9, "QUIZ_LV7_FAIL");
            l.Add(0x178458, "QUIZ_LV8_PASS");
            l.Add(0x178494, "QUIZ_LV8_FAIL");
            l.Add(0x177447, "QUIZ_EXIT");
            l.Add(0x17758a, "SHOW_QUIZ_FAIL");
            l.Add(0x1774c1, "SHOW_QUIZ_PASS");
            l.Add(0x01582b, "CHART_JUMPS");

            l.Add(0x03e5b6, "FUN_TEST_MODE_UNLOCK_CHART");
            
            l.Add(0x075895, "KAKERA_TSUMUGI");
            l.Add(0x134cb3, "KAKERA_END");
            
            l.Add(0x01561d, "SCENARIO_BEGINNING");
            l.Add(0x01731f, "DEBUG_MENU_ROOT_PAGE_1");
            l.Add(0x017408, "DEBUG_MENU_ROOT_PAGE_2");
            l.Add(0x017541, "DEBUG_MENU_SECTION_1");
            l.Add(0x0175d7, "DEBUG_MENU_SECTION_2");
            l.Add(0x01766d, "DEBUG_MENU_SECTION_3");
            l.Add(0x017703, "DEBUG_MENU_SECTION_4_PAGE_1");
            l.Add(0x017792, "DEBUG_MENU_SECTION_4_PAGE_2");
            l.Add(0x0174b3, "DEBUG_MENU_SECTION_5");
            l.Add(0x0177ec, "DEBUG_MENU_PS");
            l.Add(0x017d97, "DEBUG_MENU_TIPS");
            l.Add(0x019c79, "DEBUG_MENU_KAKERA_PAGE_1");
            l.Add(0x017885, "DEBUG_MENU_TEST_MODE_PAGE_1");
            l.Add(0x017915, "DEBUG_MENU_TEST_MODE_PAGE_2");

            l.Add(0x0175c3, "DEBUG_MENU_SECTION_1_PROLOGUE");
            l.Add(0x0175cd, "DEBUG_MENU_SECTION_1_EPILOGUE");
            l.Add(0x017659, "DEBUG_MENU_SECTION_2_PROLOGUE");
            l.Add(0x017663, "DEBUG_MENU_SECTION_2_EPILOGUE");
            l.Add(0x0176ef, "DEBUG_MENU_SECTION_3_PROLOGUE");
            l.Add(0x0176f9, "DEBUG_MENU_SECTION_3_EPILOGUE");
            l.Add(0x0177d8, "DEBUG_MENU_SECTION_4_PROLOGUE");
            l.Add(0x0177e2, "DEBUG_MENU_SECTION_4_EPILOGUE");

            l.Add(0x017caf, "TEST_MODE_PIC_M");
            l.Add(0x017d23, "TEST_MODE_PIC_L");
            l.Add(0x023435, "TEST_MODE_BG");
            l.Add(0x01ab4b, "TEST_MODE_EVG");
            l.Add(0x03765b, "TEST_MODE_TEXTG");
            l.Add(0x01797c, "TEST_MODE_JIKAI_PREVIEW");
            l.Add(0x03ee41, "TEST_MODE_BGM");
            l.Add(0x03e3e4, "TEST_MODE_UNSAFE_UNLOCK_SCENARIO");
            
            l.Add(0x017a18, "TEST_MODE_WATANAGASHI_PREVIEW");
            l.Add(0x017a27, "TEST_MODE_ONIKAKUSHISHI_PREVIEW");
            l.Add(0x017a36, "TEST_MODE_TATARIGOROSHI_PREVIEW");
            l.Add(0x017a45, "TEST_MODE_TSUKIOTOSHI_PREVIEW");
            l.Add(0x017a54, "TEST_MODE_TARAIMAWASHI_PREVIEW");
            l.Add(0x017a63, "TEST_MODE_SOMEUTSUSHI_PREVIEW");
            
            l.Add(0x134e0c, "FUN_EV_JIKAI_PREVIEW");
            l.Add(0x136cd1, "EV_JIKAI_END");
            l.Add(0x134e41, "EV_JIKAI_WATANAGASHI");
            l.Add(0x13532b, "EV_JIKAI_ONIthKAKUSHISHI");
            l.Add(0x135805, "EV_JIKAI_TATARIGOROSHI");
            l.Add(0x135cdf, "EV_JIKAI_TSUKIOTOSHI");
            l.Add(0x1362c7, "EV_JIKAI_TARAIMAWASHI");
            l.Add(0x1367a8, "EV_JIKAI_SOMEUTSUSHI");
            
            l.Add(0x019ed0, "DEBUG_MENU_KAKERA_A");
            l.Add(0x019f7b, "DEBUG_MENU_KAKERA_B");
            l.Add(0x01a038, "DEBUG_MENU_KAKERA_C");
            l.Add(0x01a0fd, "DEBUG_MENU_KAKERA_D");
            l.Add(0x01a1be, "DEBUG_MENU_KAKERA_E");
            l.Add(0x01a27d, "DEBUG_MENU_KAKERA_F");
            l.Add(0x01a32a, "DEBUG_MENU_KAKERA_G");
            l.Add(0x01a3e7, "DEBUG_MENU_KAKERA_H");
            l.Add(0x01a48e, "DEBUG_MENU_KAKERA_J");
            l.Add(0x01a563, "DEBUG_MENU_KAKERA_K");
            l.Add(0x01a638, "DEBUG_MENU_KAKERA_L");
            l.Add(0x01a711, "DEBUG_MENU_KAKERA_M");
            l.Add(0x01a7c7, "DEBUG_MENU_KAKERA_V");
            l.Add(0x01a895, "DEBUG_MENU_KAKERA_W");
            
            l.Add(0x01a965, "DEBUG_MENU_KAKERA_X");
            l.Add(0x01a9fb, "DEBUG_MENU_KAKERA_Y");
            l.Add(0x01aaa1, "DEBUG_MENU_KAKERA_Z");
            {
                var currentAddress = 0x0758d4;
                l.Add(currentAddress, "KAKERA_JUMPS");

                for (var i = 0; i < 100; i++)
                {
                    var instr = disassemblyView.GetInstructionAt(currentAddress);

                    Trace.Assert(instr.Opcode == Opcode.jc);
                    Trace.Assert((byte)instr.Data[0] == 0);
                    Trace.Assert(((NumberSpec)instr.Data[1]).Address == 0);
                    Trace.Assert(((NumberSpec)instr.Data[2]).Value == i);
                    var offset = (int)instr.Data[3];
                    l.Add(offset, $"KAKERA_ENTRY_{i:00}");
                    
                    (currentAddress, _) = disassemblyView.GetNextInstruction(currentAddress);
                }
            }
            

            l.Add(0x208668, "TIPS_END");
            l.Add(0x17849e, "TIPS_ENTRY");
            
            l.Add(0x018090, "DEBUG_MENU_TIPS_0");
            l.Add(0x01814e, "DEBUG_MENU_TIPS_5");
            l.Add(0x018201, "DEBUG_MENU_TIPS_10");
            l.Add(0x0182c0, "DEBUG_MENU_TIPS_15");
            l.Add(0x018381, "DEBUG_MENU_TIPS_20");
            l.Add(0x018476, "DEBUG_MENU_TIPS_25");
            l.Add(0x01852f, "DEBUG_MENU_TIPS_30");
            l.Add(0x0185fc, "DEBUG_MENU_TIPS_35");
            l.Add(0x0186af, "DEBUG_MENU_TIPS_40");
            l.Add(0x018760, "DEBUG_MENU_TIPS_45");
            l.Add(0x018815, "DEBUG_MENU_TIPS_50");
            l.Add(0x0188d4, "DEBUG_MENU_TIPS_55");
            l.Add(0x018989, "DEBUG_MENU_TIPS_60");
            l.Add(0x018a40, "DEBUG_MENU_TIPS_65");
            l.Add(0x018afe, "DEBUG_MENU_TIPS_70");
            l.Add(0x018bce, "DEBUG_MENU_TIPS_75");
            l.Add(0x018c82, "DEBUG_MENU_TIPS_80");
            l.Add(0x018d46, "DEBUG_MENU_TIPS_85");
            l.Add(0x018e00, "DEBUG_MENU_TIPS_90");
            l.Add(0x018ebc, "DEBUG_MENU_TIPS_95");
            l.Add(0x018f86, "DEBUG_MENU_TIPS_100");
            l.Add(0x01905c, "DEBUG_MENU_TIPS_105");
            l.Add(0x019138, "DEBUG_MENU_TIPS_110");
            l.Add(0x0191fe, "DEBUG_MENU_TIPS_115");
            l.Add(0x0192a8, "DEBUG_MENU_TIPS_120");
            l.Add(0x01935c, "DEBUG_MENU_TIPS_125");
            l.Add(0x01940c, "DEBUG_MENU_TIPS_130");
            l.Add(0x0194be, "DEBUG_MENU_TIPS_135");
            l.Add(0x01956a, "DEBUG_MENU_TIPS_140");
            l.Add(0x019630, "DEBUG_MENU_TIPS_145");
            l.Add(0x0196ec, "DEBUG_MENU_TIPS_150");
            l.Add(0x0197ac, "DEBUG_MENU_TIPS_155");
            l.Add(0x019866, "DEBUG_MENU_TIPS_160");
            l.Add(0x01991c, "DEBUG_MENU_TIPS_165");
            l.Add(0x0199cd, "DEBUG_MENU_TIPS_170");
            l.Add(0x019a8b, "DEBUG_MENU_TIPS_175");
            l.Add(0x019b3f, "DEBUG_MENU_TIPS_180");
            l.Add(0x019be7, "DEBUG_MENU_TIPS_185");
            
            {
                var currentAddress = 0x1784db;
                l.Add(currentAddress, "TIPS_JUMPS");

                for (var i = 0; i < 188; i++)
                {
                    var instr = disassemblyView.GetInstructionAt(currentAddress);

                    Trace.Assert(instr.Opcode == Opcode.jc);
                    Trace.Assert((byte)instr.Data[0] == 0);
                    Trace.Assert(((NumberSpec)instr.Data[1]).Address == 0);
                    Trace.Assert(((NumberSpec)instr.Data[2]).Value == i);
                    var offset = (int)instr.Data[3];
                    l.Add(offset, $"TIPS_ENTRY_{i:000}");
                    
                    (currentAddress, _) = disassemblyView.GetNextInstruction(currentAddress);
                }
            }
        }
 
        public static void Decompile(ReadOnlyMemory<byte> memory, string destinationDirectory)
        {
            checked
            {
                var data = memory.Span;
                var header = MemoryMarshal.Read<ScenarioHeader>(data);

                Trace.Assert(header.magic == 0x20524e53);
                Trace.Assert(header.size == data.Length);

                var codeSize = header.size - header.commands_offset;
                
                var s = Stopwatch.StartNew();
                var headData = ReadHead(data, header);
                
                using (var f = File.CreateText(destinationDirectory + "/head_data.json"))
                    headData.SerializeTo(f);
                s.Stop();
                Console.WriteLine($"head_data dump: {s.Elapsed}");
                
                s.Restart();
                using (var f = File.Create(destinationDirectory + "/code_dump.bin"))
                    f.Write(data.Slice((int)header.commands_offset));
                s.Stop();
                Console.WriteLine($"code dump: {s.Elapsed}");
                
                s.Restart();
                var disassemblyView = Disassembler.Disassemble(memory, (int) header.commands_offset);
                s.Stop();
                Console.WriteLine($"Disassembly: {s.Elapsed}");
                
                s.Restart();
                var dataXRefDb = DataXRefDb.FromDisassembly(disassemblyView);
                s.Stop();
                Console.WriteLine($"Data XRef: {s.Elapsed}");

                s.Restart();
                var initJumps = ScenarioIdTracer.GetUses(disassemblyView)
                    .Select(_ => (_, disassemblyView[_])).ToImmutableArray();
                
                var entries = initJumps
                    .Where(_ => _.Item2.Opcode == Opcode.jc && _.Item2.JumpCondition == JumpCondition.Equal)
                    // ReSharper disable once PossibleInvalidOperationException
                    .Select(_ => (((NumberSpec) _.Item2.Data[2]).Value!.Value, (int)_.Item2.Data[3]))
                    
                    .Concat(initJumps.Where(_ => _.Item2.Opcode == Opcode.jt).Select(_ => _.Item2.Data[1])
                        .SelectMany(_ => ((IEnumerable<int>)_).Select((x, i) => (i, x))))
                    
                    .ToImmutableDictionary(x => x.Item1, x => x.Item2);
                s.Stop();
                Console.WriteLine($"Scenario Id Trace: {s.Elapsed}");

                var entryNames = new Dictionary<int, string>
                {
                    {0, "SEC_0"},
                    {1, "SEC_1"},
                    {2, "SEC_2"},
                    {3, "SEC_3"},
                    {4, "SEC_4"},
                    {5, "QUIZ_LV1"},
                    {6, "QUIZ_LV2"},
                    {7, "QUIZ_LV3"},
                    {8, "QUIZ_LV4"},
                    {9, "QUIZ_LV5"},
                    {10, "QUIZ_LV6"},
                    {11, "QUIZ_LV7"},
                    {12, "QUIZ_LV8"},
                };
                
                var labelBuilder = new LabelCollection.Builder();
                labelBuilder.AddEntries(entries, entryNames);
                
                var instr = disassemblyView.GetInstructionAt(disassemblyView.BeginAddress);
                if (instr.Opcode == Opcode.jc && instr.JumpCondition == JumpCondition.GreaterOrEqual)
                {
                    instr = disassemblyView.GetNextInstruction(disassemblyView.BeginAddress).Item2;
                    Trace.Assert(instr.Opcode == Opcode.j);
                    labelBuilder.Add(instr.Data[0], "ENTRY_TEST_MODE");
                }
                
                s.Restart();
                // TODO: make a general way to declare transformation applied to the "stock" scenario
                FillKnownLabels(disassemblyView, labelBuilder);
                
                var labels = labelBuilder.Build();
                
                var mapBuilder = new DisassemblyMap.Builder(disassemblyView, labels);
                
                for (var i = 0; i < 188; i++)
                    mapBuilder.MarkTipsEntry(labels[$"TIPS_ENTRY_{i:000}"], $"tips/{i:000}");
                mapBuilder.MarkRegion(labels["TIPS_ENTRY"], labels["TIPS_JUMPS"], "tips/common");
                mapBuilder.MarkFixedNumberOfInstructions(labels["TIPS_JUMPS"], 189, "tips/common");
                
                for (var i = 0; i < 100; i++)
                    mapBuilder.MarkKakeraEntry(labels[$"KAKERA_ENTRY_{i:00}"], $"kakera/{i:00}");
                mapBuilder.MarkRegion(labels["KAKERA_TSUMUGI"], labels["KAKERA_JUMPS"], "kakera/common");
                mapBuilder.MarkFixedNumberOfInstructions(labels["KAKERA_JUMPS"], 100, "kakera/common");
                
                mapBuilder.MarkFunctionAt(labels["FUN_WIPE"], "wipe_helper");
                mapBuilder.MarkFunctionAt(labels["FUN_EV_JIKAI_PREVIEW"], "jikai");

                var map = mapBuilder.Build();
                s.Stop();
                Console.WriteLine($"Map build: {s.Elapsed}");
                
                using var writer = File.CreateText(Path.Combine(destinationDirectory, "listing.asm"));
                
                s.Restart();
                var listingSourceMap = ListingCreator.CreateListing(disassemblyView, labels, map, writer);
                s.Stop();
                Console.WriteLine($"Listing: {s.Elapsed}");
            }
        }
    }
}