using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ShinDataUtil.Common;
using ShinDataUtil.Decompression;

namespace ShinDataUtil.Compression
{
    /// <summary>
    /// Implements various mutations to rom file
    /// </summary>
    public static class ShinRomOperations
    {
        /// <summary>
        /// Quickly replace a certain file in rom
        /// Does some hacks, leaves old file contents, so it should not be used repeatedly
        /// </summary>
        public static void ReplaceFile(string inromPath, Stream outrom, Stream injfile, string targetName)
        {
            using var inrom = File.OpenRead(inromPath);
            using var readableRom = new FileReadableGameArchive(inromPath);
            
            var header = new RomHeader();
            inrom.Read(SpanUtil.AsBytes(ref header));

            outrom.Seek(0, SeekOrigin.Begin);
            inrom.Seek(0, SeekOrigin.Begin);
            inrom.CopyTo(outrom);
            
            var fileEntry = readableRom.FindFile(targetName);
            var rawEntry = new RomEntry();
            var rawEntryOffset = Marshal.SizeOf<RomHeader>() + fileEntry.SourceOffset;
            var rawEntryBytesSpan = SpanUtil.AsBytes(ref rawEntry);
            
            outrom.Seek(rawEntryOffset, SeekOrigin.Begin);
            outrom.Read(rawEntryBytesSpan);
            outrom.Seek(rawEntryOffset, SeekOrigin.Begin);

            var newDataOffset = outrom.Length;
            while (newDataOffset % header.offsetMultiplier != 0)
                newDataOffset++;

            rawEntry.RawDataOffset = newDataOffset / header.offsetMultiplier;
            rawEntry.DataSize = (int)injfile.Length;
            
            outrom.Write(rawEntryBytesSpan);

            outrom.Seek(newDataOffset, SeekOrigin.Begin);
            
            injfile.CopyTo(outrom);
        }

