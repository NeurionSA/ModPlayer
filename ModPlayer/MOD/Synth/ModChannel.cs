//#define USE_PAL_FREQ    // if this define exists, then the PAL frequency magicnum will be used instead of NTSC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModPlayer.MOD.Synth
{
    /// <summary>
    /// Class encapsulation of a synthesizer's channel. Manages retrieval of audio samples and tracks effect memory.
    /// </summary>
    internal class ModChannel
    {
        // the target playback rate for which the channel will generate samples
        private long _playbackRate;

        // flag for whether the channel is currently playing a note
        private bool _isPlaying = false;

        // the Mod sample currently assigned to the channel
        private ModInstrument? _instrument;

        // the sample period the voice has been supplied
        private int _samplePeriod;
        // the sample period the voice was supplied with initially, used for arpeggio and vibrato effects
        private int _samplePeriodBase;
        // the position within the waveform from which to get the next sample
        private float _samplePosition;
        // the value to increment the waveform sample position by when each sample is retrieved
        private float _samplePositionStep;

        // the finetune value, initially from the instrument
        private int _fineTune;

        // the multiplier for channel volume, in raw and calculated forms
        private int _rawVolume;
        private float _calcVolume;

        /*
         * The following variables are for effect memory and tracking effect state,
         * and as such have no effect on sample generation.
         */

        // the float multipliers for left and right pan
        private float _panLeft;
        private float _panRight;
        // the two other SamplePeriod values to use for the arpeggio effect
        private int _arpeggioStep1;
        private int _arpeggioStep2;
        // the counter for the arpeggio effect
        private int _arpeggioCounter;
        // the target SamplePeriod value for the Tone Portamento effect
        private int _tonePortamentoPeriodTarget;
        private int _tonePortamentoSpeed;
        // the value used for Volume Slide effects
        private int _volumeSlideValue;
        // the retrigger rate (in ticks)
        private int _retriggerRate;
        // values for vibrato
        private int _vibratoPos;    // position in the vibrato table
        private int _vibratoSpeed;  // number of positions to advance in table each tick
        private int _vibratoDepth;  // depth of vibrato

        /// <summary>
        /// Creates a new instance of the class.
        /// </summary>
        /// <param name="rate">The target playback rate for which the Channel will generate samples.</param>
        public ModChannel(long rate)
        {
            // check arguments
            if (rate < 1) throw new ArgumentOutOfRangeException("rate");

            _playbackRate = rate;
            // reset the channel's status
            Reset();
        }

        /// <summary>
        /// Resets the channel to its default state.
        /// </summary>
        public void Reset()
        {
            // voice is no longer active
            _isPlaying = false;
            // volume is 0
            Volume = 0;
            // no instrument is assigned
            _instrument = null;

            // reset all the channel effects/effect memory
            // pan is centered
            _panLeft = 1;
            _panRight = 1;
            // arpeggio
            _arpeggioStep1 = 0;
            _arpeggioStep2 = 0;
            _arpeggioCounter = 0;
            // portamento values
            _tonePortamentoPeriodTarget = 0;
            _tonePortamentoSpeed = 0;
            // volume slide
            _volumeSlideValue = 0;
            // retrigger
            _retriggerRate = 0;
            // vibrato
            _vibratoDepth = 0;
            _vibratoPos = 0;
            _vibratoSpeed = 0;
        }

        /// <summary>
        /// Activates a note, played with the given instrument and sample period.
        /// </summary>
        /// <param name="instrument"></param>
        /// <param name="samplePeriod"></param>
        public void NoteOn(ModInstrument instrument, int samplePeriod)
        {
            // check arguments
            if (instrument == null)
            {
                // a note-on with an undefined sample IS allowed, as I've seen this in official MODs, it will just be treated as a Note-Off
                _isPlaying = false;
                return;
            }

            _instrument = instrument;
            _isPlaying = true;

            _fineTune = instrument.FineTune;

            // set sample position to 0
            _samplePosition = 0;
            // set vibrato position to 0
            _vibratoPos = 0;
            // set the sample period
            SamplePeriod = samplePeriod;
            // record the initial sample period as well
            _samplePeriodBase = samplePeriod;
        }

        /// <summary>
        /// Gets the next mono sample from the Channel.
        /// </summary>
        /// <returns></returns>
        public short GetSample()
        {
            // check status
            if (_instrument == null) throw new InvalidOperationException("No instrument assigned");
            if (_isPlaying == false) return 0;

            // get the sample at the current position
            float ret = _instrument.GetSampleInterpolated(_samplePosition, SampleInterpolation.NearestNeighbor);
            // increment the sample position
            _samplePosition += _samplePositionStep;

            // handle looping
            if (_samplePosition >= _instrument.SampleLength)
            {
                if (_instrument.IsLooping)
                {
                    // it loops, so proceed with the wrap
                    _samplePosition -= _instrument.SampleRepeatLength;
                }
                else
                {
                    // it does not loop, so the voice stops playing after this sample
                    _isPlaying = false;
                }
            }

            // apply the channel's volume multiplier
            ret *= _calcVolume;

            return (short)ret;
        }

        /// <summary>
        /// Sets the channel's pan multipliers.
        /// </summary>
        /// <param name="rawValue"></param>
        public void SetPan(int rawValue)
        {
            _panLeft = Math.Min((255 - rawValue) / 128f, 1);
            _panRight = Math.Min(rawValue / 128f, 1);
        }

        /// <summary>
        /// Overrides the channel's current finetune
        /// </summary>
        /// <param name="rawValue"></param>
        public void SetFineTune(int rawValue)
        {
            // get the proper value
            if ((rawValue < 0) || (rawValue > 15)) throw new ArgumentOutOfRangeException("rawValue");

            if (rawValue >= 8)
            {
                _fineTune = 16 - rawValue;
            }
            else
            {
                _fineTune = rawValue;
            }

            // recalculate the sample position step
#if USE_PAL_FREQ
            _samplePositionStep = (float)((7093789.2 / ((_samplePeriod + _fineTune) * 2)) / _playbackRate);
#else
            _samplePositionStep = (float)((7159090.5 / ((_samplePeriod + _fineTune) * 2)) / _playbackRate);
#endif
        }

        public bool IsPlaying => _isPlaying;

        public ModInstrument Instrument
        {
            get { return _instrument!; }
            set
            {
                // make sure the instrument is valid
                ArgumentNullException.ThrowIfNull(value);

                // TODO: Find out what we should be doing here
                _instrument = value;
            }
        }

        public int Volume
        {
            get
            {
                return _rawVolume;
            }
            set
            {
                // clamp value
                _rawVolume = Math.Min(Math.Max(value, 0), 64);

                _calcVolume = _rawVolume / 64f;
            }
        }

        public int SamplePeriod
        {
            get
            {
                return _samplePeriod;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException("value");
                _samplePeriod = value;
                // calculate the sample position step for the given sample period
                // NOTE: There are two magicnums I've seen for this formula, one for PAL and one for NTSC
                // the PAL one is 7093789.2, the NTSC one is 7159090.5; OpenMPT seems to use the PAL one
#if USE_PAL_FREQ
                _samplePositionStep = (float)((7093789.2 / ((_samplePeriod + _fineTune) * 2)) / _playbackRate);
#else
                _samplePositionStep = (float)((7159090.5 / ((_samplePeriod + _fineTune) * 2)) / _playbackRate);
#endif
            }
        }

        public float SamplePosition
        {
            get
            {
                return _samplePosition;
            }
            set
            {
                // will do nothing if there is no instrument assigned
                if (_instrument != null)
                {
                    if ((value < 0) || (value >= _instrument?.SampleLength)) throw new ArgumentOutOfRangeException("value");
                    _samplePosition = value;
                }
            }

        }

        public float PanLeft
        {
            get { return _panLeft; }
        }

        public float PanRight
        {
            get { return _panRight; }
        }

        public int SamplePeriodBase
        {
            get { return _samplePeriodBase; }
            set { _samplePeriodBase = value; }
        }

        public int ArpeggioStep1
        {
            get { return _arpeggioStep1; }
            set { _arpeggioStep1 = value; }
        }

        public int ArpeggioStep2
        {
            get { return _arpeggioStep2; }
            set { _arpeggioStep2 = value; }
        }

        public int ArpeggioCounter
        {
            get { return _arpeggioCounter; }
            set { _arpeggioCounter = value; }
        }

        public int TonePortamentoPeriodTarget
        {
            get { return _tonePortamentoPeriodTarget; }
            set { _tonePortamentoPeriodTarget = value; }
        }

        public int TonePortamentoSpeed
        {
            get { return _tonePortamentoSpeed; }
            set { _tonePortamentoSpeed = value; }
        }

        public int VolumeSlideValue
        {
            get { return _volumeSlideValue; }
            set { _volumeSlideValue = value; }
        }

        public int RetriggerRate
        {
            get { return _retriggerRate; }
            set { _retriggerRate = value; }
        }

        public int VibratoPosition
        {
            get { return _vibratoPos; }
            set { _vibratoPos = value; }
        }

        public int VibratoSpeed
        {
            get { return _vibratoSpeed; }
            set { _vibratoSpeed = value; }
        }

        public int VibratoDepth
        {
            get { return _vibratoDepth; }
            set { _vibratoDepth = value; }
        }
    }
}
