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
            public TexMetadata[] texMetadatas { get; set; }
            public Sprite[] sprites { get; set; }

            [JsonConstructor]
            public Description(uint texWidth, uint texHeight, TexMetadata[] texMetadatas, Sprite[] sprites) {
                this.texWidth = texWidth;
                this.texHeight = texHeight;

                this.sprites = sprites;
                this.texMetadatas = texMetadatas;
            }

            public Description(uint texWidth, uint texHeight, TexHeader[] textures, Sprite[] sprites)
            {
                this.texWidth = texWidth;
                this.texHeight = texHeight;

                this.sprites = sprites;
                texMetadatas = textures.Select(x => new TexMetadata(x.Format, x.Levels)).ToArray();
            }
        }
    }
}
