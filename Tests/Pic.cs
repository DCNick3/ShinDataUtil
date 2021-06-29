using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShinDataUtil.Compression;
using ShinDataUtil.Decompression;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using Xunit;

namespace UnitTests
{
    public class Pic
    {
        public class PicTestData : IEnumerable<object[]>
        {
            IEnumerable<string> Enum()
            {
                var gameArchive = SharedData.Instance.GameArchive;

                foreach (var entry in gameArchive.EnumerateAllFiles())
                    if (entry.Name.EndsWith(".pic"))
                        yield return entry.Path;
            }
            
            public IEnumerator<object[]> GetEnumerator()
            {
                return Enum().Select(v => new object[] {v}).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        
        [Theory]
        [ClassData(typeof(PicTestData))]
        void RoundTrip(string romFilename)
        {
            var gameArchive = SharedData.Instance.GameArchive;

            using var file = gameArchive.OpenFile(romFilename);
            var (image, (effectiveWidth, effectiveHeight), _) = ShinPictureDecoder.DecodePicture(file.Data.Span);

            using var ms = new MemoryStream();
            
            ShinPictureEncoder.EncodePicture(ms, image, effectiveWidth, effectiveHeight, 0);

            var (imageRedec, (effectiveWidth1, effectiveHeight1), _) = ShinPictureDecoder.DecodePicture(ms.GetBuffer().AsSpan()[..(int)ms.Length]);
            
            Assert.Equal(effectiveWidth1, effectiveWidth);
            Assert.Equal(effectiveHeight1, effectiveHeight);

            for (var j = 0; j < effectiveHeight; j++)
            {
                var span1 = image.GetPixelRowSpan(j)[..effectiveWidth];
                var span2 = imageRedec.GetPixelRowSpan(j)[..effectiveWidth];
                Assert.True(span1.SequenceEqual(span2), $"Expected equality of the round-trip decoded images at row {j}");
            }
        }
    }
}