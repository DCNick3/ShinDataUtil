using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FastPngEncoderSharp;
using ShinDataUtil.Compression;
using ShinDataUtil.Compression.Scenario;
using ShinDataUtil.Decompression;
using ShinDataUtil.Decompression.Scenario;
using ShinDataUtil.Scenario;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
                            NonBlockingConsole.WriteLine("IMG  {0}", file.Path);
                            var image = ShinPictureDecoder.DecodePicture(s.Data.Span);
                            if (!dryRun)
                            {
                                FastPngEncoder.WritePngToFile(basePath + ".png", image);
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

        static int DecodePicture(ReadOnlyMemory<byte> picdata, string _, string outname)
        {
            var image = ShinPictureDecoder.DecodePicture(picdata.Span);
            
            FastPngEncoder.WritePngToFile(outname, image);
            
            //using var fd = File.OpenWrite(outname);
            //image.SaveAsPng(fd);

            return 0;
        }

        static int RemuxSound(ReadOnlyMemory<byte> nxadata, string nxaname, string outname)
        {
            using var outstream = File.OpenWrite(outname);

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

        static int FontExtract(ReadOnlyMemory<byte> fntdata, string _, string outname)
        {
            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinFontExtractor.Extract(fntdata.Span, outname);
            
            return 0;
        }

        static int TxaExtract(ReadOnlyMemory<byte> txadata, string _, string outname)
        {
            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinTxaExtractor.Extract(txadata.Span, outname);
            
            return 0;
        }

        static int SysseExtract(ReadOnlyMemory<byte> syssedata, string _, string outname)
        {
            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinSysseExtractor.Extract(syssedata.Span, outname);
            
            return 0;
        }

        static int BustupExtract(ReadOnlyMemory<byte> bupdata, string _, string outname)
        {
            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinBustupExtractor.Extract(bupdata.Span, outname);

            return 0;
        }

        private static int MaskExtract(ReadOnlyMemory<byte> mskdata, string _, string outname)
        {
            var image = ShinMaskDecompress.Decompress(mskdata.Span);
            
            FastPngEncoder.WritePngToFile(outname, image);
            
            return 0;
        }

        private static int ScenarioDecompile(ReadOnlyMemory<byte> snrdata, string _, string outname)
        {
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

        private static int LZLRDecompress(ReadOnlySpan<string> args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil lzlr-decompress [infile] [outfile]");
                return 1;
            }

            var infile = args[0];
            var outfile = args[1];

            var inData = File.ReadAllBytes(infile);

            var decompressor = new ShinLZLRDecompressor();

            var outData = decompressor.Decompress(inData);

            File.WriteAllBytes(outfile, outData.ToArray());

            return 0;
        }


        private static int LZLRCompress(ReadOnlySpan<string> args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil lzlr-compress [infile] [outfile]");
                return 1;
            }

            var infile = args[0];
            var outfile = args[1];

            var inData = File.ReadAllBytes(infile);

            var compressor = new ShinLZLRCompressor();

            var outData = compressor.Compress(inData);

            File.WriteAllBytes(outfile, outData.ToArray());

            return 0;
        }

        private static int TexDecode(ReadOnlySpan<string> args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil tex-decode [intex] [outpng]");
                return 1;
            }

            var intex = args[0];
            var outpng = args[1];

            var intexData = File.ReadAllBytes(intex);

            var image = DungeonTexDecoder.DecodeTex(intexData, true);
            
            FastPngEncoder.WritePngToFile(outpng, image);
            
            return 0;
        }

        private static int TexEncode(ReadOnlySpan<string> args)
        {
            var format = NVNTexFormat.NVN_FORMAT_BPTC_UNORM;

            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil tex-encode [inpng] [outtex] [--format] [--lzlr]");
                Console.Error.WriteLine("--lzlr     compress output with LZLR");
                Console.Error.WriteLine("--format   specify out texture format");
                Console.Error.WriteLine($"           default: {format}");
                Console.Error.WriteLine("           available formats:");
                foreach (var fmt in NVNTexture.GetAvailableFormats())
                {
                Console.Error.WriteLine($"           {(uint)fmt}  {fmt}");
                }
                
                return 1;
            }

            var inpng = args[0];
            var outtex = args[1];

            if (args.Contains("--format"))
            {
                Int32.TryParse(args[args.IndexOf("--format") + 1], out var fmt);
                format = (NVNTexFormat)fmt;
            }

            if (!NVNTexture.GetAvailableFormats().Contains(format))
            {
                Console.Error.WriteLine("specified format not supported");
                return 1;
            }

            var inpngData = Image.Load<Rgba32>(inpng);

            var texData = DungeonTexEncoder.Encode(inpngData, format, 1);

            if (args.Contains("--lzlr"))
            {
                var compressor = new ShinLZLRCompressor();
                texData = compressor.Compress(texData);
            }

            File.WriteAllBytes(outtex, texData.ToArray());

            return 0;
        }

        private static int TXPLExtract(ReadOnlySpan<string> args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil txpl-extract [txl|tlz file] [outfolder] [--save-sprites]");
                Console.Error.WriteLine("--save-sprites     save sprites to png files");
                return 1;
            }

            var inTXPL = args[0];
            var outname = args[1];

            bool extractSprites = false;
            if (args.Contains("--save-sprites"))
            {
                extractSprites = true;
            }

            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);

            ShinTexpoolExtractor.Extract(inTXPL, outname, extractSprites);

            return 0;
        }

        private static int TXPLBuild(ReadOnlySpan<string> args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil txpl-build [infolder] [outfile] [--lzlr]");
                Console.Error.WriteLine("--lzlr     compress output with LZLR");
                return 1;
            }

            var indir = args[0];
            var outname = args[1];

            ShinTexpoolBuilder.BuildFromDirectory(indir, outname);

            if (args.Contains("--lzlr"))
            {
                var data = File.ReadAllBytes(outname);
                var compressor = new ShinLZLRCompressor();
                File.WriteAllBytes(outname, compressor.Compress(data).ToArray());
            }

            return 0;
        }

        private class ActionList
        {
            public delegate int Action(ReadOnlySpan<string> args);
            public delegate int SingleFileProcessingAction(ReadOnlyMemory<byte> input, string inputname, string output);

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
                    if (args.Length != 2)
                    {
                        Console.Error.WriteLine($"Usage: ShinDataUtil {name} [{inputName}file] [outname]");
                        return 1;
                    }

                    var inname = args[0];
                    var outname = args[1];

                    var indata = File.ReadAllBytes(inname);

                    return action(indata, inname, outname);
                });

                actions.Add($"rom-{name}", (args) =>
                {
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

                    return action(infile.Data, inname, outname);
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
            actions.AddAction("rom-extract-all", (args_) => ExtractAllFiles(args_, false, false));
            actions.AddAction("rom-extract-all-with-decode", (args_) => ExtractAllFiles(args_, true, false));
            actions.AddSingleFileProcessingAction("pic-decode", "pic", DecodePicture);
            actions.AddSingleFileProcessingAction("sound-remux", "nxa", RemuxSound);
            actions.AddAction("lz77-test", Lz77Test);
            actions.AddSingleFileProcessingAction("font-extract", "fnt", FontExtract);
            actions.AddSingleFileProcessingAction("txa-extract", "txa", TxaExtract);
            actions.AddSingleFileProcessingAction("sysse-extract", "sysse", SysseExtract);
            actions.AddSingleFileProcessingAction("bustup-extract", "bup", BustupExtract);
            actions.AddSingleFileProcessingAction("mask-extract", "msk", MaskExtract);
            actions.AddSingleFileProcessingAction("scenario-decompile", "snr", ScenarioDecompile);
            actions.AddAction("scenario-build", ScenarioBuild);
            actions.AddAction("rom-replace-file", RomReplaceFile);
            actions.AddAction("rom-build", RomBuild);
            actions.AddAction("lzlr-decompress", LZLRDecompress);
            actions.AddAction("lzlr-compress", LZLRCompress);
            actions.AddAction("tex-decode", TexDecode);
            actions.AddAction("tex-encode", TexEncode);
            actions.AddAction("txpl-extract", TXPLExtract);
            actions.AddAction("txpl-build", TXPLBuild);

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
