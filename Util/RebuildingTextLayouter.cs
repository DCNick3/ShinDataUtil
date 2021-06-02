using System.Diagnostics;
using System.Text;

namespace ShinDataUtil.Util
{
    public class RebuildingTextLayouter : MessageTextParser.IVisitor
    {
        protected readonly StringBuilder Sb = new StringBuilder();
        
        public virtual void MessageStart()
        {
            Sb.Clear();
        }

        protected virtual void Emit(string s) => Sb.Append(s);

        public virtual void Char(int codePoint)
        {
            Trace.Assert(codePoint <= 0xffff);
            Trace.Assert(!char.IsSurrogate((char)codePoint));
            Trace.Assert(codePoint != '@');
            Sb.Append((char) codePoint);
        }

        public virtual void NewLine() => Emit("@r");
        public virtual void LipSyncEnable() => Emit("@+");
        public virtual void LipSyncDisable() => Emit("@-");
        public virtual void AutoClick() => Emit("@e");
        public virtual void ClickWait() => Emit("@k");
        public virtual void SimultaneousTextDraw() => Emit("@t");
        public virtual void Sync() => Emit("@y");
        public virtual void Section() => Emit("@|");
        public virtual void FuriganaKana(string furiganaContents) => Emit($"@b{furiganaContents}.");
        public virtual void FuriganaBaseStart() => Emit("@<");
        public virtual void FuriganaBaseEnd() => Emit("@>");
        public virtual void Voice(string voiceFilename) => Emit($"@v{voiceFilename}.");
        public virtual void Fade(int param) => Emit($"@a{param}.");
        public virtual void Color(int r, int g, int b) => Emit($@"@c{r}{g}{b}.");
        public virtual void VoiceVolume(int volume) => Emit($"@o{volume}.");
        public virtual void TextDrawSpeed(int speed) => Emit($"@s{speed}.");
        public virtual void Wait(int param) => Emit($"@w{param}.");
        public virtual void FontSize(int size) => Emit($"@z{size}.");

        public virtual string Dump() => Sb.ToString();
    }
}