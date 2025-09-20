using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModPlayer.MOD
{
    /// <summary>
    /// Class that represents a row in a ModPattern.
    /// </summary>
    internal class ModPatternRow
    {
        private ModPatternCell[] _cells;

        public ModPatternRow(ModPatternCell[] cells)
        {
            // check arguments
            ArgumentNullException.ThrowIfNull(cells);
            if (cells.Length == 0) throw new ArgumentException("notes must have a non-zero length.");

            _cells = cells;
        }

        /// <summary>
        /// Gets the cells for the specified channel.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public ModPatternCell Cells(int channel)
        {
            // check arguments
            if ((channel < 0) || (channel >= _cells.Length)) throw new ArgumentOutOfRangeException("channel");

            return _cells[channel];
        }
    }
}
