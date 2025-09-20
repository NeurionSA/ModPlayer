using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModPlayer.MOD
{
    /// <summary>
    /// Class that represents a MOD pattern.
    /// </summary>
    internal class ModPattern
    {
        private ModPatternRow[] _rows;

        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <param name="rows"></param>
        public ModPattern(ModPatternRow[] rows)
        {
            // check arguments
            ArgumentNullException.ThrowIfNull(rows);
            if (rows.Length != 64) throw new ArgumentException("rows array must have a length of exactly 64.");

            _rows = rows;
        }

        /// <summary>
        /// Gets the ModPatternRow with the specified index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public ModPatternRow Rows(int index)
        {
            // check arguments
            if ((index < 0) || (index >= 64)) throw new ArgumentOutOfRangeException("index");

            return _rows[index];
        }
    }
}
