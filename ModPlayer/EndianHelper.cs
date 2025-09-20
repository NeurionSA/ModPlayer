using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModPlayer
{
    /// <summary>
    /// Class that provides helper functions for swapping endianness of values.
    /// </summary>
    sealed class EndianHelper
    {
        /// <summary>
        /// Swaps the endianness of an unsigned 32-bit integer.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint SwapEndianU32(uint value)
        {
            return ((value & 0xFF000000) >> 24) |
                ((value & 0xFF0000) >> 8) |
                ((value & 0xFF00) << 8) |
                ((value & 0xFF) << 24);
        }

        /// <summary>
        /// Swaps the endianness of an unsigned 16-bit integer.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ushort SwapEndianU16(ushort value)
        {
            return (ushort)(((value & 0xFF00) >> 8) |
                ((value & 0xFF) << 8));
        }
    }
}
