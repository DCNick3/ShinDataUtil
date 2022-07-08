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
        public struct TXPLHeader
        {
            public uint Magic;
            public uint TexpoolInfoOffset;
            public uint TexpoolAndSpriteInfoSize;

            public static uint HeaderSize => sizeof(int) * 3;


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
        }

        public struct Sprite
        {
            public ushort layer { get; set; }
            public ushort texNum { get; set; }
            public ushort x { get; set; }
            public ushort y { get; set; }
            public ushort width { get; set; }
            public ushort height { get; set; }
        }

        public class TXPLDescription
        {
            public uint texWidth { get; set; }
            public uint texHeight { get; set; }
            public uint texCount { get; set; }

            public List<Sprite[]> sprites { get; set; }

            public TXPLDescription(uint texWidth, uint texHeight, uint texCount, Sprite[] sprites)
            {
                this.texWidth = texWidth;
                this.texHeight = texHeight;
                this.texCount = texCount;

                this.sprites = new List<Sprite[]>();
                for (int i = 0; i < texCount; i++)
                {
                    var texSprites = sprites.Where((sprite) => sprite.texNum == i);
                    this.sprites.Add(texSprites.ToArray());
                }
            }
        }
    }
}
