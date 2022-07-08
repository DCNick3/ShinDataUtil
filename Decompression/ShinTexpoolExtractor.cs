using FastPngEncoderSharp;
using ShinDataUtil.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Collections.Generic;

namespace ShinDataUtil.Decompression
{
    public static class ShinTexpoolExtractor
    {
        public static void Extract(string inTXPL, string outname, bool extractSprites)
        {
            var TXPLData = new ReadOnlySpan<byte>(File.ReadAllBytes(inTXPL));

            if (DungeonLzlrDecompressor.CheckHeader(ref TXPLData))
            {
                var compressor = new DungeonLzlrDecompressor();
                TXPLData = compressor.Decompress(TXPLData);
            }

            var header = MemoryMarshal.Read<TXPL.Header>(TXPLData);

            Trace.Assert(header.Magic == TXPL.Header.DefaultMagic);

            var texturesInfo = MemoryMarshal.Cast<byte, TXPL.TexData>(header.GetTexInfoData(TXPLData));
            var texpoolInfo = MemoryMarshal.Read<TXPL.TexpoolInfo>(header.GetTexpoolInfoData(TXPLData));
            var spritesInfo = MemoryMarshal.Cast<byte, TXPL.Sprite>(header.GetSpriteInfoData(TXPLData)).ToArray();

            var texHeaders = new List<TexHeader>();

            foreach (var (index, texInfo) in texturesInfo.ToArray().Select((x, i) => (i, x)))
            {
                // Last texture info always has size = 0, maybe used to store file size 
                if (texInfo.size != 0)
                {
                    var texData = TXPLData.Slice((int)texInfo.offset, (int)texInfo.size);

                    //Also in "Umineko: Golden Fantasia" there are DDS stored in this format
                    var texHeader = MemoryMarshal.Read<TexHeader>(texData);
                    var image = DungeonTexDecoder.DecodeTex(texData);
                    texHeaders.Add(texHeader);

                    var pngPath = Path.Combine(outname, $"{index:000}.png");
                    FastPngEncoder.WritePngToFile(pngPath, image);
                    
                    if (extractSprites)
                    {
                        Console.WriteLine($"Saving sprites from texture {index}...");

                        var spriteDirPath = Path.Combine(outname, $"{index:000}");
                        Directory.CreateDirectory(spriteDirPath);

                        
                        foreach (var (sprIndex, sprite) in spritesInfo.Where((x) => x.texNum == index).Select((x, i) => (i, x)))
                        {
                            var img = image.Clone();
                            var spriteRect = new Rectangle(sprite.x, sprite.y, sprite.width, sprite.height);
                            img.Mutate(ctx => ctx.Crop(spriteRect));

                            var spritePath = Path.Combine(spriteDirPath, $"{sprIndex:000}.png");

                            FastPngEncoder.WritePngToFile(spritePath, img);
                        }
                    }
                }
            }

            var desc = new TXPL.Description(texpoolInfo.texWidth, texpoolInfo.texHeight, texHeaders.ToArray(), spritesInfo);

            var options = new JsonSerializerOptions();
            options.WriteIndented = true;
            var descString = JsonSerializer.Serialize(desc, options);

            var jsonPath = Path.Combine(outname, "texpool.json");
            File.WriteAllText(jsonPath, descString);
        }
    }
}
