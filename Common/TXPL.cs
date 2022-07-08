using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace ShinDataUtil.Common
{
    public static class TXPL
    {
        public struct Header
        {
            public uint Magic;
            public uint TexpoolInfoOffset;
            public uint TexpoolAndSpriteInfoSize;

            public static uint HeaderSize => sizeof(int) * 3;

            public static uint DefaultMagic => 0x4C505854;
            public ReadOnlySpan<byte> GetTexInfoData(ReadOnlySpan<byte> txplData) =>
                txplData.Slice(checked((int)HeaderSize), checked((int)(TexpoolInfoOffset - HeaderSize)));

            public ReadOnlySpan<byte> GetTexpoolInfoData(ReadOnlySpan<byte> txplData) =>
                txplData.Slice(checked((int)TexpoolInfoOffset), checked((int)TexpoolInfo.SelfSize));

            public ReadOnlySpan<byte> GetSpriteInfoData(ReadOnlySpan<byte> txplData) =>
                txplData.Slice(checked((int)(TexpoolInfoOffset + TexpoolInfo.SelfSize)),
                    checked((int)(TexpoolAndSpriteInfoSize - TexpoolInfo.SelfSize)));
        }

        public struct TexpoolInfo
        {
            public uint texWidth;
            public uint texHeight;
            public uint texCount;
            public uint spriteCount;

            public static uint SelfSize => sizeof(uint) * 4;
        }

        public struct TexData
        {
            public uint offset;
            public uint size;

            public static uint SelfSize => sizeof(uint) * 2;
        }

        public struct Sprite
        {
            
            public ushort layer { get; set; }
            public ushort texNum { get; set; }
            public ushort x { get; set; }
            public ushort y { get; set; }
            public ushort width { get; set; }
            public ushort height { get; set; }

            public static uint SelfSize => sizeof(ushort) * 6;

            public Sprite(SpriteJson sprite)
            {
                layer = sprite.layer;
                this.texNum = sprite.texNum;
                width = sprite.width;
                height = sprite.height;
                x = sprite.x;
                y = sprite.y;
            }
        }

        public struct SpriteJson
        {
            public int index { get; set; }
            public ushort layer { get; set; }
            [JsonIgnore]
            public ushort texNum { get; set; }
            public ushort x { get; set; }
            public ushort y { get; set; }
            public ushort width { get; set; }
            public ushort height { get; set; }

            public SpriteJson(Sprite sprite, int index)
            {
                this.index = index;
                layer = sprite.layer;
                texNum = sprite.texNum;
                width = sprite.width;
                height = sprite.height; 
                x = sprite.x;
                y = sprite.y;
            }
        }

        public struct TexMetadata
        {
            public TexMetadata(NVNTexFormat format, uint levels)
            {
                this.format = format;
                this.levels = levels;
            }

            public NVNTexFormat format { get; set; }
            public uint levels { get; set; }
        }

        public class Description
        {
            public uint texWidth { get; set; }
            public uint texHeight { get; set; }
            public List<TexMetadata> texMetadatas { get; set; }
            public List<SpriteJson[]> sprites { get; set; }

            public Description() {}

            public Description(uint texWidth, uint texHeight, TexHeader[] textures, Sprite[] sprites)
            {
                this.texWidth = texWidth;
                this.texHeight = texHeight;

                this.sprites = new List<SpriteJson[]>();
                texMetadatas = new List<TexMetadata>();

                // Add index to sprites just to be sure, if they will be shuffled
                var jsonSprites = sprites.Select((x, i) => new SpriteJson(x, i));
                foreach (var (index, tex) in textures.Select((x, i) => (i, x)))
                {
                    var metadata = new TexMetadata(tex.Format, tex.Levels);
                    texMetadatas.Add(metadata);
                    var texSprites = jsonSprites.Where((sprite) => sprite.texNum == index);
                    this.sprites.Add(texSprites.ToArray());
                }
            }

            public Sprite[] GetSprites()
            {
                // Read sprites from json and sort them by index field, then convert to "binary" sprite struct
                var spritesJsonFlatten = new List<TXPL.SpriteJson>();
                foreach (var (index, texSprites) in this.sprites.Select((x, i) => (i, x)))
                {
                    var range = texSprites.Select(x => { x.texNum = (ushort)index; return x; });
                    spritesJsonFlatten.AddRange(range);
                }
                return spritesJsonFlatten.OrderBy(x => x.index).Select(x => new TXPL.Sprite(x)).ToArray();
            }
        }
    }
}
