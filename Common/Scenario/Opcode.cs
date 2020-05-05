// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
namespace ShinDataUtil.Scenario
{
    public enum Opcode : byte
    {
        // a special guy
        EXIT = 0,
        
        // Operations (do not yield to game loop)
        
        /// <summary>
        /// Binary operation (two arguments...)
        /// </summary>
        bo = 65,
        /// <summary>
        /// Reverse polish notation-encoded expression
        /// </summary>
        exp = 66,
        /// <summary>
        /// Jump conditional
        /// </summary>
        jc = 70,
        /// <summary>
        /// Jump unconditional
        /// </summary>
        j = 71,
        /// <summary>
        /// Function call
        /// </summary>
        call = 72,
        /// <summary>
        /// Function return
        /// </summary>
        ret = 73,
        /// <summary>
        /// Follow a jump table
        /// </summary>
        jt = 74,
        
        /// <summary>
        /// Generate a pseudo-random number. Destination address is in the first argument. Second and third give boundaries.
        /// </summary>
        rnd = 76,
        
        /// <summary>
        /// Push several values to CALL stack
        /// </summary>
        push = 77,
        /// <summary>
        /// Pop several values from CALL stack
        /// </summary>
        pop = 78,
        
        // Commands, yield to game loop
        SGET = 129,
        SSET = 130,
        WAIT = 131,              // doc'ed
        MSGINIT = 133,           // doc'ed
        MSGSET = 134,            // doc'ed
        MSGWAIT = 135,           // doc'ed
        MSGSIGNAL = 136,         // doc'ed
        MSGSYNC = 137,
        MSGCLOSE = 138,          // doc'ed
        MSGFACE = 139,
        LOGSET = 140,
        SELECT = 141,            // (partially) doc'ed
        WIPE = 142,              
        WIPEWAIT = 143,          
        BGMPLAY = 144,           // doc'ed
        BGMSTOP = 145,           // doc'ed
        BGMVOL = 146,            // doc'ed
        BGMWAIT = 147,           // doc'ed
        BGMSYNC = 148,           // doc'ed
        SEPLAY = 149,            // doc'ed
        SESTOP = 150,            // doc'ed
        SESTOPALL = 151,         // doc'ed
        SEVOL = 152,             // doc'ed
        SEPAN = 153,             // doc'ed
        SEWAIT = 154,            // doc'ed
        SEONCE = 155,
        VOICEPLAY = 156,         // doc'ed
        VOICESTOP = 157,         // doc'ed
        VOICEWAIT = 158,         // doc'ed
        SAVEINFO = 160,
        AUTOSAVE = 161,
        EVBEGIN = 162,
        EVEND = 163,
        TROPHY = 176,
        LAYERINIT = 192,         
        LAYERLOAD = 193,         // doc'ed
        LAYERUNLOAD = 194,       // doc'ed
        LAYERCTRL = 195,         // doc'ed
        LAYERWAIT = 196,         // doc'ed
        LAYERBACK = 197,         // doc'ed
        LAYERSWAP = 198,
        LAYERSELECT = 199,       // doc'ed
        MOVIEWAIT = 200,
        FEELICON = 201,
        TIPSGET = 208,
        CHARSELECT = 209,
        OTSUGET = 210,
        CHART = 211,
        SNRSEL = 212,
        KAKERA = 213,
        KAKERAGET = 214,
        QUIZ = 215,
        FAKESELECT = 216,        // doc'ed
        UNLOCK = 217,
        DEBUGOUT = 255
    }
}