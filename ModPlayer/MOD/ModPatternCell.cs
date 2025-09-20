using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModPlayer.MOD
{
    /// <summary>
    /// Class that represents a cell within a MOD pattern.
    /// </summary>
    internal class ModPatternCell
    {
        // the sample number the cell uses
        private int _sampleNumber;
        // the sample 'period' of the cell's note
        private int _samplePeriod;
        // the cell's effect
        private ModEffects _effect;
        // the argument for the cell's effect
        private int _effectArgument;

        public ModPatternCell(int sampleNumber, int samplePeriod, ModEffects effect, int effectArgument)
        {
            // TODO: Check arguments

            _sampleNumber = sampleNumber;
            _samplePeriod = samplePeriod;
            _effect = effect;
            _effectArgument = effectArgument;
        }

        public int SampleNumber => _sampleNumber;
        public int SamplePeriod => _samplePeriod;
        public ModEffects Effect => _effect;
        public int EffectArgument => _effectArgument;

    }
}
