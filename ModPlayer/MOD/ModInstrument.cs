using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModPlayer.MOD
{
    /// <summary>
    /// Encapsulates a MOD instrument.
    /// </summary>
    internal class ModInstrument
    {
        // the instrument's name
        private string _name;
        // the instrument's "finetune" value
        private int _fineTune;
        // the instrument's initial volume
        private byte _initialVolume;
        // the instrument's sample repeat offset
        private int _sampleRepeatOffset;
        // the instrument's sample repeat length
        private int _sampleRepeatLength;

        // calculated endpoint for sample repeat loop
        private int _sampleRepeatEnd;

        // the instrument's sample data (8-bit, signed)
        private sbyte[] _sampleData;

        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="finetune"></param>
        /// <param name="initialVolume"></param>
        /// <param name="sampleRepeatOffset"></param>
        /// <param name="sampleRepeatLength"></param>
        /// <param name="sampleData"></param>
        public ModInstrument(string name, int finetune, byte initialVolume, int sampleRepeatOffset, int sampleRepeatLength, sbyte[] sampleData)
        {
            // check arguments
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(sampleData);
            if (sampleData.Length == 0) throw new ArgumentException("sampleData must have a non-zero length.");
            if ((finetune < -8) || (finetune > 7)) throw new ArgumentOutOfRangeException("finetune");
            // TODO: ensure the sample repeat offset and length values are also valid

            _name = name;
            _fineTune = finetune;
            _initialVolume = initialVolume;
            _sampleRepeatOffset = sampleRepeatOffset;
            _sampleRepeatLength = sampleRepeatLength;
            _sampleRepeatEnd = _sampleRepeatOffset + _sampleRepeatLength;
            _sampleData = sampleData;
        }

        /// <summary>
        /// Gets the sample at a specified position in the waveform without interpolation.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private short getSample(int index)
        {
            // check arguments
            if (index < 0) throw new ArgumentOutOfRangeException("index");

            if (IsLooping)
            {
                if (index >= _sampleRepeatEnd)
                {
                    // change the index so it falls within the region bound by sampleRepeatOffset and sampleRepeatLength
                    index = ((index - _sampleRepeatEnd) % _sampleRepeatLength) + _sampleRepeatOffset;
                }
            }
            else
            {
                if (index >= _sampleData.Length) return 0;
            }

            // expand from 8 bits to 16 bits
            return (short)(_sampleData[index] << 8);
        }

        /// <summary>
        /// Gets the sample at a specified position in the waveform using the specified interpolation.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="interpolation"></param>
        /// <returns></returns>
        public short GetSampleInterpolated(float position, SampleInterpolation interpolation)
        {
            // check arguments
            if (position < 0) throw new ArgumentOutOfRangeException("position");

            short ret = 0;

            // branch based on interpolation type
            switch (interpolation)
            {
                case SampleInterpolation.NearestNeighbor:
                    // return whichever sample we're closest to
                    ret = getSample((int)Math.Round(position));
                    break;

                case SampleInterpolation.Linear:
                    // get the 2 samples around the point
                    int floor = (int)Math.Floor(position);
                    short s1 = getSample(floor);
                    short s2 = getSample(floor + 1);

                    // get the fractional value of the position
                    float frac = position % 1;

                    // interpoalte between the samples linearly
                    ret = (short)(s1 * (1 - frac) + s2 * frac);
                    break;

                default:
                    throw new InvalidEnumArgumentException("interpolation", (int)interpolation, typeof(SampleInterpolation));
            }

            return ret;
        }

        public string Name => _name;
        public int FineTune => _fineTune;
        public byte InitialVolume => _initialVolume;
        public int SampleRepeatOffset => _sampleRepeatOffset;
        public int SampleRepeatLength => _sampleRepeatLength;
        public int SampleLength => _sampleData.Length;

        /// <summary>
        /// Gets whether the instrument loops.
        /// </summary>
        public bool IsLooping
        {
            get
            {
                if (_sampleRepeatLength > 2) return true;
                return false;
            }
        }
    }
}
