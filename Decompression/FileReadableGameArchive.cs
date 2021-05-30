using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using ShinDataUtil.Common;

namespace ShinDataUtil.Decompression
{
    /// <summary>
    /// Implements reading operations on rom file
    /// </summary>
    // TODO: refactor this mess
    public class FileReadableGameArchive : ReadableGameArchive, IDisposable
    {
        private const int DirectoryOffsetMultiplier = 16;
        private readonly MemoryMappedFile _memoryMappedFile;
        private readonly uint _offsetMultiplier;
        private readonly MemoryMappedViewAccessor _fileAccessor;
        private readonly ReadOnlyMemory<byte> _indexMemory;
        private readonly FileStream _fileStream;
        
        static FileReadableGameArchive()
        {
            Trace.Assert(BitConverter.IsLittleEndian);
        }
        
        public FileReadableGameArchive(string path) : this(File.OpenRead(path))
        { }

        public unsafe FileReadableGameArchive(FileStream fileStream)
        {
            _memoryMappedFile = MemoryMappedFile.CreateFromFile(fileStream, null, 0, MemoryMappedFileAccess.Read,
                HandleInheritability.None, true);
            _fileStream = fileStream;

            var headerSize = Marshal.SizeOf<RomHeader>();

            _fileAccessor = _memoryMappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            // TODO: replace memory mapping with plain reads (it's more effective anyways)
            var headerSpan = new MemoryMappedViewMemoryManager(_fileAccessor, 0, headerSize).GetSpan();
            var header = MemoryMarshal.Read<RomHeader>(headerSpan);
            
            if (header.magic != RomHeader.Magic) // ROM2
                throw new InvalidDataException("Invalid magic.");
            if (header.version != RomHeader.Version)
                throw new InvalidDataException("Unknown version.");

            _offsetMultiplier = header.offsetMultiplier;

            _indexMemory = new MemoryMappedViewMemoryManager(_fileAccessor, headerSize, (int)header.indexLength).Memory;

            //_indexAccessor = _memoryMappedFile.CreateViewAccessor(headerSize, header.indexLength);

            byte* p = null; 
            _fileAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
        }
        
        public override DirectoryEntry RootEntry => new DirectoryEntry("", -1,0, -1);

        private string ReadName(int offset)
        {
            var ms = new MemoryStream();
            while (true)
            {
                var c = _indexMemory.Span[offset];
                offset++;
                if (c == 0)
                    break;
                ms.WriteByte(c);
            }
            return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ms.GetBuffer(), 0, (int)ms.Length));
        }

        private IEnumerable<FileEntry> TraverseDirectory(string path, int offset)
        {
            /* should create _a bit_ less GC pressure that solution using EnumerateEntries,
                so code duplication is a performance trade off... */
            var entryCount = MemoryMarshal.Read<int>(_indexMemory.Span[offset..]);
            for (var i = 2; i < entryCount; i++)
                /* entries #0 and #1 are . and .. (useless), so they are skipped here */
            {
                var sourceOffset = offset + 4 + i * Marshal.SizeOf<RomEntry>();
                var entry = MemoryMarshal.Read<RomEntry>(_indexMemory.Span[sourceOffset..]);
                //_indexAccessor.Read(offset + 4 + i * Marshal.SizeOf<Entry>(), out Entry entry);
                var name = ReadName(offset + entry.NameOffset);
                var fullPath = path + "/" + name;

                if (entry.IsDirectory)
                    foreach (var filename in TraverseDirectory(fullPath, 
                        (int)entry.RawDataOffset * DirectoryOffsetMultiplier))
                        yield return filename;
                else
                    yield return new FileEntry(fullPath, 
                        sourceOffset,
                        entry.RawDataOffset * _offsetMultiplier, entry.DataSize);
            }
        }
        
        public override IEnumerable<FileEntry> EnumerateAllFiles()
        {
            return TraverseDirectory("", 0);
        }

        public override IEnumerable<Entry> EnumerateEntries(DirectoryEntry entry)
        {
            var entryCount = MemoryMarshal.Read<int>(_indexMemory.Span[(int)entry.DataOffset..]);
            //_indexAccessor.Read(entry.Offset, out int entryCount);
            for (int i = 2; i < entryCount; i++)
                /* entries #0 and #1 are . and .. (useless), so they are skipped here */
            {
                var sourceOffset = (int)entry.DataOffset + 4 + i * Marshal.SizeOf<RomEntry>();
                var e = MemoryMarshal.Read<RomEntry>(_indexMemory.Span[sourceOffset..]);
                //_indexAccessor.Read(entry.Offset + 4 + i * Marshal.SizeOf<Entry>(), out Entry e);
                var name = ReadName((int)entry.DataOffset + e.NameOffset);
                var fullPath = entry.Path + "/" + name;

                if (e.IsDirectory)
                    yield return new DirectoryEntry(fullPath, sourceOffset,
                        e.RawDataOffset * DirectoryOffsetMultiplier, entry.Size);
                else
                    yield return new FileEntry(fullPath, sourceOffset,
                        e.RawDataOffset * _offsetMultiplier, e.DataSize);
            }
        }

        public override ShinFile OpenFile(FileEntry entry)
        {
            lock(_fileStream)
                return new CachedShinFile(_fileStream, entry.DataOffset, entry.Size);
        }

        public override void Dispose()
        {
            _fileAccessor?.Dispose();
            _memoryMappedFile?.Dispose();
            _fileStream?.Dispose();
        }

        private unsafe class MemoryMappedViewMemoryManager : MemoryManager<byte>
        {
            private readonly byte* _ptr;
            private readonly int _size;

            public MemoryMappedViewMemoryManager(MemoryMappedViewAccessor accessor, long offset, int size)
            {
                _size = size;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _ptr);
                _ptr += accessor.PointerOffset + offset;
            }

            protected override void Dispose(bool disposing)
            {
            }

            public override Span<byte> GetSpan()
            {
                return new Span<byte>(_ptr, _size);
            }

            public override MemoryHandle Pin(int elementIndex = 0)
            {
                return new MemoryHandle();
            }
            

            public override void Unpin()
            {
            }
        }
    }
}