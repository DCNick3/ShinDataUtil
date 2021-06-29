using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Xml;
using Newtonsoft.Json;
using ShinDataUtil.Common;
using ShinDataUtil.Decompression;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using Formatting = Newtonsoft.Json.Formatting;

namespace ShinDataUtil.Compression
{
    public class ShinPictureEncoder
    {
        private const int MagicWidth = 256;
        private const int MagicHeight = 128;

        static bool IsBorderEmptyV(Image<Rgba32> image, int x, int y0, int height)
        {
            for (var j = y0; j < y0 + height && j < image.Height; j++)
                if (image[x, j].A > 0)
                    return false;

            return true;
        }
        
        static bool IsBorderEmptyH(Image<Rgba32> image, int x0, int y, int width)
        {
            for (var i = x0; i < x0 + width && i < image.Width; i++)
                if (image[i, y].A > 0)
                    return false;

            return true;
        }

        public static unsafe void EncodePicture(Stream outpic, Image<Rgba32> image, 
            int effectiveWidth, int effectiveHeight, uint pictureId, Origin origin)
        {
            Trace.Assert(effectiveWidth > 0 && effectiveHeight > 0);

            // Split image into fragments
            // this algorithm does not match the one used originally __exactly__, but seems to give close results
            // probably the order of checks and size transformations is different in the tool
            // this is good enough for me :shrug:
            int minX = int.MaxValue,
                maxX = int.MinValue,
                minY = int.MaxValue,
                maxY = int.MinValue;
            
            Trace.Assert(effectiveHeight <= image.Height);
            Trace.Assert(effectiveWidth <= image.Width);
            
            for (var j = 0; j < effectiveHeight; j++)
            {
                var row = image.GetPixelRowSpan(j);
                for (var i = 0; i < effectiveWidth; i++)
                    if (row[i].A > 0)
                    {
                        minX = Math.Min(minX, i);
                        minY = Math.Min(minY, j);
                        maxX = Math.Max(maxX, i);
                        maxY = Math.Max(maxY, j);
                    }
            }

            var preliminaryFragments = new List<Rectangle>();

            for (var j = minY; j <= maxY; j += MagicHeight)
            for (var i = minX; i <= maxX; i += MagicWidth)
            {
                var h = Math.Min(j + MagicHeight, effectiveHeight) - j;
                
                while (i <= maxX && IsBorderEmptyV(image, i, j, h))
                    i++;
                if (i > maxX)
                    break;
                
                var w = Math.Min(i + MagicWidth, effectiveWidth) - i;
                
                preliminaryFragments.Add(new Rectangle(i, j, w, h));
            }

            var fragments = preliminaryFragments
                .Select(frag =>
                {
                    // make sure it's in the image bounds
                    var shrinkY = frag.Bottom - Math.Min(frag.Bottom, maxY + 1);
                    var shrinkX = frag.Right - Math.Min(frag.Right, maxX + 1);
                    frag.Width -= shrinkX;
                    frag.Height -= shrinkY;
                    if (frag.Width <= 0 && frag.Height <= 0)
                        return frag;

                    // shrink the top
                    while (frag.Width > 0 && frag.Height > 0 && IsBorderEmptyH(image, frag.X, frag.Top, frag.Width))
                    {
                        frag.Y++;
                        frag.Height--;
                    }

                    // shrink the bottom
                    while (frag.Width > 0 && frag.Height > 0 && IsBorderEmptyH(image, frag.X, frag.Bottom - 1, frag.Width))
                    {
                        frag.Height--;
                    }

                    // shrink the left
                    while (frag.Width > 0 && frag.Height > 0 && IsBorderEmptyV(image, frag.Left, frag.Y, frag.Height))
                    {
                        frag.X++;
                        frag.Width--;
                    }

                    // shrink the right
                    while (frag.Width > 0 && frag.Height > 0 && IsBorderEmptyV(image, frag.Right - 1, frag.Y, frag.Height))
                    {
                        frag.Width--;
                    }

                    return frag;
                })
                .Where(frag => frag.Width > 0 && frag.Height > 0)
                .ToImmutableArray();

            // we have one more padding to add: in case if fragment does not have directly adjacent fragments to the right or bottom,
            //   it's width or height (respectively) needs to be incremented

            // unfortunately linear, which makes the algorithm O(n^2)
            // but hey, nobody will pass huge pictures here, right?..
            bool CheckAdj(bool isBottom, Rectangle rect)
            {
                var rectButt = isBottom
                    ? new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1)
                    : new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height);
                    //? new Rectangle(rect.X + 1, rect.Bottom - 1, rect.Width - 2, 1)
                    //: new Rectangle(rect.Right - 1, rect.Y + 1, 1, rect.Height - 2);

