using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FastPngEncoderSharp;
using Newtonsoft.Json;
using ShinDataUtil.Common.Scenario;
using ShinDataUtil.Compression;
using ShinDataUtil.Compression.Scenario;
using ShinDataUtil.Decompression;
using ShinDataUtil.Decompression.Scenario;
using ShinDataUtil.Scenario;
using ShinDataUtil.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ShinDataUtil
{
    public static class Program
    {
        static string BytesToString(long byteCount)
        {
            string[] sizes = { "B", "KiB", "MiB", "GiB", "TiB" };
            int order = 0;
            while (byteCount >= 1024 && order < sizes.Length - 1) {
                order++;
                byteCount = byteCount/1024;
            }

            return $"{byteCount:0.##} {sizes[order]}";
        }
        
        /* TODO: use proper arguments parser */
        
        static int ListFiles(ReadOnlySpan<string> args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-list [filename]");
                return 1;
            }
            var filename = args[0];
            
            using var archive = new FileReadableGameArchive(filename);
            int count = 0;
            foreach (var file in archive.EnumerateAllFiles())
            {
                count++;
                NonBlockingConsole.WriteLine("{0,10} {1,10} {2}", file.DataOffset, file.Size, file.Path);
            }
            NonBlockingConsole.WriteLine($"In total: {count} files");

            return 0;
        }

        static int ExtractAllFiles(ReadOnlySpan<string> args, bool decode, bool dryRun)
        {
            var regex = new Regex(".*");
            
            if (args.Length > 0 && args[0].StartsWith("--"))
            {
                var flagName = args[0][2..];
                if (flagName == "regex")
                    regex = new Regex(args[1]);
                else
                    throw new Exception("parsing command line is boring");
                args = args[2..];
            }

            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-extract-all{0} --regex [regex] [filename] [destination]", decode ? "-with-decode" : "");
                return 1;
            }

            var filename = args[0];
            var destination = Path.GetFullPath(args[1]);
            
            using var archive = (ReadableGameArchive)new FileReadableGameArchive(filename);
            var count = 0;
            var readBytes = 0L;
            var writeBytes = 0L;

            void HandleOneFile(ReadableGameArchive.FileEntry file)
            {
                // ReSharper disable once AccessToModifiedClosure
                Interlocked.Increment(ref count);
                // ReSharper disable once AccessToModifiedClosure
                Interlocked.Add(ref readBytes, file.Size);
                // ReSharper disable once AccessToDisposedClosure
                using var s = archive.OpenFile(file);
                
                if (!decode)
                {
                    NonBlockingConsole.WriteLine("{0,10} {1,10} {2}", file.DataOffset, file.Size, file.Path);
                    using var d = File.OpenWrite(Path.Join(destination, file.Path));
                    d.Write(s.Data.Span);
                    Interlocked.Add(ref writeBytes, s.Data.Length);
                }
                else
                {
                    var ext = Path.GetExtension(file.Path);
                    var basePath = Path.Join(destination,
                        Path.GetDirectoryName(file.Path),
                        Path.GetFileNameWithoutExtension(file.Path));
                    switch (ext)
                    {
                        case ".pic":
                        {
                            NonBlockingConsole.WriteLine("PIC  {0}", file.Path);
                            var (image, effectiveSize, _) = ShinPictureDecoder.DecodePicture(s.Data.Span);

                            if (!dryRun)
                            {
                                FastPngEncoder.WritePngToFile(basePath + ".png", image, effectiveSize);
                                Interlocked.Add(ref writeBytes, new FileInfo(basePath + ".png").Length);
                            }

                            break;
                        }
                        case ".nxa":
                        {
                            NonBlockingConsole.WriteLine("SND  {0}", file.Path);
                            if (!dryRun)
                            {
                                using var destinationFile =
                                    File.Open(basePath + ".opus", FileMode.Create, FileAccess.Write);
                                ShinOpusRemuxer.Remux(s.Data.Span, destinationFile, basePath);
                                Interlocked.Add(ref writeBytes, destinationFile.Length);
                            }
                            else
                                ShinOpusRemuxer.Remux(s.Data.Span, Stream.Null, basePath);

                            break;
                        }
                        case ".fnt":
                        {
                            NonBlockingConsole.WriteLine("FNT  {0}", file.Path);
                            if (!dryRun)
                            {
                                var destinationDir = basePath + "_fnt";

                                if (Directory.Exists(destinationDir))
                                    Directory.Delete(destinationDir, true);
                                Directory.CreateDirectory(destinationDir);
                                
                                var written = ShinFontExtractor.Extract(s.Data.Span, destinationDir);
                                Interlocked.Add(ref writeBytes, written);
                            }
                            
                            break;
                        }
                        case ".txa":
                        {
                            NonBlockingConsole.WriteLine("TXA  {0}", file.Path);
                            if (!dryRun)
                            {
                                var destinationDir = basePath + "_txa";

                                if (Directory.Exists(destinationDir))
                                    Directory.Delete(destinationDir, true);
                                Directory.CreateDirectory(destinationDir);
                                
                                var written = ShinTxaExtractor.Extract(s.Data.Span, destinationDir);
                                Interlocked.Add(ref writeBytes, written);
                            }
                            break;
                        }
                        case ".bin":
                        {
                            Trace.Assert(file.Name == "sysse.bin");
                            NonBlockingConsole.WriteLine("SYSE {0}", file.Path);
                            if (!dryRun)
                            {
                                var destinationDir = basePath + "_sysse";

                                if (Directory.Exists(destinationDir))
                                    Directory.Delete(destinationDir, true);
                                Directory.CreateDirectory(destinationDir);
                                
                                var written = 
                                    ShinSysseExtractor.Extract(s.Data.Span, destinationDir);
                                Interlocked.Add(ref writeBytes, written);
                            }
                            break;
                        }
                        case ".bup":
                        {
                            NonBlockingConsole.WriteLine("BUP  {0}", file.Path);
                            

                            if (!dryRun)
                            {
                                var destinationPath = basePath + "_bup";

                                if (Directory.Exists(destinationPath))
                                    Directory.Delete(destinationPath, true);
                                Directory.CreateDirectory(destinationPath);
                                
                                ShinBustupExtractor.Extract(s.Data.Span, destinationPath);
                                //Interlocked.Add(ref writeBytes, new FileInfo(destinationPath).Length);
                            }
                            break;
                        }
                        case ".msk":
                        {
                            NonBlockingConsole.WriteLine("MSK  {0}", file.Path);
                            
                            var image = ShinMaskDecompress.Decompress(s.Data.Span);
                            if (!dryRun)
                                FastPngEncoder.WritePngToFile(basePath + "_msk.png", image);
                            break;
                        }
                        case ".mp4":
                        {
                            NonBlockingConsole.WriteLine("VID  {0}", file.Path);
                            if (!dryRun)
                            {
                                using var destinationFile = File.Open(basePath + ".mp4", FileMode.Create, FileAccess.Write);
                                destinationFile.Write(s.Data.Span);
                                Interlocked.Add(ref writeBytes, destinationFile.Length);
                            }
                            else
                                Stream.Null.Write(s.Data.Span);

                            break;
                        }
                        default:
                            NonBlockingConsole.WriteLine("SKIP {0}", file.Path);
                            return;
                    }
                }
            }
            
            var allFiles = archive.EnumerateAllFiles()
                .Where(_ => regex.IsMatch(_.Path))
                .OrderBy(_ => _.DataOffset).ToArray();

            var dirs = allFiles.Select(_ => Path.GetDirectoryName(_.Path[1..] /* strip leading slash */ ))
                .Distinct().Select(_ => Path.Combine(destination, _!)).ToArray();

            foreach (var dir in dirs)
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

            if (decode)
            {
                // cpu-intensive
                Parallel.ForEach(allFiles, new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount + 1
                }, HandleOneFile);
            }
            else
                foreach (var file in allFiles)
                    HandleOneFile(file);

            Console.WriteLine($"In total: processed {count} files, {BytesToString(readBytes)} read, {BytesToString(writeBytes)} written");
            
            return 0;
        }

        static int DecodePicture(ReadOnlyMemory<byte> picdata, string _, string outname, ImmutableArray<string> options)
        {
            Trace.Assert(options.Length == 0);
            var (image, effectiveSize, _) = ShinPictureDecoder.DecodePicture(picdata.Span);
            
            FastPngEncoder.WritePngToFile(outname, image, effectiveSize);
            
            return 0;
        }

        static int DumpPictureFragments(ReadOnlyMemory<byte> picdata, string _, string outname, ImmutableArray<string> options)
        {
            Trace.Assert(options.Length == 0);
            
            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinPictureDecoder.DumpPictureFragments(picdata.Span, outname);

            return 0;
        }

        static int RemuxSound(ReadOnlyMemory<byte> nxadata, string nxaname, string outname, ImmutableArray<string> options)
        {
            Trace.Assert(options.Length == 0);
            using var outstream = File.Create(outname);

            ShinOpusRemuxer.Remux(nxadata.Span, outstream, nxaname);

            return 0;
        }

        static int Lz77Test(ReadOnlySpan<string> args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil lz77-test [filename] ...");
                return 1;
            }

            var offsetBits = 12;
            var compressor = new Lz77Compressor(offsetBits);

            foreach (var filename in args)
            {

                var data =
                    //    Encoding.UTF8.GetBytes("123123"); 
                    File.ReadAllBytes(filename);

                var sw = new Stopwatch();

                sw.Start();
                var (compressed, compressedSize) = compressor.Compress(data);
                sw.Stop();

                var decompressed = new byte[data.Length];
                Lz77Decompressor.Decompress(decompressed, compressed[..compressedSize], offsetBits);

                for (var i = 0; i < data.Length; i++)
                    if (decompressed[i] != data[i])
                        Debugger.Break();

                Console.WriteLine($"{filename,50}: {BytesToString(data.Length)} -> {BytesToString(compressedSize)} " +
                                  $"; K = {(double) compressedSize / data.Length:0.00}; T = {sw.ElapsedMilliseconds} ms");
            }

            return 0;
        }

        static int TxaEncode(ReadOnlySpan<string> args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil txa-encode [srcdir] [outfile]");
                return 1;
            }

            var srcdir = args[0];
            var outfile = args[1];

            using var outtxa = File.Create(outfile);
            
            ShinTxaEncoder.BuildTxa(outtxa, srcdir);

            return 0;
        }

        static int PicEncode(ReadOnlySpan<string> args)
        {
            var origin = ShinPictureEncoder.Origin.Bottom;
            var compressionConfig = new ShinTextureCompress.FragmentCompressionConfig
            {
                Quantize = false,
                Dither = false,
                LosslessAlpha = false
            };
            while (args.Length > 0 && args[0].StartsWith("--"))
            {
                var opt = args[0];
                args = args[1..];
                switch (opt)
                {
                    case "--origin":
                        var v = args[0];
                        args = args[1..];
                        if (int.TryParse(v, out var i))
                            origin = (ShinPictureEncoder.Origin) i;
                        else if (!Enum.TryParse(v, out origin))
                            throw new ArgumentException($"Unknown origin: {v}. Allowed values: " +
                                                        $"{string.Join(", ", Enum.GetValues<ShinPictureEncoder.Origin>())}");
                        break;
                    case "--quantize":
                        compressionConfig.Quantize = true;
                        break;
                    case "--dither":
                        compressionConfig.Dither = true;
                        break;
                    case "--lossless-alpha":
                        compressionConfig.LosslessAlpha = true;
                        break;
                    case "--version3":
                        compressionConfig.IsVersion3 = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option: {opt}");
                }
            }
            
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil pic-encode {--origin [origin]} [srcpng] [outfile]");
                return 1;
            }

            var srcpng = args[0];
            var outfile = args[1];

            using var outpic = File.Create(outfile);
            using var fs = File.OpenRead(srcpng);
            using var image = PngDecoder.Instance.Decode<Rgba32>(new DecoderOptions(), fs);

            var rnd = new Random((int)Environment.TickCount64 ^ Environment.ProcessId);
            var pictureId = (uint)rnd.Next();
            
            ShinPictureEncoder.EncodePicture(outpic, image, image.Width, image.Height, pictureId, origin, compressionConfig);

            return 0;
        }

        static int PicEncodeRoundtrip(ReadOnlySpan<string> args)
        {
            var compressionConfig = new ShinTextureCompress.FragmentCompressionConfig
            {
                Quantize = false,
                Dither = false,
                LosslessAlpha = false
            };
            while (args.Length > 0 && args[0].StartsWith("--"))
            {
                var opt = args[0];
                args = args[1..];
                switch (opt)
                {
                    case "--quantize":
                        compressionConfig.Quantize = true;
                        break;
                    case "--dither":
                        compressionConfig.Dither = true;
                        break;
                    case "--lossless-alpha":
                        compressionConfig.LosslessAlpha = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option: {opt}");
                }
            }
            
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil pic-encode {--origin [origin]} [srcpng] [outfile]");
                return 1;
            }

            var srcpng = args[0];
            var outpng = args[1];

            using var outpic = new MemoryStream();
            using var fs = File.OpenRead(srcpng);
            using var image = PngDecoder.Instance.Decode<Rgba32>(new DecoderOptions(), fs);

            ShinPictureEncoder.EncodePicture(outpic, image, image.Width, image.Height, 0, 
                ShinPictureEncoder.Origin.Bottom, compressionConfig);
            
            
            var (decodedImage, size, _) = ShinPictureDecoder.DecodePicture(outpic.GetBuffer()[..(int)outpic.Length]);

            var (effectiveWidth, effectiveHeight) = size;
            
            var mse = 0.0;
            for (var j = 0; j < effectiveHeight; j++)
            {
                var span1 = image.DangerousGetPixelRowMemory(j)[..effectiveWidth].Span;
                var span2 = decodedImage.DangerousGetPixelRowMemory(j)[..effectiveWidth].Span;
                if (span1.SequenceEqual(span2))
                    continue;
                for (var i = 0; i < effectiveWidth; i++)
                    mse += (span1[i].ToVector4() - span2[i].ToVector4()).LengthSquared();
            }
            
            Console.WriteLine($"Encoded size: {BytesToString(outpic.Length)} ({outpic.Length} bytes)");
            Console.WriteLine($"MSE: {mse / effectiveHeight / effectiveWidth}");
            
            FastPngEncoder.WritePngToFile(outpng, decodedImage, size);
            
            return 0;
        }

        static int FontExtract(ReadOnlyMemory<byte> fntdata, string _, string outname, ImmutableArray<string> options)
        {
            Trace.Assert(options.Length == 0);
            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinFontExtractor.Extract(fntdata.Span, outname);
            
            return 0;
        }

        static int TxaExtract(ReadOnlyMemory<byte> txadata, string _, string outname, ImmutableArray<string> options)
        {
            var ignoreFileSize = false;
            foreach (var option in options)
            {
                if (option == "--ignore-file-size")
                    ignoreFileSize = true;
                else
                {
                    Console.Error.WriteLine($"Unknown option: {option}");
                    return 1;
                }
                }
                
                if (Directory.Exists(outname))
                    Directory.Delete(outname, true);
                Directory.CreateDirectory(outname);
            
            ShinTxaExtractor.Extract(txadata.Span, outname, ignoreFileSize);
            
            return 0;
        }

        static int SysseExtract(ReadOnlyMemory<byte> syssedata, string _, string outname, ImmutableArray<string> options)
        {
            Trace.Assert(options.Length == 0);
            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinSysseExtractor.Extract(syssedata.Span, outname);
            
            return 0;
        }

        static int BustupExtract(ReadOnlyMemory<byte> bupdata, string _, string outname, ImmutableArray<string> options)
        {
            Trace.Assert(options.Length == 0);
            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinBustupExtractor.Extract(bupdata.Span, outname);

            return 0;
        }

        private static int MaskExtract(ReadOnlyMemory<byte> mskdata, string _, string outname, ImmutableArray<string> options)
        {
            Trace.Assert(options.Length == 0);
            var image = ShinMaskDecompress.Decompress(mskdata.Span);
            
            FastPngEncoder.WritePngToFile(outname, image);
            
            return 0;
        }

        private static int ScenarioDecompile(ReadOnlyMemory<byte> snrdata, string _, string outname, ImmutableArray<string> options)
        {
            Trace.Assert(options.Length == 0);
            ShinScenarioDecompiler.Decompile(snrdata, outname);
            
            return 0;
        }

        private static int ScenarioBuild(ReadOnlySpan<string> args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil scenario-build [asmdir] [snrfile]");
                return 1;
            }

            var asmdir = args[0];
            var snrfile = args[1];
            
            using var f = File.Create(snrfile);
            ShinScenarioBuilder.BuildScenario(asmdir, f);

            f.Seek(0, SeekOrigin.Begin);
            var buf = new byte[f.Length];
            f.Read(buf);
            //ShinScenarioDecompiler.Decompile(buf, asmdir + "/redecomp");
            
            //var slice = res.instructions.Take(10000).ToArray();
            
            return 0;
        }

        private static int ScenarioLayout(ReadOnlySpan<string> args)
        {
            var ignoreLogset = false;
            while (args.Length > 0 && args[0].StartsWith("--"))
            {
                var flag = args[0][2..];
                args = args[1..];
                switch (flag)
                {
                    case "ignore-logset":
                        ignoreLogset = true;
                        break;
                    default:
                        throw new ArgumentException("Unknown flag: --" + flag);
                }
            }

            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil scenario-layout [--ignore-logset] [fntfile] [asmfile] [outasmfile]");
                return 1;
            }

            var fntfile = args[0];
            var asmfile = args[1];
            var outasmfile = args[2];

            var fontInfo = ShinFontExtractor.GetLayoutInfo(File.ReadAllBytes(fntfile));
            
            using var codeFile = File.OpenText(asmfile);

            var asmParser = new Parser(codeFile);
            var (instructions, lab) = asmParser.ReadAll();
            
            var visitor = new MessageEnglishLayoutHelper(fontInfo);
            var parser = new MessageTextParser();

            var outInstructions = new List<Instruction>();

            string ProcessMessage(string message)
            {
                try
                {
                    parser.ParseTo(message, visitor);
                }
                catch (Exception e)
                {
                    throw new AggregateException("While was processing message " +
                                                 $"{ListingCreator.FormatString(message)}", e);
                }

                return visitor.Dump();
            }
            
            foreach (var instr in instructions)
            {
                var instrUpd = instr;
                if (instr.Opcode == Opcode.MSGSET)
                {
                    string message = instr.Data[3];
                    instrUpd = instr.ChangeData(instr.Data.SetItem(3, ProcessMessage(message)));
                }
                else if (!ignoreLogset && instr.Opcode == Opcode.LOGSET)
                {
                    string message = instr.Data[0];
                    instrUpd = instr.ChangeData(instr.Data.SetItem(0, ProcessMessage(message)));
                }
                outInstructions.Add(instrUpd);
            }

            using var outfs = File.Create(outasmfile);
            using var outfstw = new StreamWriter(outfs);

            ListingCreator.CreateListing(outInstructions.ToImmutableArray(), lab, outfstw);
            
            return 0; 
        }

        private static int RomReplaceFile(ReadOnlySpan<string> args)
        {
            if (args.Length != 4)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-replace-file [origrom] [injfile] [targetname] [outrom]");
                return 1;
            }

            var origrom = args[0];
            var injfilename = args[1];
            var targetname = args[2];
            var outrom = args[3];

            using var outromfile = File.Create(outrom);
            using var injfile = File.OpenRead(injfilename);

            ShinRomOperations.ReplaceFile(origrom, outromfile, injfile, targetname);
            
            return 0;
        }

        private static int RomBuild(ReadOnlySpan<string> args)
        {
            if (args.Length < 1 || (args.Length - 1) % 2 != 0)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-build [outrom] {[infile] [targetname]}");
                return 1;
            }

            var outrom = args[0];
            var files = args[1..].ToArray().Select((x, i) => (i, x))
                .GroupBy(_ => _.i / 2).Select(_ => (_.ElementAt(0).x, _.ElementAt(1).x)).ToArray();

            using var outromfile = File.Create(outrom);
            
            ShinRomOperations.BuildRom(outromfile, files);
            
            return 0;
        }

        private static int RomBuildFromDir(ReadOnlySpan<string> args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-build-from-dir [outrom] [indir]");
                return 1;
            }

            var outrom = args[0];
            var indir = args[1];

            var infiles = Directory.EnumerateFiles(indir, "*", SearchOption.AllDirectories);

            var files = infiles.Select(_ => (_, "/" + Path.GetRelativePath(indir, _)
                    .Replace(Path.DirectorySeparatorChar, '/')
                )).ToArray();

            using var outromfile = File.Create(outrom);
            
            ShinRomOperations.BuildRom(outromfile, files);
            
            return 0;
        }

        private class ActionList
        {
            public delegate int Action(ReadOnlySpan<string> args);
            public delegate int SingleFileProcessingAction(ReadOnlyMemory<byte> input, string inputname, string output, 
                ImmutableArray<string> options);

            private Dictionary<string, Action> actions;

            public ActionList()
            {
                actions = new Dictionary<string, Action>();
            }

            public void AddAction(string name, Action action)
            {
                actions.Add(name, action);
            }

            public IEnumerable<string> AllActions => actions.Keys;

            /// <summary>
            /// Adds a pair of actions associated with the given processing function
            /// One that processes from a rom file (prefixed with rom-), and one that processes from an extracted file
            /// </summary>
            public void AddSingleFileProcessingAction(string name, string inputName, SingleFileProcessingAction action)
            {
                actions.Add(name, (args) =>
                {
                    var options = new List<string>();
                    while (args.Length > 0 && args[0].StartsWith("--"))
                    {
                        options.Add(args[0]);
                        args = args[1..];
                    }

                    if (args.Length != 2)
                    {
                        Console.Error.WriteLine($"Usage: ShinDataUtil {name} {{--options...}} [{inputName}file] [outname]");
                        return 1;
                    }

                    var inname = args[0];
                    var outname = args[1];

                    var indata = File.ReadAllBytes(inname);

                    return action(indata, inname, outname, options.ToImmutableArray());
                });

                actions.Add($"rom-{name}", (args) =>
                {
                    var options = new List<string>();
                    while (args.Length > 0 && args[0].StartsWith("--"))
                    {
                        options.Add(args[0]);
                        args = args[1..];
                    }
                    
                    if (args.Length != 3)
                    {
                        Console.Error.WriteLine($"Usage: ShinDataUtil rom-{name} [romname] [{inputName}name] [outname]");
                        return 1;
                    }

                    var romname = args[0];
                    var inname = args[1];
                    var outname = args[2];

                    using var archive = (ReadableGameArchive)new FileReadableGameArchive(romname);
                    using var infile = archive.OpenFile(inname);

                    return action(infile.Data, inname, outname, options.ToImmutableArray());
                });
            }

            public int Execute(string actionname, ReadOnlySpan<string> args)
            {
                if (!actions.TryGetValue(actionname, out Action? action))
                {
                    Console.Error.WriteLine("Unknown action");
                    return -1;
                }
                return action(args);
            }
        }

        private static int MainWrap(string[] args)
        {
            var actions = new ActionList();

            actions.AddAction("rom-list", ListFiles);
            actions.AddAction("rom-extract-all", _ => ExtractAllFiles(_, false, false));
            actions.AddAction("rom-extract-all-with-decode", _ => ExtractAllFiles(_, true, false));
            actions.AddSingleFileProcessingAction("pic-decode", "pic", DecodePicture);
            actions.AddSingleFileProcessingAction("dump-pic-fragments", "pic", DumpPictureFragments);
            actions.AddSingleFileProcessingAction("sound-remux", "nxa", RemuxSound);
            actions.AddAction("lz77-test", Lz77Test);
            actions.AddAction("txa-encode", TxaEncode);
            actions.AddAction("pic-encode", PicEncode);
            actions.AddAction("pic-encode-roundtrip", PicEncodeRoundtrip);
            actions.AddSingleFileProcessingAction("font-extract", "fnt", FontExtract);
            actions.AddSingleFileProcessingAction("txa-extract", "txa", TxaExtract);
            actions.AddSingleFileProcessingAction("sysse-extract", "sysse", SysseExtract);
            actions.AddSingleFileProcessingAction("bustup-extract", "bup", BustupExtract);
            actions.AddSingleFileProcessingAction("mask-extract", "msk", MaskExtract);
            actions.AddSingleFileProcessingAction("scenario-decompile", "snr", ScenarioDecompile);
            actions.AddAction("scenario-build", ScenarioBuild);
            actions.AddAction("scenario-layout", ScenarioLayout);
            actions.AddAction("rom-replace-file", RomReplaceFile);
            actions.AddAction("rom-build", RomBuild);
            actions.AddAction("rom-build-from-dir", RomBuildFromDir);

            if (args.Length < 1 || args[0] == "help" || args[0] == "-h" || args[0] == "--help")
            {
                Console.Error.WriteLine("Usage: ShinDataUtil [action] [action_args]");
                Console.Error.WriteLine("Actions:\n" + string.Join("\n", actions.AllActions));
                return 1;
            }

            return actions.Execute(args[0], args[1..]);
        }

        static int Main(string[] args)
        {
            try
            {
                return MainWrap(args);
            }
            finally
            {
                NonBlockingConsole.Flush();
                NonBlockingConsole.Stop();
            }
        }
    }
}
