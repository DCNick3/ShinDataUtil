using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace ShinDataUtil.Util
{
    public class MessageTextLayouter : MessageTextParser.IVisitor
    {
        // this is somewhat similar to the class with the same name
        // but this only tries to handle the spacial layout, not order of things (for now, at least)
        
        private const int NewrodinFontWidth = 20;
        private const int NewrodinFontHeight = 80;

        private double _defaultFontSize;
        private uint _defaultTextColor;
        private double _defaultTextDrawSpeed;
        private double _defaultFade;
        
        
        private static readonly byte[] ColorDecimalToByte = {0, 0x1c, 0x39, 0x55, 0x71, 0x8e, 0xaa, 0xc6, 0xe3, 0xff};
        
        // ), >, ], ―, ’, ”, ‥, …, ─, ♪, 、, 。, 々, 〉, 》, 」, 』, 】, 〕, 〟,
        // ぁ, ぃ, ぅ, ぇ, ぉ, っ, ゃ, ゅ, ょ, ゎ, ん, ゝ, ゞ, ァ, ィ, ゥ, ェ, ォ, ッ, ャ, ュ, ョ, ヮ, ヵ, ヶ,
        // ・, ー, ヽ, ヾ, ！, ）, ：, ；, ？, ｝, ～
        private static readonly char[] CharTable1 = { 
            '\u0029',  '\u003E',  '\u005D', '\u2015',
            '\u2019',  '\u201D',  '\u2025', '\u2026',
            '\u2500',  '\u266A',  '\u3001', '\u3002',
            '\u3005',  '\u3009',  '\u300B', '\u300D',
            '\u300F',  '\u3011',  '\u3015', '\u301F',
            '\u3041',  '\u3043',  '\u3045', '\u3047',
            '\u3049',  '\u3063',  '\u3083', '\u3085',
            '\u3087',  '\u308E',  '\u3093', '\u309D',
            '\u309E',  '\u30A1',  '\u30A3', '\u30A5',
            '\u30A7',  '\u30A9',  '\u30C3', '\u30E3',
            '\u30E5',  '\u30E7',  '\u30EE', '\u30F5',
            '\u30F6',  '\u30FB',  '\u30FC', '\u30FD',
            '\u30FE',  '\uFF01',  '\uFF09', '\uFF1A',
            '\uFF1B',  '\uFF1F',  '\uFF5D', '\uFF5E' };
        
        // , <, [, ‘, “, 〈, 《, 「, 『, 【, 〔, 〝, （, ｛
        private static readonly char[] CharTable2 =
        {
            '\u0008',  '\u003C',  '\u005B',  '\u2018',
            '\u201C',  '\u3008',  '\u300A',  '\u300C',
            '\u300E',  '\u3010',  '\u3014',  '\u301D',
            '\uFF08',  '\uFF5B'
        };

        private double _currentFontSize;
        private uint _currentTextColor;
        private double _currentTextDrawSpeed;
        private double _currentFade;


        private bool _boolB4;
        private bool _isFuriganaOpen;
        private double _xPosition;
        private double _someCharModifier;
        private double _voiceVolume;

        private Vector2 _someVect;
        private long _someCount;
        private bool _someVoiceModifier;

        private int _sectionIndex;
        private int _commandCount;
        private double _furiganaStartX;

        private int _msgInitValue1;
        private int _someQuotationMagic;
        private int _moreQuotationMagic;

        private MtlParam _mtlParam;
        
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public struct MtlParam
        {
            public double LayoutWidth;
            public int MsgInitValue2;
            public double f_8;
            public double f_c;
            public double f_10;
            public double SmallTextSize;
            public double BigTextSize;
            public double f_1c;
            public bool SomeTableModifier;
            public bool b_21;
            public bool b_22;
        }

        public struct MtlParam2
        {
            public int ColorDec;
            public int TextDrawSpeed;
            public int Fade;
        }
        
        public MessageTextLayouter(int msgInit1, MtlParam mtlParam, MtlParam2 mtlParam2)
        {
            _mtlParam = mtlParam;
            if (_mtlParam.SmallTextSize == 0) 
                _mtlParam.SmallTextSize = _mtlParam.BigTextSize * .4;

            _defaultFontSize = 1;
            _defaultTextColor = DecodeColor(mtlParam2.ColorDec);
            
            mtlParam2.TextDrawSpeed = Math.Min(mtlParam2.TextDrawSpeed, 100);
            _defaultTextDrawSpeed = .0025;
            if (mtlParam2.TextDrawSpeed >= 0)
                _defaultTextDrawSpeed = (100 - mtlParam2.TextDrawSpeed) * .0001 * .25;
            
            _defaultFade = Math.Max(0, mtlParam2.Fade);
            
            _msgInitValue1 = msgInit1;
            _someQuotationMagic = 0;
            _moreQuotationMagic = 0;
        }

        static uint DecodeColor(int colorDec)
        {
            if (colorDec < 999)
            {
                return (uint) (
                    0xff000000U
                    | ColorDecimalToByte[colorDec % 10]
                    | (ColorDecimalToByte[colorDec / 10 % 10] << 8)
                    | (ColorDecimalToByte[colorDec / 100] << 16)
                );
            }

            return 0xffffffffU;
        }
        
        public static MessageTextLayouter Default(int msgInit1, int mgsInit2)
        {
            var mtlParam = new MtlParam
            {
                LayoutWidth = 1500,
                MsgInitValue2 = mgsInit2,
                f_8 = 0,
                f_c = 0,
                f_10 = 4,
                SmallTextSize = 20,
                BigTextSize = 50,
                f_1c = 1,
                SomeTableModifier = true,
                b_21 = true,
                b_22 = true,
            };
            
            var mtlParam2 = new MtlParam2
            {
                ColorDec = 999, 
                TextDrawSpeed = 100, 
                Fade = 200
            };

            return new MessageTextLayouter(msgInit1, mtlParam, mtlParam2);
        }


        public void MessageStart()
        {
            _xPosition = 0;
            _someCharModifier = 0;
            _someCount = 0;

            _currentFontSize = 1.0;

            _boolB4 = _isFuriganaOpen = false;
            _voiceVolume = 1.0;

            _currentTextColor = _defaultTextColor;
            _currentTextDrawSpeed = _defaultTextDrawSpeed;
            _currentFade = _defaultFade;

            _someVoiceModifier = true;

            _sectionIndex = 1;
            _commandCount = 0;

            _furiganaStartX = 0;
            
            _someVect = Vector2.Zero;
        }

        public void Char(int codePoint)
        {
            // TODO: is it needed here? The game does that =)
            var isInTable1 = Array.IndexOf(CharTable1, codePoint) != -1;
            var isInTable2 = Array.IndexOf(CharTable2, codePoint) != -1;

            
            
            throw new System.NotImplementedException();
        }

        public void NewLine()
        {
            //throw new System.NotImplementedException();
        }

        public void LipSyncEnable()
        {
            throw new System.NotImplementedException();
        }

        public void LipSyncDisable()
        {
            throw new System.NotImplementedException();
        }

        public void AutoClick()
        {
            throw new System.NotImplementedException();
        }

        public void ClickWait()
        {
            throw new System.NotImplementedException();
        }

        public void SimultaneousTextDraw()
        {
            throw new System.NotImplementedException();
        }

        public void Sync()
        {
            throw new System.NotImplementedException();
        }

        public void Section()
        {
            throw new System.NotImplementedException();
        }

        public void FuriganaKana(string furiganaContents)
        {
            throw new System.NotImplementedException();
        }

        public void FuriganaBaseStart()
        {
            throw new System.NotImplementedException();
        }

        public void FuriganaBaseEnd()
        {
            throw new System.NotImplementedException();
        }

        public void Voice(string voiceFilename)
        {
            throw new System.NotImplementedException();
        }

        public void Fade(int param)
        {
            throw new System.NotImplementedException();
        }

        public void Color(int r, int g, int b)
        {
            throw new System.NotImplementedException();
        }

        public void VoiceVolume(int volume)
        {
            throw new System.NotImplementedException();
        }

        public void TextDrawSpeed(int speed)
        {
            throw new System.NotImplementedException();
        }

        public void Wait(int param)
        {
            // seems to only add it to the command list and call NewLine
            // no command list (yet?)
            NewLine();
        }

        public void FontSize(int size)
        {
            throw new System.NotImplementedException();
        }
    }
}