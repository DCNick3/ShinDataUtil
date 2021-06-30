using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinDataUtil.Compression;
using ShinDataUtil.Decompression;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Processing;

namespace NUnitTests
{
    public class PicTests
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

        [Test]
        [Parallelizable(ParallelScope.All)]
        [TestCaseSource(typeof(PicTestData))]
        public void RoundTripLossy(string romFilename)
        {
            var gameArchive = SharedData.Instance.GameArchive;

            using var file = gameArchive.OpenFile(romFilename);
            var (image, (effectiveWidth, effectiveHeight), _) = ShinPictureDecoder.DecodePicture(file.Data.Span);

            var imageCropped = image.Clone();
            imageCropped.Mutate(o => o.Crop(effectiveWidth, effectiveHeight));
            
            using var ms = new MemoryStream();
            
            ShinPictureEncoder.EncodePicture(ms, imageCropped, effectiveWidth, effectiveHeight, 0,
                ShinPictureEncoder.Origin.Bottom, new ShinTextureCompress.FragmentCompressionConfig
                {
                    Quantize = true,
                    Dither = true,
                    LosslessAlpha = true
                });

            var (imageRedec, (effectiveWidth1, effectiveHeight1), _) = ShinPictureDecoder.DecodePicture(ms.GetBuffer().AsSpan()[..(int)ms.Length]);
            
            Assert.AreEqual(effectiveWidth1, effectiveWidth);
            Assert.AreEqual(effectiveHeight1, effectiveHeight);

            var mse = 0.0;
            for (var j = 0; j < effectiveHeight; j++)
            {
                var span1 = imageCropped.GetPixelRowSpan(j)[..effectiveWidth];
                var span2 = imageRedec.GetPixelRowSpan(j)[..effectiveWidth];
                if (span1.SequenceEqual(span2))
                    continue;
                for (var i = 0; i < effectiveWidth; i++)
                    mse += (span1[i].ToVector4() - span2[i].ToVector4()).LengthSquared();
            }

            mse /= effectiveWidth * effectiveHeight;
            
            // MSE not greater than this value. Allows the error to be up to 1 per each pixel per channel
            Assert.Less(mse, 0.0003 /* 3 * 10 ^ -4, should be quite conservative */);
        }
        
        [Test]
        [TestCaseSource(typeof(PicTestData))]
        [Parallelizable(ParallelScope.All)]
        public void RoundTrip(string romFilename)
        {
            var gameArchive = SharedData.Instance.GameArchive;

            using var file = gameArchive.OpenFile(romFilename);
            var (image, (effectiveWidth, effectiveHeight), _) = ShinPictureDecoder.DecodePicture(file.Data.Span);

            var imageCropped = image.Clone();
            imageCropped.Mutate(o => o.Crop(effectiveWidth, effectiveHeight));
            
            using var ms = new MemoryStream();
            
            ShinPictureEncoder.EncodePicture(ms, imageCropped, effectiveWidth, effectiveHeight, 0,
                ShinPictureEncoder.Origin.Bottom, new ShinTextureCompress.FragmentCompressionConfig
                {
                    Quantize = false,
                    Dither = false
                });

            var (imageRedec, (effectiveWidth1, effectiveHeight1), _) = ShinPictureDecoder.DecodePicture(ms.GetBuffer().AsSpan()[..(int)ms.Length]);
            
            Assert.AreEqual(effectiveWidth1, effectiveWidth);
            Assert.AreEqual(effectiveHeight1, effectiveHeight);

            for (var j = 0; j < effectiveHeight; j++)
            {
                var span1 = imageCropped.GetPixelRowSpan(j)[..effectiveWidth];
                var span2 = imageRedec.GetPixelRowSpan(j)[..effectiveWidth];
                if (span1.SequenceEqual(span2))
                    continue;
                for (var i = 0; i < effectiveWidth; i++)
                    Assert.AreEqual(span2[i], span1[i],
                        $"Expected equality of the round-trip decoded images at row {j}, column {i}");
            }
        }
    }
}