using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ShinDataUtil.Util
{
    public class MessageTextParser
    {
        public interface IVisitor
        {
            void MessageStart();
            void Char(int codePoint);
            void NewLine();
            void LipSyncEnable();
            void LipSyncDisable();
            void AutoClick();
            void ClickWait();
            void SimultaneousTextDraw();
            void Sync();
            void Section();
            void FuriganaKana(string furiganaContents);
            void FuriganaBaseStart();
            void FuriganaBaseEnd();
            void Voice(string voiceFilename);
            void Fade(int param); // TODO: what this changes?
            void Color(int r, int g, int b);
            void VoiceVolume(int volume);
            void TextDrawSpeed(int speed);
            void Wait(int param); // TODO: which unit
            void FontSize(int size);
        }
        
        public class LoggingVisitor : IVisitor
        {
            public void MessageStart()
            {
                NonBlockingConsole.WriteLine("");
            }

            public void Char(int codePoint)
            {
                NonBlockingConsole.Write(char.ConvertFromUtf32(codePoint));
            }

            public void NewLine()
            {
                NonBlockingConsole.Write("@r");
            }

            public void LipSyncEnable()
            { }

            public void LipSyncDisable()
            { }

            public void AutoClick()
            { }

            public void ClickWait()
            { }

            public void SimultaneousTextDraw()
            { }

            public void Sync()
            { }

            public void Section()
            { }

            public void FuriganaKana(string furiganaContents)
            { }

            public void FuriganaBaseStart()
            { }

            public void FuriganaBaseEnd()
            { }

            public void Voice(string voiceFilename)
            { }

            public void Fade(int param)
            { }

            public void Color(int r, int g, int b)
            { }

            public void VoiceVolume(int volume)
            { }

            public void TextDrawSpeed(int speed)
            { }

            public void Wait(int param)
            { }

            public void FontSize(int size)
            { }
        }

        class Reader
        {
            private ReadOnlyMemory<char> _memory;

            public Reader(ReadOnlyMemory<char> memory)
            {
                _memory = memory;
            }

            public void Reset(ReadOnlyMemory<char> memory)
            {
                _memory = memory;
            }
            
            public bool HaveChars => !_memory.IsEmpty;

            public char ReadChar()
            {
                var c = _memory.Span[0];
                _memory = _memory[1..];
                Trace.Assert(!char.IsSurrogate(c));
                return c;
            }

            public string ReadString()
            {
                var c = _memory.Span[0];
                _memory = _memory[1..];
                StringBuilder sb = new StringBuilder();
                while (c != '.')
                {
                    Trace.Assert(!char.IsSurrogate(c));
                    sb.Append(c);
                    c = _memory.Span[0];
                    _memory = _memory[1..];
                }
                return sb.ToString();
            }

            public int ReadInt()
            {
                var c = _memory.Span[0];
                _memory = _memory[1..];
                Trace.Assert(c >= '0' && c <= '9' || c == '$');
                
                var r = 0;
                
                if (c == '$')
                    throw new NotImplementedException("Parsing hex numbers in MessageTextParser");
                
                {
                    while (c != '.')
                    {
                        Trace.Assert('0' <= c && c <= '9');
                        r = r * 10 + (c - '0');
                        
                        c = _memory.Span[0];
                        _memory = _memory[1..];
                    }
                    return r;
                }
            }
        }

        private readonly Reader _reader = new Reader("".AsMemory());
        
        public void ParseTo(string message, IVisitor visitor)
        {
            // the algorithm is kind of similar to shin::MessageTextParser::parse_to
            // but it assumes that string fixups have already been done by the disassembler, so no code related to that
            
            // C# uses UTF-16. This means that some chars can be surrogates
            // For simplicity we do not handle them, just asserting if there are any

            _reader.Reset(message.AsMemory());
            
            while (_reader.HaveChars)
            {
                var c = _reader.ReadChar();

                if (c != '@') 
                    visitor.Char(c);
                else
                {
                    c = _reader.ReadChar();
                    switch (c)
                    {
                        case 'r': visitor.NewLine();  break;
                        case '+': visitor.LipSyncEnable();  break;
                        case '-': visitor.LipSyncDisable();  break;
                        case '<': visitor.FuriganaBaseStart(); break;
                        case '>': visitor.FuriganaBaseEnd(); break;
                        case 'e': visitor.AutoClick();  break;
                        case 'k': visitor.ClickWait();  break;
                        case 't': visitor.SimultaneousTextDraw();  break;
                        case 'y': visitor.Sync();  break;
                        case '|': visitor.Section();  break;
                        case 'b': visitor.FuriganaKana(_reader.ReadString()); break;
                        case 'v': visitor.Voice(_reader.ReadString()); break;
                        case 'U': visitor.Char(_reader.ReadInt()); break;
                        case 'a': visitor.Fade(_reader.ReadInt()); break;
                        case 'c':
                        {
                            var color = _reader.ReadInt();
                            visitor.Color(color / 100, color / 10 % 10, color % 10);
                        } 
                            break;
                        case 'o': visitor.VoiceVolume(_reader.ReadInt()); break;
                        case 's': visitor.TextDrawSpeed(_reader.ReadInt()); break;
                        case 'w': visitor.Wait(_reader.ReadInt()); break;
                        case 'z': visitor.FontSize(_reader.ReadInt()); break;
                        default:
                        throw new NotImplementedException($"Formatting modifier {c} is not implemented");   
                    }
                }
            }
        }
    }
}