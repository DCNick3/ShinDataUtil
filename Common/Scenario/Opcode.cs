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
        uo = 64,
        
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
        /// Call using a jump table
        /// </summary>
        callt = 75,
        
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
        
        /// <summary>
        /// Call, but pass arguments
        /// </summary>
        callex = 79,
        /// <summary>
        /// Return, but clean mem3 from arguments
        /// </summary>
        retex = 80,
        getheadprop = 83,
        
        SGET = 128,
        SSET = 129,
        WAIT = 130,
        MSGINIT = 131,
        MSGSET = 132,
        MSGWAIT = 133,
        MSGSIGNAL = 134,
        MSGCLOSE = 135,
        SELECT = 136,
        WIPE = 137,
        WIPEWAIT = 138,
        BGMPLAY = 144,
        BGMSTOP = 145,
        BGMVOL = 146,
        SEPLAY = 149,
        SESTOP = 150,
        SESTOPALL = 151,
        SEVOL = 152,
        SEWAIT = 154,
        SEONCE = 155,
        VOICEPLAY = 156,
        VOICEWAIT = 158,
        LAYERLOAD = 193,
        LAYERUNLOAD = 194,
        LAYERCTRL = 195,
        LAYERWAIT = 196,
        LAYERBACK = 197,
        LAYERSELECT = 198,
        MOVIEWAIT = 199,
        SLEEP = 224,
        VSET = 225,
        DEBUGOUT = 255           // doc'ed
    }
}