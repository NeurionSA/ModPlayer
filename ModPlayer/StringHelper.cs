using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModPlayer
{
    /// <summary>
    /// Class that provides additional common string operations.
    /// </summary>
    sealed class StringHelper
    {
        /// <summary>
        /// Returns the first N characters in a string.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        public static string Left(string str, int len)
        {
            // check arguments
            if (str == null) throw new ArgumentNullException("str");
            if (len < 0) throw new ArgumentOutOfRangeException("len", "Length must be greater than or equal to 0.");

            return str.Substring(0, Math.Min(str.Length, len));
        }

        /// <summary>
        /// Returns the last N characters in a string.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        public static string Right(string str, int len)
        {
            // check arguments
            if (str == null) throw new ArgumentNullException("str");
            if (len <= 0) throw new ArgumentOutOfRangeException("len", "Length must be greater than 0.");

            return str.Substring(Math.Max(str.Length - len, 0), Math.Min(str.Length, len));
        }

        /// <summary>
        /// Returns a string up to the first instance of a null character.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string TrimNull(string str)
        {
            // check arguments
            if (str == null) throw new ArgumentNullException("str");

            int first = str.IndexOf((char)0);

            if (first == -1) return str;
            return Left(str, first);
        }
    }
}
