using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCnEncoder.Shared;

namespace ShinDataUtil
{
    // Enum source
    // https://codeviewer.zeldamods.org/uking/uking/lib/NintendoSDK/include/nvn/nvn_types.h.html
    public enum NVNTexFormat : uint
    {
        NVN_FORMAT_NONE = 0x0,
        NVN_FORMAT_R8 = 0x1,
        NVN_FORMAT_R8SN = 0x2,
        NVN_FORMAT_R8UI = 0x3,
        NVN_FORMAT_R8I = 0x4,
        NVN_FORMAT_R16F = 0x5,
        NVN_FORMAT_R16 = 0x6,
        NVN_FORMAT_R16SN = 0x7,
        NVN_FORMAT_R16UI = 0x8,
        NVN_FORMAT_R16I = 0x9,
        NVN_FORMAT_R32F = 0xA,
        NVN_FORMAT_R32UI = 0xB,
        NVN_FORMAT_R32I = 0xC,
        NVN_FORMAT_RG8 = 0xD,
        NVN_FORMAT_RG8SN = 0xE,
        NVN_FORMAT_RG8UI = 0xF,
        NVN_FORMAT_RG8I = 0x10,
        NVN_FORMAT_RG16F = 0x11,
        NVN_FORMAT_RG16 = 0x12,
        NVN_FORMAT_RG16SN = 0x13,
        NVN_FORMAT_RG16UI = 0x14,
        NVN_FORMAT_RG16I = 0x15,
        NVN_FORMAT_RG32F = 0x16,
        NVN_FORMAT_RG32UI = 0x17,
        NVN_FORMAT_RG32I = 0x18,
        NVN_FORMAT_RGB8 = 0x19,
        NVN_FORMAT_RGB8SN = 0x1A,
        NVN_FORMAT_RGB8UI = 0x1B,
        NVN_FORMAT_RGB8I = 0x1C,
        NVN_FORMAT_RGB16F = 0x1D,
        NVN_FORMAT_RGB16 = 0x1E,
        NVN_FORMAT_RGB16SN = 0x1F,
        NVN_FORMAT_RGB16UI = 0x20,
        NVN_FORMAT_RGB16I = 0x21,
        NVN_FORMAT_RGB32F = 0x22,
        NVN_FORMAT_RGB32UI = 0x23,
        NVN_FORMAT_RGB32I = 0x24,
        NVN_FORMAT_RGBA8 = 0x25,
        NVN_FORMAT_RGBA8SN = 0x26,
        NVN_FORMAT_RGBA8UI = 0x27,
        NVN_FORMAT_RGBA8I = 0x28,
        NVN_FORMAT_RGBA16F = 0x29,
        NVN_FORMAT_RGBA16 = 0x2A,
        NVN_FORMAT_RGBA16SN = 0x2B,
        NVN_FORMAT_RGBA16UI = 0x2C,
        NVN_FORMAT_RGBA16I = 0x2D,
        NVN_FORMAT_RGBA32F = 0x2E,
        NVN_FORMAT_RGBA32UI = 0x2F,
        NVN_FORMAT_RGBA32I = 0x30,
        NVN_FORMAT_STENCIL8 = 0x31,
        NVN_FORMAT_DEPTH16 = 0x32,
        NVN_FORMAT_DEPTH24 = 0x33,
        NVN_FORMAT_DEPTH32F = 0x34,
        NVN_FORMAT_DEPTH24_STENCIL8 = 0x35,
        NVN_FORMAT_DEPTH32F_STENCIL8 = 0x36,
        NVN_FORMAT_RGBX8_SRGB = 0x37,
        NVN_FORMAT_RGBA8_SRGB = 0x38,
        NVN_FORMAT_RGBA4 = 0x39,
        NVN_FORMAT_RGB5 = 0x3A,
        NVN_FORMAT_RGB5A1 = 0x3B,
        NVN_FORMAT_RGB565 = 0x3C,
        NVN_FORMAT_RGB10A2 = 0x3D,
        NVN_FORMAT_RGB10A2UI = 0x3E,
        NVN_FORMAT_R11G11B10F = 0x3F,
        NVN_FORMAT_RGB9E5F = 0x40,
        NVN_FORMAT_RGB_DXT1 = 0x41,
        NVN_FORMAT_RGBA_DXT1 = 0x42,
        NVN_FORMAT_RGBA_DXT3 = 0x43,
        NVN_FORMAT_RGBA_DXT5 = 0x44,
        NVN_FORMAT_RGB_DXT1_SRGB = 0x45,
        NVN_FORMAT_RGBA_DXT1_SRGB = 0x46,
        NVN_FORMAT_RGBA_DXT3_SRGB = 0x47,
        NVN_FORMAT_RGBA_DXT5_SRGB = 0x48,
        NVN_FORMAT_RGTC1_UNORM = 0x49,
        NVN_FORMAT_RGTC1_SNORM = 0x4A,
        NVN_FORMAT_RGTC2_UNORM = 0x4B,
        NVN_FORMAT_RGTC2_SNORM = 0x4C,
        NVN_FORMAT_BPTC_UNORM = 0x4D,
        NVN_FORMAT_BPTC_UNORM_SRGB = 0x4E,
        NVN_FORMAT_BPTC_SFLOAT = 0x4F,
        NVN_FORMAT_BPTC_UFLOAT = 0x50,
        NVN_FORMAT_R8_UI2F = 0x51,
        NVN_FORMAT_R8_I2F = 0x52,
        NVN_FORMAT_R16_UI2F = 0x53,
        NVN_FORMAT_R16_I2F = 0x54,
        NVN_FORMAT_R32_UI2F = 0x55,
        NVN_FORMAT_R32_I2F = 0x56,
        NVN_FORMAT_RG8_UI2F = 0x57,
        NVN_FORMAT_RG8_I2F = 0x58,
        NVN_FORMAT_RG16_UI2F = 0x59,
        NVN_FORMAT_RG16_I2F = 0x5A,
        NVN_FORMAT_RG32_UI2F = 0x5B,
        NVN_FORMAT_RG32_I2F = 0x5C,
        NVN_FORMAT_RGB8_UI2F = 0x5D,
        NVN_FORMAT_RGB8_I2F = 0x5E,
        NVN_FORMAT_RGB16_UI2F = 0x5F,
        NVN_FORMAT_RGB16_I2F = 0x60,
        NVN_FORMAT_RGB32_UI2F = 0x61,
        NVN_FORMAT_RGB32_I2F = 0x62,
        NVN_FORMAT_RGBA8_UI2F = 0x63,
        NVN_FORMAT_RGBA8_I2F = 0x64,
        NVN_FORMAT_RGBA16_UI2F = 0x65,
        NVN_FORMAT_RGBA16_I2F = 0x66,
        NVN_FORMAT_RGBA32_UI2F = 0x67,
        NVN_FORMAT_RGBA32_I2F = 0x68,
        NVN_FORMAT_RGB10A2SN = 0x69,
        NVN_FORMAT_RGB10A2I = 0x6A,
        NVN_FORMAT_RGB10A2_UI2F = 0x6B,
        NVN_FORMAT_RGB10A2_I2F = 0x6C,
        NVN_FORMAT_RGBX8 = 0x6D,
        NVN_FORMAT_RGBX8SN = 0x6E,
        NVN_FORMAT_RGBX8UI = 0x6F,
        NVN_FORMAT_RGBX8I = 0x70,
        NVN_FORMAT_RGBX16F = 0x71,
        NVN_FORMAT_RGBX16 = 0x72,
        NVN_FORMAT_RGBX16SN = 0x73,
        NVN_FORMAT_RGBX16UI = 0x74,
        NVN_FORMAT_RGBX16I = 0x75,
        NVN_FORMAT_RGBX32F = 0x76,
        NVN_FORMAT_RGBX32UI = 0x77,
        NVN_FORMAT_RGBX32I = 0x78,
        NVN_FORMAT_RGBA_ASTC_4x4 = 0x79,
        NVN_FORMAT_RGBA_ASTC_5x4 = 0x7A,
        NVN_FORMAT_RGBA_ASTC_5x5 = 0x7B,
        NVN_FORMAT_RGBA_ASTC_6x5 = 0x7C,
        NVN_FORMAT_RGBA_ASTC_6x6 = 0x7D,
        NVN_FORMAT_RGBA_ASTC_8x5 = 0x7E,
        NVN_FORMAT_RGBA_ASTC_8x6 = 0x7F,
        NVN_FORMAT_RGBA_ASTC_8x8 = 0x80,
        NVN_FORMAT_RGBA_ASTC_10x5 = 0x81,
        NVN_FORMAT_RGBA_ASTC_10x6 = 0x82,
        NVN_FORMAT_RGBA_ASTC_10x8 = 0x83,
        NVN_FORMAT_RGBA_ASTC_10x10 = 0x84,
        NVN_FORMAT_RGBA_ASTC_12x10 = 0x85,
        NVN_FORMAT_RGBA_ASTC_12x12 = 0x86,
        NVN_FORMAT_RGBA_ASTC_4x4_SRGB = 0x87,
        NVN_FORMAT_RGBA_ASTC_5x4_SRGB = 0x88,
        NVN_FORMAT_RGBA_ASTC_5x5_SRGB = 0x89,
        NVN_FORMAT_RGBA_ASTC_6x5_SRGB = 0x8A,
        NVN_FORMAT_RGBA_ASTC_6x6_SRGB = 0x8B,
        NVN_FORMAT_RGBA_ASTC_8x5_SRGB = 0x8C,
        NVN_FORMAT_RGBA_ASTC_8x6_SRGB = 0x8D,
        NVN_FORMAT_RGBA_ASTC_8x8_SRGB = 0x8E,
        NVN_FORMAT_RGBA_ASTC_10x5_SRGB = 0x8F,
        NVN_FORMAT_RGBA_ASTC_10x6_SRGB = 0x90,
        NVN_FORMAT_RGBA_ASTC_10x8_SRGB = 0x91,
        NVN_FORMAT_RGBA_ASTC_10x10_SRGB = 0x92,
        NVN_FORMAT_RGBA_ASTC_12x10_SRGB = 0x93,
        NVN_FORMAT_RGBA_ASTC_12x12_SRGB = 0x94,
        NVN_FORMAT_BGR565 = 0x95,
        NVN_FORMAT_BGR5 = 0x96,
        NVN_FORMAT_BGR5A1 = 0x97,
        NVN_FORMAT_A1BGR5 = 0x98,
        NVN_FORMAT_BGRX8 = 0x99,
        NVN_FORMAT_BGRA8 = 0x9A,
        NVN_FORMAT_BGRX8_SRGB = 0x9B,
        NVN_FORMAT_BGRA8_SRGB = 0x9C,
        NVN_FORMAT_LARGE = 0x7FFFFFFF
    }
    internal class NVNFormatInfo
    {
        public int BytesPerPixel { get; private set; }
        public int BlockWidth { get; private set; }
        public int BlockHeight { get; private set; }
        public int BlockDepth { get; private set; }

