using System.IO;
using System.Text;

namespace ShinDataUtil.Common
{
    public class ShiftJisEncodingProvider : EncodingProvider
    {
        private static readonly object RegisterLock = new object();
        private static bool _registered;
        public override Encoding? GetEncoding(int codepage) => null;

        public override Encoding? GetEncoding(string name)
        {
            if (name == "shift_jis")
                return new ShiftJisEncoding();
            return null;
        }

        public static void Register()
        {
            lock (RegisterLock)
            {
                if (!_registered)
                    Encoding.RegisterProvider(new ShiftJisEncodingProvider());
                _registered = true;
            }
        }
    }
    
    public partial class ShiftJisEncoding : Encoding
    {
        
        public override int GetByteCount(char[] chars, int index, int count)
        {
            var bytesCount = 0;
            for (var i = index; i < index + count; i++)
            {
                var c = chars[i];
                if (Chars.TryGetValue(c, out var t))
                    bytesCount += t.Item2 == null ? 1 : 2;
                else
                    throw new InvalidDataException();
            }

            return bytesCount;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            var bytesCount = 0;
            for (var i = charIndex; i < charIndex + charCount; i++)
            {
                var c = chars[i];
                if (Chars.TryGetValue(c, out var t))
                {
                    var (b1, b2) = t;
                    if (b2 == null)
                        bytes[byteIndex++] = b1;
                    else
                    {
                        bytes[byteIndex++] = b1;
                        bytes[byteIndex++] = b2.Value;
                    }

                    bytesCount += b2 == null ? 1 : 2;
                }
            }

            return bytesCount;
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            var r = 0;
            for (var i = index; i < index + count; i++)
            {
                var b1 = bytes[i];
                if (SingleByte.ContainsKey(b1))
                {
                    r++;
                    continue;
                }
                if (i + 1 == index + count)
                    return r;
                var b2 = bytes[++i];
                if (DoubleByte.ContainsKey((b1, b2)))
                {
                    r++;
                    continue;
                }
                throw new InvalidDataException();
            }

            return r;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            var j = charIndex;
            for (var i = byteIndex; i < byteIndex + byteCount; i++)
            {
                var b1 = bytes[i];
                if (SingleByte.TryGetValue(b1, out var c1))
                {
                    chars[j++] = c1;
                    continue;
                }
                if (i + 1 == byteIndex + byteCount)
                    return j - charIndex;
                var b2 = bytes[++i];
                if (DoubleByte.TryGetValue((b1, b2), out var c2))
                {
                    chars[j++] = c2;
                    continue;
                }
                throw new InvalidDataException();
            }

            return j - charIndex;
        }

        public override int GetMaxByteCount(int charCount) => charCount * 2;
        public override int GetMaxCharCount(int byteCount) => byteCount;
    }
}