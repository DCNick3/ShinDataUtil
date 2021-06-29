using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FastPngEncoderSharp;
using ShinDataUtil.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace ShinDataUtil.Decompression
{
    public static unsafe class ShinBustupExtractor
    {

        public static void Extract(ReadOnlySpan<byte> data, string destinationDirectory)
        {
            checked
            {
                var header = MemoryMarshal.Read<Header>(data);

                Trace.Assert(header.magic == 877679938);
                Trace.Assert(header.version == 3);
                Trace.Assert(header.file_size == data.Length);

                var basePictureElements = MemoryMarshal.Cast<byte, FileLocation>(
                    data.Slice(sizeof(Header), (int)header.base_picture_elements_count)
                );

                var maxX = (int) header.viewport_width;
                var maxY = (int) header.viewport_height;

                foreach (var pictureElement in basePictureElements)
                {
                    var entryData = data.Slice(
                        (int) pictureElement.offset, 
                        (int) pictureElement.size);

                    var (offsetX, offsetY) = ShinTextureDecompress.GetImageFragmentOffset(entryData);
                    var (width, height) = ShinTextureDecompress.GetImageFragmentSize(entryData);

                    maxX = Math.Max(offsetX + width, maxX);
                    maxY = Math.Max(offsetY + height, maxY);
                }

                var image = new Image<Rgba32>(maxX, maxY);

                foreach (var entry in basePictureElements)
                {
                    var entryData = data.Slice((int) entry.offset, (int) entry.size);

                    var (offsetX, offsetY) = ShinTextureDecompress.GetImageFragmentOffset(entryData);
                    var (width, height) = ShinTextureDecompress.GetImageFragmentSize(entryData);

                    var (vertices, _, _) =
                        ShinTextureDecompress.DecodeImageFragment(image, offsetX, offsetY, entryData);
                    CleanupVertices(image, offsetX, offsetY, width, height, vertices);
                }

                var path = Path.Combine(destinationDirectory, "body.png");
                FastPngEncoder.WritePngToFile(path, image, (header.viewport_width, header.viewport_height));

                var unusedEntriesOffset = (int) (sizeof(Header) + header.base_picture_elements_count * sizeof(FileLocation));
                var emotionsEntriesOffset = (int) (unusedEntriesOffset + header.unused_entries_count * sizeof(UnusedEntry));

                var emotionsData =
                    data.Slice(emotionsEntriesOffset, (int)header.emotion_entry_count * sizeof(EmotionHeader));
                var emotions = MemoryMarshal.Cast<byte, EmotionHeader>(emotionsData);

                Image<Rgba32>? DecodeImage(ReadOnlySpan<byte> imageData)
                {
                    if (imageData.Length == 0)
                        return null;
                    var (width, height) = ShinTextureDecompress.GetImageFragmentSize(imageData);
                    var (offsetX, offsetY) = ShinTextureDecompress.GetImageFragmentOffset(imageData);
                    var imageLocal = new Image<Rgba32>(Math.Max(offsetX + width, header.viewport_width),
                        Math.Max(offsetY + height, header.viewport_height));

                    var (vertices, _, _) =
                        ShinTextureDecompress.DecodeImageFragment(imageLocal, offsetX, offsetY, imageData);
                    CleanupVertices(imageLocal, offsetX, offsetY, width, height, vertices);

                    return imageLocal;
                }

                foreach (var emotion in emotions)
                {
                    var emotionName = Marshal.PtrToStringUTF8(new IntPtr(emotion.name)) ?? throw new Exception("fuck");

                    var faceEntry = emotion.location.Slice(data);
                    var unusedEntry1 = emotion.fl1_1.Slice(data);
                    var unusedEntry2 = emotion.fl1_2.Slice(data);
                    var unusedEntry3 = emotion.fl1_2.Slice(data);
                    var mouthEntry1 = emotion.fl2_1.Slice(data);
                    var mouthEntry2 = emotion.fl2_2.Slice(data);
                    var mouthEntry3 = emotion.fl2_2.Slice(data);

                    var anotherData = data.Slice(checked((int) (
                        unusedEntriesOffset + emotion.unused_offset * 0xc
                    )), 12);
                    var anotherHeader = MemoryMarshal.Read<UnusedEntry>(anotherData);
                    Trace.Assert(emotion.unused_offset == 0);
                    Trace.Assert(anotherHeader.location.offset == 0);

                    var faceImage = DecodeImage(faceEntry);

                    var unusedImage1 = DecodeImage(unusedEntry1);
                    var unusedImage2 = DecodeImage(unusedEntry2);
                    var unusedImage3 = DecodeImage(unusedEntry3);
                    var mouthImage1 = DecodeImage(mouthEntry1);
                    var mouthImage2 = DecodeImage(mouthEntry2);
                    var mouthImage3 = DecodeImage(mouthEntry3);

                    Trace.Assert(unusedImage1 == null);
                    Trace.Assert(unusedImage2 == null);
                    Trace.Assert(unusedImage3 == null);

                    if (unusedEntry1.Length > 0)
                        Debugger.Break();

                    void WriteImage(Image<Rgba32>? imageLocal, string suffix)
                    {
                        if (imageLocal == null)
                            return;
                        path = Path.Combine(destinationDirectory, $"{emotionName}_{suffix}.png");
                        FastPngEncoder.WritePngToFile(path, imageLocal,
                            (header.viewport_width, header.viewport_height));
                    }

                    WriteImage(faceImage, "face");

                    WriteImage(mouthImage1, "mouth_1");
                    WriteImage(mouthImage2, "mouth_2");
                    WriteImage(mouthImage3, "mouth_3");
                }

                /*
                 * <header>
                 * <base picture elements>
                 * <unused entry elements>
                 * <emotion entry elements>
                 * <data>
                 */
            }
        }

        private static void CleanupVertices(Image<Rgba32> image,
            int offsetX, int offsetY, int width, int height,
            IEnumerable<PicVertexEntry> vertices)
        {
            var ba = new BitArray(width * height, false);
            foreach (var vertex in vertices)
                for (int i = vertex.fromX; i < vertex.toX; i++)
                for (int j = vertex.fromY; j < vertex.toY; j++)
                    ba[i + j * width] = true;

            for (var j = offsetY; j < offsetY + height; j++)
            {
                var row = image.GetPixelRowSpan(j);
                for (var i = offsetX; i < offsetX + width; i++)
                {
                    if (!ba[i - offsetX + (j - offsetY) * width])
                    //if (!vertices.Any(_ => _.Contains(i - offsetX, j - offsetY)))
                        row[i] = Rgba32.Transparent;
                }
            }
        }

#pragma warning disable 649
        // ReSharper disable InconsistentNaming
        private struct UnusedEntry
        {
            public uint id;
            public FileLocation location;
        }
        
        private struct FileLocation
        {
            public uint offset;
            public uint size;

            public readonly ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> data)
            {
                // If offset == 0, size must be == 0
                Trace.Assert(offset != 0 || size == 0);
                
                return checked(data[
                    (int)offset
                        ..
                    (int)(offset + size)
                ]);
            }
        }

        private struct EmotionHeader
        {
            public fixed byte name[0x10];
            public uint unused_offset;
            public FileLocation location;
            
            // Unused
            public FileLocation fl1_1;
            public FileLocation fl1_2;
            public FileLocation fl1_3;
            
            // Mouth
            public FileLocation fl2_1;
            public FileLocation fl2_2;
            public FileLocation fl2_3;
        }
        
        private struct Header
        {
            public uint magic;
            public uint version;
            public uint file_size;
            public ushort f_12;
            public ushort f_14;
            public ushort viewport_width;
            public ushort viewport_height;
            public uint f_20;
            public uint base_picture_elements_count;
            public uint emotion_entry_count;
            public uint unused_entries_count;
            public uint identifier;
            public ulong well;
        }
#pragma warning restore 649
        // ReSharper restore InconsistentNaming
    }
}