        public CompressionFormat CompressionFormat { get; private set; }

        public NVNFormatInfo(int bytesPerPixel, int blockWidth, int blockHeight, int blockDepth, CompressionFormat compressionFormat)
        {
            BytesPerPixel = bytesPerPixel;
            BlockWidth = blockWidth;
            BlockHeight = blockHeight;
            BlockDepth = blockDepth;
            CompressionFormat = compressionFormat;
        }
    }
    public class NVNTexture
    {

        public static int GetBytesPerPixel(NVNTexFormat Format)
        {
            return FormatTable[Format].BytesPerPixel;
        }

        public static int GetBlockHeight(NVNTexFormat Format)
        {
            return FormatTable[Format].BlockHeight;
        }

        public static int GetBlockWidth(NVNTexFormat Format)
        {
            return FormatTable[Format].BlockWidth;
        }

        public static int GetBlockDepth(NVNTexFormat Format)
        {
            return FormatTable[Format].BlockDepth;
        }

        public static CompressionFormat GetCompressionFormat(NVNTexFormat Format)
        {
            return FormatTable[Format].CompressionFormat;
        }

        private static readonly Dictionary<NVNTexFormat, NVNFormatInfo> FormatTable =
                 new Dictionary<NVNTexFormat, NVNFormatInfo>()
        {
            { NVNTexFormat.NVN_FORMAT_RGBA_DXT5,  new NVNFormatInfo(16, 4,  4, 1, CompressionFormat.Bc3) },
            { NVNTexFormat.NVN_FORMAT_RGBA_DXT1,  new NVNFormatInfo(8,  4,  4, 1, CompressionFormat.Bc1) },
            { NVNTexFormat.NVN_FORMAT_BPTC_UNORM, new NVNFormatInfo(16, 4,  4, 1, CompressionFormat.Bc7) },
        };
    }
}
