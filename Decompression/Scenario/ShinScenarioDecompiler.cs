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
                /*var initJumps = ScenarioIdTracer.GetUses(disassemblyView)
                    .Select(_ => (_, disassemblyView[_])).ToImmutableArray();
                
                var entries = initJumps
                    .Where(_ => _.Item2.Opcode == Opcode.jc && _.Item2.JumpCondition == JumpCondition.Equal)
                    // ReSharper disable once PossibleInvalidOperationException
                    .Select(_ => (((NumberSpec) _.Item2.Data[2]).Value!.Value, (int)_.Item2.Data[3]))
                    
                    .Concat(initJumps.Where(_ => _.Item2.Opcode == Opcode.jt).Select(_ => _.Item2.Data[1])
                        .SelectMany(_ => ((IEnumerable<int>)_).Select((x, i) => (i, x))))
                    
                    .ToImmutableDictionary(x => x.Item1, x => x.Item2);
                s.Stop();
                Console.WriteLine($"Scenario Id Trace: {s.Elapsed}");*/

                var labelBuilder = new LabelCollection.Builder();
                //labelBuilder.AddEntries(entries, entryNames);
                
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
                
                /*for (var i = 0; i < 188; i++)
                    mapBuilder.MarkTipsEntry(labels[$"TIPS_ENTRY_{i:000}"], $"tips/{i:000}");
                mapBuilder.MarkRegion(labels["TIPS_ENTRY"], labels["TIPS_JUMPS"], "tips/common");
                mapBuilder.MarkFixedNumberOfInstructions(labels["TIPS_JUMPS"], 189, "tips/common");
                
                for (var i = 0; i < 100; i++)
                    mapBuilder.MarkKakeraEntry(labels[$"KAKERA_ENTRY_{i:00}"], $"kakera/{i:00}");
                mapBuilder.MarkRegion(labels["KAKERA_TSUMUGI"], labels["KAKERA_JUMPS"], "kakera/common");
                mapBuilder.MarkFixedNumberOfInstructions(labels["KAKERA_JUMPS"], 100, "kakera/common");
                
                mapBuilder.MarkFunctionAt(labels["FUN_WIPE"], "wipe_helper");
                mapBuilder.MarkFunctionAt(labels["FUN_EV_JIKAI_PREVIEW"], "jikai");*/
                
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