                Trace.Assert(rectButt.Width > 0 && rectButt.Height > 0);
                
                for (var i = 0; i < fragments.Length; i++)
                    if (fragments[i] != rect && fragments[i].IntersectsWith(rectButt))
                        return true;
                return false;
            }

            fragments = fragments
                .Select(frag =>
                {
                    // add another bit of padding to match the original encoder results
                    frag.X -= 1;
                    frag.Width += 2;
                    frag.Y -= 1;
                    frag.Height += 2;
                    if (frag.X < 0)
                        frag.X = 0;
                    if (frag.Y <= 0)
                        frag.Y = 0;
                    return frag;
                })
                .Select(frag =>
                {
                    // hello, O(n^2), my old friend...
                    if (!CheckAdj(false, frag))
                        frag.Width++;
                    if (!CheckAdj(true, frag))
                        frag.Height++;
                    return frag;
                }).ToImmutableArray();
            
            //Console.WriteLine(JsonConvert.SerializeObject(fragments.Select(f => new
            //    {f.X, f.Y, f.Width, f.Height}), Formatting.Indented));

            var (originX, originY) = origin switch
            {
                Origin.TopLeft => (0, 0),
                Origin.Top => (effectiveWidth / 2, 0),
                Origin.TopRight => (effectiveWidth, 0),
                Origin.Left => (0, effectiveHeight / 2),
                Origin.Center => (effectiveWidth / 2, effectiveHeight / 2),
                Origin.Right => (effectiveWidth, effectiveHeight / 2),
                Origin.BottomLeft => (0, effectiveHeight),
                Origin.Bottom => (effectiveWidth / 2, effectiveHeight),
                Origin.BottomRight => (effectiveWidth, effectiveHeight),
                _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
            };
            
            var header = new PicHeader
            {
                magic = 0x34434950,
                version = 2,
                // fileSize!
                effectiveHeight = checked((ushort)effectiveHeight),
                effectiveWidth = checked((ushort)effectiveWidth),
                entryCount = checked((ushort)fragments.Length),
                originX = checked((ushort)originX),
                originY = checked((ushort)originY),
                field20 = 1, // this value is set in __most__ pictures, excluding __some__ from /picture/e/ directory
                pictureId = pictureId
            };

            var dataOffset = sizeof(PicHeader) + fragments.Length * sizeof(PicHeaderFragmentEntry);
            var currentOffset = dataOffset;
            outpic.Seek(dataOffset, SeekOrigin.Begin);

            var fragmentEntries = new List<PicHeaderFragmentEntry>();

            foreach (var frag in fragments)
            {
                var p1 = outpic.Position;
                var sz = ShinTextureCompress.EncodeImageFragment(outpic, image, frag.X, frag.Y,
                    0, 0, frag.Width, frag.Height);
                var p2 = outpic.Position;
                Debug.Assert(p2 - p1 == sz);
                fragmentEntries.Add(new PicHeaderFragmentEntry
                {
                    x = checked((ushort)frag.X),
                    y = checked((ushort)frag.Y),
                    offset = checked((uint)currentOffset),
                    size = checked((uint)sz),
                });
                currentOffset += sz;
            }
            
            Trace.Assert(currentOffset == outpic.Length);

            var fragmentEntriesArray = fragmentEntries.ToImmutableArray();

            outpic.Seek(0, SeekOrigin.Begin);
            header.fileSize = checked((uint)currentOffset);
            outpic.Write(SpanUtil.AsBytes(ref header));
            outpic.Write(MemoryMarshal.Cast<PicHeaderFragmentEntry, byte>(fragmentEntriesArray.AsSpan()));
            
            Trace.Assert(dataOffset == outpic.Position);
        }

        public enum Origin
        {
            TopLeft = 1,
            Top = 2,
            TopRight = 3,
            Left = 4,
            Center = 5,
            Right = 6,
            BottomLeft = 7,
            Bottom = 8,
            BottomRight = 9
        }
    }
}