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
        
        OPCODE79 = 79,
        
        OPCODE80 = 80,
        OPCODE83 = 83,
        
        OPCODE128 = 128,
        OPCODE129 = 129,
        OPCODE130 = 130,
        OPCODE131 = 131,
        OPCODE132 = 132,
        OPCODE133 = 133,
        OPCODE134 = 134,
        OPCODE135 = 135,
        OPCODE136 = 136,
        OPCODE137 = 137,
        OPCODE138 = 138,
        OPCODE144 = 144,
        OPCODE145 = 145,
        OPCODE146 = 146,
        OPCODE149 = 149,
        OPCODE150 = 150,
        OPCODE151 = 151,
        OPCODE152 = 152,
        OPCODE154 = 154,
        OPCODE155 = 155,
        OPCODE156 = 156,
        OPCODE158 = 158,
        OPCODE193 = 193,
        OPCODE194 = 194,
        OPCODE195 = 195,
        OPCODE196 = 196,
        OPCODE197 = 197,
        OPCODE198 = 198,
        OPCODE199 = 199,
        OPCODE224 = 224,
        OPCODE225 = 225,
        DEBUGOUT = 255           // doc'ed
    }
}