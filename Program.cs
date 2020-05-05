using System;
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

        static int RomDecodePicture(ReadOnlySpan<string> args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-pic-decode [romname] [picname] [outname]");
                return 1;
            }

            var romname = args[0];
            var picname = args[1];
            var outname = args[2];
            
            using var archive = (ReadableGameArchive)new FileReadableGameArchive(romname);
            using var picfile = archive.OpenFile(picname);

            var image = ShinPictureDecoder.DecodePicture(picfile.Data.Span);
            
            FastPngEncoder.WritePngToFile(outname, image);
            
            //using var fd = File.OpenWrite(outname);
            //image.SaveAsPng(fd);

            return 0;
        }

        static int RomRemuxSound(ReadOnlySpan<string> args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-sound-remux [romname] [nxaname] [outname]");
                return 1;
            }

            var romname = args[0];
            var nxaname = args[1];
            var outname = args[2];
            
            using var archive = (ReadableGameArchive)new FileReadableGameArchive(romname);
            using var nxafile = archive.OpenFile(nxaname);
            using var outstream = File.OpenWrite(outname);

            ShinOpusRemuxer.Remux(nxafile.Data.Span, outstream, nxaname);

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

        static int RomFontExtract(ReadOnlySpan<string> args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-font-extract [romname] [fntname] [outname]");
                return 1;
            }

            var romname = args[0];
            var fntname = args[1];
            var outname = args[2];
            
            using var archive = (ReadableGameArchive)new FileReadableGameArchive(romname);
            using var fntfile = archive.OpenFile(fntname);

            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinFontExtractor.Extract(fntfile.Data.Span, outname);
            
            return 0;
        }

        static int RomTxaExtract(ReadOnlySpan<string> args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-txa-extract [romname] [txaname] [outname]");
                return 1;
            }

            var romname = args[0];
            var txaname = args[1];
            var outname = args[2];

            using var archive = (ReadableGameArchive)new FileReadableGameArchive(romname);
            using var txafile = archive.OpenFile(txaname);

            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinTxaExtractor.Extract(txafile.Data.Span, outname);
            
            return 0;
        }

        static int RomSysseExtract(ReadOnlySpan<string> args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-txa-extract [romname] [syssename] [outname]");
                return 1;
            }

            var romname = args[0];
            var syssename = args[1];
            var outname = args[2];

            using var archive = (ReadableGameArchive)new FileReadableGameArchive(romname);
            using var syssefile = archive.OpenFile(syssename);

            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinSysseExtractor.Extract(syssefile.Data.Span, outname);
            
            return 0;
        }

        static int RomBustupExtract(ReadOnlySpan<string> args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-bustup-extract [romname] [bupname] [outname]");
                return 1;
            }

            var romname = args[0];
            var bupname = args[1];
            var outname = args[2];
            
            using var archive = (ReadableGameArchive)new FileReadableGameArchive(romname);
            using var bupfile = archive.OpenFile(bupname);

            if (Directory.Exists(outname))
                Directory.Delete(outname, true);
            Directory.CreateDirectory(outname);
            
            ShinBustupExtractor.Extract(bupfile.Data.Span, outname);

            return 0;
        }

        private static int RomMaskExtract(ReadOnlySpan<string> args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-mask-extract [romname] [mskname] [outname]");
                return 1;
            }

            var romname = args[0];
            var mskname = args[1];
            var outname = args[2];
            
            using var archive = (ReadableGameArchive)new FileReadableGameArchive(romname);
            using var mskfile = archive.OpenFile(mskname);

            var image = ShinMaskDecompress.Decompress(mskfile.Data.Span);
            
            FastPngEncoder.WritePngToFile(outname, image);
            
            return 0;
        }

        private static int RomScenarioDecompile(ReadOnlySpan<string> args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil rom-scenario-decompile [romname] [snrname] [outname]");
                return 1;
            }

            var romname = args[0];
            var snrname = args[1];
            var outname = args[2];
            
            using var archive = (ReadableGameArchive)new FileReadableGameArchive(romname);
            using var snrfile = archive.OpenFile(snrname);

            ShinScenarioDecompiler.Decompile(snrfile.Data, outname);
            
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

        private static int MainWrap(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: ShinDataUtil [action] [action_args]");
                return 1;
            }

            var action = args[0];
            switch (action)
            {
                case "rom-list":
                    return ListFiles(args[1..]);
                case "rom-extract-all":
                    return ExtractAllFiles(args[1..], false, false);
                case "rom-extract-all-with-decode":
                    return ExtractAllFiles(args[1..], true, false);
                case "rom-pic-decode":
                    return RomDecodePicture(args[1..]);
                case "rom-sound-remux":
                    return RomRemuxSound(args[1..]);
                case "lz77-test":
                    return Lz77Test(args[1..]);
                case "rom-font-extract":
                    return RomFontExtract(args[1..]);
                case "rom-txa-extract":
                    return RomTxaExtract(args[1..]);
                case "rom-sysse-extract":
                    return RomSysseExtract(args[1..]);
                case "rom-bustup-extract":
                    return RomBustupExtract(args[1..]);
                case "rom-mask-extract":
                    return RomMaskExtract(args[1..]);
                case "rom-scenario-decompile":
                    return RomScenarioDecompile(args[1..]);
                case "scenario-build":
                    return ScenarioBuild(args[1..]);
                case "rom-replace-file":
                    return RomReplaceFile(args[1..]);
                case "rom-build":
                    return RomBuild(args[1..]);
                default:
                    Console.Error.WriteLine("Unknown action");
                    return 1;
            }
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