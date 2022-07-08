using BCnEncoder.Encoder;
using ShinDataUtil.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShinDataUtil.Compression
{
    public static class ShinTexpoolBuilder
    {
        public static void BuildFromDirectory(string indir, string outfile)
        {
            var jsonPath = Path.Combine(indir, "texpool.json");
            var jsonString = File.ReadAllText(jsonPath);
            var desc = JsonSerializer.Deserialize<TXPL.Description>(jsonString);

            if (desc == null)
            {
                throw new ApplicationException("corrrupted or wrong texpool.json file");
            }

            if (File.Exists(outfile))
            {
                File.Delete(outfile);
            }

            var outStream = File.OpenWrite(outfile);
            var sprites = desc.GetSprites();


            var txplHeader = new TXPL.Header();
            txplHeader.Magic = TXPL.Header.DefaultMagic;
            txplHeader.TexpoolInfoOffset = (uint)(TXPL.Header.HeaderSize + (desc.texMetadatas.Count + 1) * TXPL.TexData.SelfSize);
            txplHeader.TexpoolAndSpriteInfoSize = (uint)(sprites.Length * TXPL.Sprite.SelfSize + TXPL.TexpoolInfo.SelfSize);

            outStream.Write(SpanUtil.AsReadOnlyBytes(ref txplHeader));

            //Space for texture data to write later
            outStream.Write(new byte[(desc.texMetadatas.Count + 1) * TXPL.TexData.SelfSize]);

            var txplInfo = new TXPL.TexpoolInfo();
            txplInfo.texWidth = desc.texWidth;
            txplInfo.texHeight = desc.texHeight;
            txplInfo.texCount = (uint)desc.texMetadatas.Count;
            txplInfo.spriteCount = (uint)sprites.Length;
            outStream.Write(SpanUtil.AsReadOnlyBytes(ref txplInfo));

            for (int i = 0; i < sprites.Length; i++)
            {
                outStream.Write(SpanUtil.AsReadOnlyBytes(ref sprites[i]));
            }

            var encoder = new BcEncoder();
            foreach (var (index, texture) in desc.texMetadatas.Select((x, i) => (i, x)))
            {
                var texPath = Path.Combine(indir, $"{index:000}.png");


                var image = Image.Load<Rgba32>(texPath);

                Trace.Assert(image.Width == desc.texWidth && image.Height == desc.texHeight);

                var texData = DungeonTexEncoder.Encode(image, texture.format, texture.levels);

                var texInfo = new TXPL.TexData();
                texInfo.offset = (uint)outStream.Position;
                texInfo.size = (uint)texData.Length;

                outStream.Write(texData);

                outStream.Seek(TXPL.Header.HeaderSize + index * TXPL.TexData.SelfSize, SeekOrigin.Begin);
                outStream.Write(SpanUtil.AsReadOnlyBytes(ref texInfo));

                outStream.Seek(0, SeekOrigin.End);
            }
            var lastTexInfo = new TXPL.TexData();
            lastTexInfo.offset = (uint)outStream.Position;
            lastTexInfo.size = 0;

            outStream.Seek(TXPL.Header.HeaderSize + desc.texMetadatas.Count * TXPL.TexData.SelfSize, SeekOrigin.Begin);
            outStream.Write(SpanUtil.AsReadOnlyBytes(ref lastTexInfo));

            outStream.Close();
        }
    }
}
