using System;
using System.Collections.Generic;
using System.Diagnostics;
using ShinDataUtil.Decompression;

namespace ShinDataUtil.Util
{
    public class MessageEnglishLayoutHelper : RebuildingTextLayouter
    {
        private readonly ShinFontExtractor.LayoutInfo _fontInfo;

        public MessageEnglishLayoutHelper(ShinFontExtractor.LayoutInfo fontInfo)
        {
            _fontInfo = fontInfo;
            _lineCommands = new List<Command>();
        }
        
        struct Command
        {
            public string Value;
            public double X;
            public double Width;

            public override string ToString()
            {
                return Value;
            }
        }

        protected double _defaultFontSize = 1;
        protected double _bigTextSize = 50;
        protected double _layoutWidth = 1500;

        private List<Command> _lineCommands;
        private int _lineNumber;
        private double _fontSize;
        protected double _xPosition;

        public override void MessageStart()
        {
            base.MessageStart();
            _lineCommands.Clear();
            _lineNumber = 0;
            _xPosition = 0;
            _fontSize = _defaultFontSize;
        }

        protected override void Emit(string s)
        {
            _lineCommands.Add(new Command
            {
                Value = s,
                X = _xPosition,
                Width = 0
            });
        }

        public override void Char(int codePoint)
        {
            if (_lineNumber == 0)
            {
                base.Char(codePoint);
                return;
            }
            
            Trace.Assert(codePoint <= 0xffff);
            Trace.Assert(!char.IsSurrogate((char)codePoint));
            Trace.Assert(codePoint != '@');
            var c = (char) codePoint;
            var glyphInfo = _fontInfo.GlyphInfo[codePoint];
            var width =
                _fontSize
                * glyphInfo.VirtualWidth
                * _bigTextSize 
                / (_fontInfo.BiggerSize + _fontInfo.SmallerSize); 
            
            _lineCommands.Add(new Command
            {
                Value = c.ToString(),
                X = _xPosition,
                Width = width
            });
            
            _xPosition += width;
        }

        public override void FontSize(int size)
        {
            if (size < 0)
                _fontSize = _defaultFontSize;
            else
            {
                _fontSize = size * .01;
                _fontSize = Math.Max(_fontSize, 0.1);
                _fontSize = Math.Min(_fontSize, 2.0);
            }

            base.FontSize(size);
        }

        private void FlushLine()
        {
            // TODO: not so flexible. Maybe we want some more unicode here =)
            var breakableCharacters = new HashSet<char>
            {
                ' '
            };

            void AddRange(int lo, int hi)
            {
                for (var i = lo; i <= hi; i++) 
                    breakableCharacters.Add((char) i);
            }
            
            // https://stackoverflow.com/questions/19899554/unicode-range-for-japanese
            
            // CJK punctuation
            AddRange(0x3000, 0x303f);
            
            // hiragana & katakana
            AddRange(0x3040, 0x30ff);
            
            // Kanji
            AddRange(0x3400, 0x4DB5);
            AddRange(0x4E00, 0x9FCB);
            AddRange(0xF900, 0xFA6A);
            
            // Katakana and Punctuation (Half Width)
            AddRange(0xFF5F, 0xFF9F);
            
            // Miscellaneous Japanese Symbols and Characters
            AddRange(0x31F0, 0x31FF);
            AddRange(0x3220, 0x3243);
            AddRange(0x3280, 0x337F);


            var currentDumpedWidth = 0.0;
            var dumpedOffset = 0;
            while (dumpedOffset < _lineCommands.Count)
            {
                int toDumpEnd;
                var mustBreak = false;
                for (toDumpEnd = dumpedOffset; toDumpEnd < _lineCommands.Count; toDumpEnd++)
                {
                    if (_lineCommands[toDumpEnd].X - currentDumpedWidth >= _layoutWidth ||
                        _lineCommands[toDumpEnd].X + _lineCommands[toDumpEnd].Width - currentDumpedWidth >=
                        _layoutWidth * 1.5)
                    {
                        mustBreak = true;
                        break;
                    }
                }

                var breakIndex = toDumpEnd;

                if (mustBreak)
                {
                    for (var i = toDumpEnd - 1; i >= dumpedOffset; i--)
                    {
                        if (!breakableCharacters.Contains(_lineCommands[i].Value[0]))
                            continue;
                        breakIndex = i + 1;
                        break;
                    }

                    if (breakIndex == 0)
                        breakIndex = toDumpEnd;
                }

                currentDumpedWidth = _lineCommands[breakIndex - 1].X + _lineCommands[breakIndex - 1].Width;
                for (var i = dumpedOffset; i < breakIndex; i++)
                    Sb.Append(_lineCommands[i].Value);
                if (mustBreak)
                    Sb.Append("@r");
                dumpedOffset = breakIndex;
            }

            _xPosition = 0;
            _lineCommands.Clear();
        }

        public override void NewLine()
        {
            base.NewLine();
            _lineNumber++;
            FlushLine();
        }

        public override string Dump()
        {
            FlushLine();
            return base.Dump();
        }
    }
}