using System;
using System.Collections.Generic;
using System.Linq;

namespace ShinDataUtil.Decompression
{
    public abstract class ReadableGameArchive : IDisposable
    {
        public abstract IEnumerable<FileEntry> EnumerateAllFiles();
        public abstract IEnumerable<Entry> EnumerateEntries(DirectoryEntry entry);
        public abstract DirectoryEntry RootEntry { get; }
        
        public Entry FindEntry(string path)
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var entry = RootEntry;

            foreach (var part in parts[..^1])
                entry = (DirectoryEntry) EnumerateEntries(entry).Single(e => e.Name == part);

            return (FileEntry) EnumerateEntries(entry).Single(e => e.Name == parts[^1]);
        }
        
        public FileEntry FindFile(string path)
        {
            var e = FindEntry(path);
            return (FileEntry) e;
        }

        public DirectoryEntry FindDirectory(string path)
        {
            var e = FindEntry(path);
            return (DirectoryEntry) e;
        }
        
        public abstract ShinFile OpenFile(FileEntry entry);

        public ShinFile OpenFile(string filename) => OpenFile(FindFile(filename));

        // TODO: If a logic similar to mounting is needed (like in game) than Entry class should become more abstract
        // (no offset and size for you)
        public abstract class Entry
        {
            internal Entry(string path, long sourceOffset, long dataOffset, int size)
            {
                Path = path;
                SourceOffset = sourceOffset;
                DataOffset = dataOffset;
                Size = size;
            }
            public long SourceOffset { get; }
            public string Path { get; }
            public long DataOffset { get; }
            public int Size { get; }
            public string Name => Path.Split('/', StringSplitOptions.RemoveEmptyEntries)[^1];
        }

        public class FileEntry : Entry
        {
            public FileEntry(string path, long sourceOffset, long dataOffset, int size) : base(path, sourceOffset, dataOffset, size)
            {
            }
        }

        public class DirectoryEntry : Entry
        {
            public DirectoryEntry(string path, long sourceOffset, long dataOffset, int size) : base(path, sourceOffset, dataOffset, size)
            {
            }
        }

        public abstract void Dispose();
    }
}