        /// <summary>
        /// Build a rom from specified IFileProvider's
        /// </summary>
        public static unsafe void BuildRom(Stream outrom, IEnumerable<(IFileProvider source, string targetName)> files)
        {
            const int directoryOffsetMultiplier = 16;
            
            /* build index */
            DirectoryEntry root = new DirectoryEntry();
            
            root.Children.Add((".", root));
            root.Children.Add(("..", root));

            void AddEntry(IFileProvider source, string targetName)
            {
                if (targetName[0] != '/')
                    throw new ArgumentException("Entry name must start with a '/'.", nameof(targetName));
                if (targetName.Any(c => c >= 128))
                    throw new ArgumentException("Entry name must be ascii-only.");
                var parts = targetName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var currentEntry = root;
                foreach (var part in parts[..^1])
                {
                    DirectoryEntry? foundEntry = null;
                    foreach (var (name, child) in currentEntry.Children)
                    {
                        if (name == part) 
                            foundEntry = (DirectoryEntry)child;
                    }

                    if (foundEntry == null)
                    {
                        foundEntry = new DirectoryEntry();
                        foundEntry.Children.Add((".", foundEntry));
                        foundEntry.Children.Add(("..", currentEntry));
                        currentEntry.Children.Add((part, foundEntry));
                    }

                    currentEntry = foundEntry;
                }

                if (currentEntry.Children.Any(_ => _.name == parts[^1]))
                    throw new ArgumentException("Duplicate file names in archive");
                
                currentEntry.Children.Add((parts[^1], new FileEntry(source)));
            }

            List<DirectoryEntry> dirLinearizedEntries = new List<DirectoryEntry>();
            List<FileEntry> fileLinearizedEntries = new List<FileEntry>();

            void RecurLinearizeEntry(DirectoryEntry entry)
            {
                if (entry.Seen)
                    return;
                entry.Seen = true;
                dirLinearizedEntries.Add(entry);
                foreach (var (name, childEntry) in entry.Children)
                    if (childEntry is DirectoryEntry directoryEntry)
                        RecurLinearizeEntry(directoryEntry);
                    else
                        fileLinearizedEntries.Add((FileEntry)childEntry);
            }
            
            long dataSizeEstimate = dirLinearizedEntries.Count * 64;

            foreach (var (source, targetName) in files
                    // the game requires lexicographical ordering inside the directory
                    // I __think__ this will make it =)
                .OrderBy(x => x.targetName))
            {
                AddEntry(source, targetName);
                dataSizeEstimate += source.GetApproximateSize();
            }

            RecurLinearizeEntry(root);

            /* start filling the header */
            var header = new RomHeader
            {
                magic = RomHeader.Magic,
                version = RomHeader.Version,
                offsetMultiplier = (uint)((dataSizeEstimate + uint.MaxValue) / uint.MaxValue)
            };
            
            /* put the index with placeholder offsets and sizes */
            var currentOffset = sizeof(RomHeader);
            foreach (var entry in dirLinearizedEntries)
            {
                while (currentOffset % directoryOffsetMultiplier != 0)
                    currentOffset++;
                
                outrom.Seek(currentOffset, SeekOrigin.Begin);
                var count = entry.Children.Count;
                entry.Offset = currentOffset;
                
                outrom.Write(SpanUtil.AsReadOnlyBytes(ref count));
                var nameOffset = 4 + sizeof(RomEntry) * count;
                
                foreach (var (i, name, fentry) in entry.Children.Select((x, i) => (i, x.name, x.entry)))
                {
                    outrom.Seek(currentOffset + 4 + sizeof(RomEntry) * i, SeekOrigin.Begin);
                    var rawEntry = new RomEntry
                    {
                        NameOffset = nameOffset, 
                        IsDirectory = fentry is DirectoryEntry
                    };

                    outrom.Write(SpanUtil.AsBytes(ref rawEntry));
                    outrom.Seek(currentOffset + nameOffset, SeekOrigin.Begin);

                    var buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(name.Length + 1));
                    try
                    {
                        var used = Encoding.UTF8.GetBytes(name, buffer);
                        buffer[used] = 0;
                        used++;
                        outrom.Write(buffer[..used]);
                        nameOffset += used;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                entry.Size = nameOffset;
                currentOffset += nameOffset;
            }

            header.indexLength = (uint)(currentOffset - sizeof(RomHeader));

            /* put the header */
            outrom.Seek(0, SeekOrigin.Begin);
            outrom.Write(SpanUtil.AsBytes(ref header));

            var currentOffsetLong = sizeof(RomHeader) + header.indexLength;
            
            /* put the data */
            foreach (var fileEntry in fileLinearizedEntries)
            {
                while (currentOffsetLong % header.offsetMultiplier != 0)
                    currentOffsetLong++;
                outrom.Seek(currentOffsetLong, SeekOrigin.Begin);
                
                fileEntry.Offset = currentOffsetLong;
                using var fileStream = fileEntry.Source.OpenRead();
                fileEntry.Size = (int)fileStream.Length;
                
                fileStream.CopyTo(outrom);

                currentOffsetLong += fileEntry.Size;
            }

            /* fill the offsets */
            foreach (var entry in dirLinearizedEntries)
            {
                outrom.Seek(entry.Offset + 4, SeekOrigin.Begin);
                foreach (var (name, fentry) in entry.Children)
                {
                    var rawEntry = new RomEntry();
                    outrom.Read(SpanUtil.AsBytes(ref rawEntry));

                    rawEntry.DataSize = fentry.Size;
                    if (fentry is DirectoryEntry)
                        rawEntry.RawDataOffset = (fentry.Offset - sizeof(RomHeader)) / directoryOffsetMultiplier;
                    else
                        rawEntry.RawDataOffset = fentry.Offset / header.offsetMultiplier;

                    outrom.Seek(-sizeof(RomEntry), SeekOrigin.Current);
                    outrom.Write(SpanUtil.AsBytes(ref rawEntry));
                }
            }
        }

        /// <summary>
        /// Build a rom from specified files
        /// </summary>
        public static void BuildRom(Stream outrom, IEnumerable<(string srcName, string targetName)> files) =>
            BuildRom(outrom, files.Select(_ => (IFileProvider.FromFile(_.srcName), _.targetName)));
        
        // Data types used to build index for the rom
        
        private abstract class Entry
        {
            public long Offset { get; set; }
            public int Size { get; set; }
        }
        
        private class DirectoryEntry : Entry
        {
            public bool Seen { get; set; }
            public List<(string name, Entry entry)> Children { get; } = new List<(string name, Entry entry)>();
        }

        private class FileEntry : Entry
        {
            public FileEntry(IFileProvider source)
            {
                Source = source;
            }
            public IFileProvider Source { get; set; }
        }
        
        // Abstract way to represent file source
        public interface IFileProvider
        {
            public static IFileProvider FromFile(string path) => new FsFileProvider(path);
            Stream OpenRead();
            int GetApproximateSize();
        }
        
        public class FsFileProvider : IFileProvider
        {
            private readonly string _path;
            public FsFileProvider(string path)
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException();
                _path = path;
            }

            public Stream OpenRead() => File.OpenRead(_path);
            public int GetApproximateSize() => (int) new FileInfo(_path).Length;
        }
        
    }
}