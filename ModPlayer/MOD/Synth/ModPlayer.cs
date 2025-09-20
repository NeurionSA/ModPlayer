using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace ModPlayer.MOD.Synth
{
    internal class ModPlayer : IDisposable, IWaveProvider
    {
        // max number of channels to support
        private const int MAX_CHANNELS = 8;
        // forced global multiplier for voice volume
        private const float VOLUME_MULTIPLIER = 0.25f;

        private const int PLAYBACK_RATE = 44100;

        // internal stream for the synth's generated waveform buffer
        private MemoryStream _waveStream;
        // BinaryWriter for operating on the waveform buffer
        private BinaryWriter _waveWriter;
        // BinaryReader for operating on the waveform buffer
        private BinaryReader _waveReader;

        // audio device to send wave data to
        private WasapiOut _waveDevice;

        // the wave format the wave device has been opened with
        private WaveFormat _waveFormat;

        // MOD sequence to play from
        private ModSequence? _modSeq;

        // array of ModChannel objects for retrieving samples and tracking information on each channel
        private ModChannel[] _channels = new ModChannel[MAX_CHANNELS];

        // current position within the pattern table
        private int _currentPatternIndex;
        // current row within the current pattern
        private int _currentRow;
        // current tick within the row
        private int _currentTick;
        // the number of audio samples that must be generated to finish the current tick
        private float _samplesToNextTick;

        // the row to pattern break to on the conclusion of the current row; set to -1 when no break is pending
        private int _patternBreakRow;
        // the pattern index to jump to on the conclusion of the current row; set to -1 when no jump is pending
        private int _patternJumpIndex;
        // the row to jump to when a pattern loop command is reached; set to -1 when no loop is pending, resets to -1 at the start of each pattern
        private int _patternLoopRow;
        // the counter for pattern loops, set to 0 on effect E60
        private int _patternLoopCounter;
        // whether a loop will occur on conclusion of the current row
        private bool _patternLoopPending;

        // the number of audio samples to generate per MOD tick
        private float _samplesPerTick;
        // the number of ticks per row
        private int _ticksPerRow;

        // tables that map the sample period values to semi-tones
        private static int[][] periodTables;
        // sine table for vibrato and tremolo
        private static int[] sineTable = {
            0, 24, 49, 74, 97, 120, 141, 161,
            180, 197, 212, 224, 235, 244, 250, 253,
            255, 253, 250, 244, 235, 224, 212, 197,
            180, 191, 141, 120, 97, 74, 49, 24,
            0, -24, -49, -74, -97, -120, -141, -161,
            -180, -197, -212, -224, -235, -244, -250, -253,
            -255, -253, -250, -244, -235, -224, -212, -197,
            -180, -191, -141, -120, -97, -74, -49, -24,
        };
        // strings for keys
        private static string[] keyStrings = { "C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-" };

        public event EventHandler<EventArgs>? PlaybackStopped;

        static ModPlayer()
        {
            // populate the period tables
            // values taken from http://lclevy.free.fr/mo3/mod.txt
            periodTables = new int[16][];

            // table for Finetune 0
            periodTables[0] = new int[] {
                1712, 1616, 1524, 1440, 1356, 1280, 1208, 1140, 1076, 1016, 960 , 906,
                856 , 808 , 762 , 720 , 678 , 640 , 604 , 570 , 538 , 508 , 480 , 453,
                428 , 404 , 381 , 360 , 339 , 320 , 302 , 285 , 269 , 254 , 240 , 226,
                214 , 202 , 190 , 180 , 170 , 160 , 151 , 143 , 135 , 127 , 120 , 113,
                107 , 101 , 95  , 90  , 85  , 80  , 75  , 71  , 67  , 63  , 60  , 56
            };
            // table for Finetune +1
            periodTables[1] = new int[] {
                1700, 1604, 1514, 1430, 1348, 1274, 1202, 1134, 1070, 1010, 954 , 900,
                850 , 802 , 757 , 715 , 674 , 637 , 601 , 567 , 535 , 505 , 477 , 450,
                425 , 401 , 379 , 357 , 337 , 318 , 300 , 284 , 268 , 253 , 239 , 225,
                213 , 201 , 189 , 179 , 169 , 159 , 150 , 142 , 134 , 126 , 119 , 113,
                106 , 100 , 94  , 89  , 84  , 79  , 75  , 71  , 67  , 63  , 59  , 56
            };
            // table for Finetune +2
            periodTables[2] = new int[]
            {
                1688, 1592, 1504, 1418, 1340, 1264, 1194, 1126, 1064, 1004, 948 , 894,
                844 , 796 , 752 , 709 , 670 , 632 , 597 , 563 , 532 , 502 , 474 , 447,
                422 , 398 , 376 , 355 , 335 , 316 , 298 , 282 , 266 , 251 , 237 , 224,
                211 , 199 , 188 , 177 , 167 , 158 , 149 , 141 , 133 , 125 , 118 , 112,
                105 , 99  , 94  , 88  , 83  , 79  , 74  , 70  , 66  , 62  , 59  , 56
            };
            // table for Finetune +3
            periodTables[3] = new int[]
            {
                1676, 1582, 1492, 1408, 1330, 1256, 1184, 1118, 1056, 996 , 940 , 888,
                838 , 791 , 746 , 704 , 665 , 628 , 592 , 559 , 528 , 498 , 470 , 444,
                419 , 395 , 373 , 352 , 332 , 314 , 296 , 280 , 264 , 249 , 235 , 222,
                209 , 198 , 187 , 176 , 166 , 157 , 148 , 140 , 132 , 125 , 118 , 111,
                104 , 99  , 93  , 88  , 83  , 78  , 74  , 70  , 66  , 62  , 59  , 55
            };
            // table for Finetune +4
            periodTables[4] = new int[]
            {
                1664, 1570, 1482, 1398, 1320, 1246, 1176, 1110, 1048, 990 , 934 , 882,
                832 , 785 , 741 , 699 , 660 , 623 , 588 , 555 , 524 , 495 , 467 , 441,
                416 , 392 , 370 , 350 , 330 , 312 , 294 , 278 , 262 , 247 , 233 , 220,
                208 , 196 , 185 , 175 , 165 , 156 , 147 , 139 , 131 , 124 , 117 , 110,
                104 , 98  , 92  , 87  , 82  , 78  , 73  , 69  , 65  , 62  , 58  , 55
            };
            // table for Finetune +5
            periodTables[5] = new int[]
            {
                1652, 1558, 1472, 1388, 1310, 1238, 1168, 1102, 1040, 982 , 926 , 874,
                826 , 779 , 736 , 694 , 655 , 619 , 584 , 551 , 520 , 491 , 463 , 437,
                413 , 390 , 368 , 347 , 328 , 309 , 292 , 276 , 260 , 245 , 232 , 219,
                206 , 195 , 184 , 174 , 164 , 155 , 146 , 138 , 130 , 123 , 116 , 109,
                103 , 97  , 92  , 87  , 82  , 77  , 73  , 69  , 65  , 61  , 58  , 54
            };
            // table for Finetune +6
            periodTables[6] = new int[]
            {
                1640, 1548, 1460, 1378, 1302, 1228, 1160, 1094, 1032, 974 , 920 , 868,
                820 , 774 , 730 , 689 , 651 , 614 , 580 , 547 , 516 , 487 , 460 , 434,
                410 , 387 , 365 , 345 , 325 , 307 , 290 , 274 , 258 , 244 , 230 , 217,
                205 , 193 , 183 , 172 , 163 , 154 , 145 , 137 , 129 , 122 , 115 , 109,
                102 , 96  , 91  , 86  , 81  , 77  , 72  , 68  , 64  , 61  , 57  , 54
            };
            // table for Finetune +7
            periodTables[7] = new int[]
            {
                1628, 1536, 1450, 1368, 1292, 1220, 1150, 1086, 1026, 968 , 914 , 862,
                814 , 768 , 725 , 684 , 646 , 610 , 575 , 543 , 513 , 484 , 457 , 431,
                407 , 384 , 363 , 342 , 323 , 305 , 288 , 272 , 256 , 242 , 228 , 216,
                204 , 192 , 181 , 171 , 161 , 152 , 144 , 136 , 128 , 121 , 114 , 108,
                102 , 96  , 90  , 85  , 80  , 76  , 72  , 68  , 64  , 60  , 57  , 54
            };
            // table for Finetune -8
            periodTables[8] = new int[]
            {
                1814, 1712, 1616, 1524, 1440, 1356, 1280, 1208, 1140, 1076, 1016, 960,
                907 , 856 , 808 , 762 , 720 , 678 , 640 , 604 , 570 , 538 , 508 , 480,
                453 , 428 , 404 , 381 , 360 , 339 , 320 , 302 , 285 , 269 , 254 , 240,
                226 , 214 , 202 , 190 , 180 , 170 , 160 , 151 , 143 , 135 , 127 , 120,
                113 , 107 , 101 , 95  , 90  , 85  , 80  , 75  , 71  , 67  , 63  , 60
            };
            // table for Finetune -7
            periodTables[9] = new int[]
            {
                1800, 1700, 1604, 1514, 1430, 1350, 1272, 1202, 1134, 1070, 1010, 954,
                900 , 850 , 802 , 757 , 715 , 675 , 636 , 601 , 567 , 535 , 505 , 477,
                450 , 425 , 401 , 379 , 357 , 337 , 318 , 300 , 284 , 268 , 253 , 238,
                225 , 212 , 200 , 189 , 179 , 169 , 159 , 150 , 142 , 134 , 126 , 119,
                112 , 106 , 100 , 94  , 89  , 84  , 79  , 75  , 71  , 67  , 63  , 59
            };
            // table for Finetune -6
            periodTables[10] = new int[]
            {
                1788, 1688, 1592, 1504, 1418, 1340, 1264, 1194, 1126, 1064, 1004, 948,
                894 , 844 , 796 , 752 , 709 , 670 , 632 , 597 , 563 , 532 , 502 , 474,
                447 , 422 , 398 , 376 , 355 , 335 , 316 , 298 , 282 , 266 , 251 , 237,
                223 , 211 , 199 , 188 , 177 , 167 , 158 , 149 , 141 , 133 , 125 , 118,
                111 , 105 , 99  , 94  , 88  , 83  , 79  , 74  , 70  , 66  , 62  , 59
            };
            // table for Finetune -5
            periodTables[11] = new int[]
            {
                1774, 1676, 1582, 1492, 1408, 1330, 1256, 1184, 1118, 1056, 996 , 940,
                887 , 838 , 791 , 746 , 704 , 665 , 628 , 592 , 559 , 528 , 498 , 470,
                444 , 419 , 395 , 373 , 352 , 332 , 314 , 296 , 280 , 264 , 249 , 235,
                222 , 209 , 198 , 187 , 176 , 166 , 157 , 148 , 140 , 132 , 125 , 118,
                111 , 104 , 99  , 93  , 88  , 83  , 78  , 74  , 70  , 66  , 62  , 59
            };
            // table for Finetune -4
            periodTables[12] = new int[]
            {
                1762, 1664, 1570, 1482, 1398, 1320, 1246, 1176, 1110, 1048, 988 , 934,
                881 , 832 , 785 , 741 , 699 , 660 , 623 , 588 , 555 , 524 , 494 , 467,
                441 , 416 , 392 , 370 , 350 , 330 , 312 , 294 , 278 , 262 , 247 , 233,
                220 , 208 , 196 , 185 , 175 , 165 , 156 , 147 , 139 , 131 , 123 , 117,
                110 , 104 , 98  , 92  , 87  , 82  , 78  , 73  , 69  , 65  , 61  , 58
            };
            // table for Finetune -3
            periodTables[13] = new int[]
            {
                1750, 1652, 1558, 1472, 1388, 1310, 1238, 1168, 1102, 1040, 982 , 926,
                875 , 826 , 779 , 736 , 694 , 655 , 619 , 584 , 551 , 520 , 491 , 463,
                437 , 413 , 390 , 368 , 347 , 328 , 309 , 292 , 276 , 260 , 245 , 232,
                219 , 206 , 195 , 184 , 174 , 164 , 155 , 146 , 138 , 130 , 123 , 116,
                109 , 103 , 97  , 92  , 87  , 82  , 77  , 73  , 69  , 65  , 61  , 58
            };
            // table for Finetune -2
            periodTables[14] = new int[]
            {
                1736, 1640, 1548, 1460, 1378, 1302, 1228, 1160, 1094, 1032, 974 , 920,
                868 , 820 , 774 , 730 , 689 , 651 , 614 , 580 , 547 , 516 , 487 , 460,
                434 , 410 , 387 , 365 , 345 , 325 , 307 , 290 , 274 , 258 , 244 , 230,
                217 , 205 , 193 , 183 , 172 , 163 , 154 , 145 , 137 , 129 , 122 , 115,
                108 , 102 , 96  , 91  , 86  , 81  , 77  , 72  , 68  , 64  , 61  , 57
            };
            // table for Finetune -1
            periodTables[15] = new int[]
            {
                1724, 1628, 1536, 1450, 1368, 1292, 1220, 1150, 1086, 1026, 968 , 914,
                862 , 814 , 768 , 725 , 684 , 646 , 610 , 575 , 543 , 513 , 484 , 457,
                431 , 407 , 384 , 363 , 342 , 323 , 305 , 288 , 272 , 256 , 242 , 228,
                216 , 203 , 192 , 181 , 171 , 161 , 152 , 144 , 136 , 128 , 121 , 114,
                108 , 101 , 96  , 90  , 85  , 80  , 76  , 72  , 68  , 64  , 60  , 57
            };
        }

        public ModPlayer()
        {
            // set the wave format to a hard-coded 44Khz 16-bit Stereo
            _waveFormat = new WaveFormat(PLAYBACK_RATE, 16, 2);
            _samplesPerTick = PLAYBACK_RATE / 50f;

            // create the wave stream, reader, and writer
            _waveStream = new MemoryStream();
            _waveWriter = new BinaryWriter(_waveStream);
            _waveReader = new BinaryReader(_waveStream);

            // create and open the audio device
            _waveDevice = new WasapiOut(AudioClientShareMode.Shared, 50);
            // register the event handler for playback stopping
            _waveDevice.PlaybackStopped += waveDevice_PlaybackStopped;

            // open the device using this Player as the WaveProvider
            _waveDevice.Init(this);

            // create the channel objects
            for (int i = 0; i < MAX_CHANNELS; i++)
            {
                _channels[i] = new ModChannel(PLAYBACK_RATE);
            }
        }

        /// <summary>
        /// Loads a MOD file for playback. Cannot be called during playback.
        /// </summary>
        /// <param name="filename"></param>
        public void LoadMod(string filename)
        {
            // check arguments
            ArgumentNullException.ThrowIfNull(filename);
            // check state
            if (_waveDevice.PlaybackState != PlaybackState.Stopped) throw new InvalidOperationException("Cannot load a sequence while one is playing.");

            // load the sequence
            _modSeq = new ModSequence(filename);
        }

        /// <summary>
        /// Finds the closest note for the given instrument played at the specified period.
        /// </summary>
        /// <param name="instrument"></param>
        /// <param name="samplePeriod"></param>
        /// <returns></returns>
        private int periodToNote(ModInstrument instrument, int samplePeriod)
        {
            int note = -1;                  // the current note to return
            int bestDist = int.MaxValue;    // the distance from the current note to the given sample period

            // figure out which period table to use
            int ftIndex = instrument.FineTune;
            if (ftIndex < 0) ftIndex += 16;
            int[] periodTable = periodTables[ftIndex];

            // find the nearest period in the table
            for (int i = 0; i < periodTable.Length; i++)
            {
                // find the distance from the sample period to the current note
                int dist = Math.Abs(periodTable[i] - samplePeriod);
                // if the new distance is smaller than the best distance, update the note
                if (dist < bestDist)
                {
                    note = i;
                    bestDist = dist;
                }
                else
                {
                    // once the distance begins to increase then we've gone past the ideal note, it's time to return
                    break;
                }
            }

            // print a debug message to the console if an exact match was not found
            if (bestDist != 0)
            {
                Console.WriteLine("[PLAYER] Couldn't find exact period-to-note, using best fit (distance = {0})", bestDist);
            }
            return note;
        }

        private void synthReset()
        {
            // reset the waveform buffer by setting its length to 0
            _waveStream.Seek(0, SeekOrigin.Begin);
            _waveStream.SetLength(0);

            // reset all the channels
            for (int i = 0; i < MAX_CHANNELS; i++)
            {
                _channels[i].Reset();
            }

            // set all the pan settings to their initial defaults
            _channels[0].SetPan(64);
            _channels[1].SetPan(192);
            _channels[2].SetPan(192);
            _channels[3].SetPan(64);
        }

        /// <summary>
        /// Starts playback or resumes playback if it's paused.
        /// </summary>
        public void Play()
        {
            // check if a file is loaded
            if (_modSeq == null) throw new InvalidOperationException("MOD sequence has not been loaded.");

            // check if the file is not currently playing
            if (_waveDevice.PlaybackState == PlaybackState.Stopped)
            {
                _ticksPerRow = _modSeq.InitialSpeed;
                _samplesPerTick = PLAYBACK_RATE / 50f;
                _currentPatternIndex = 0;
                printPatternHeader();

                // set the current row and current tick to special values to ensure the synth works
                _currentRow = -1;
                _currentTick = _ticksPerRow;
                _samplesToNextTick = 0;

                _patternBreakRow = -1;
                _patternJumpIndex = -1;
                _patternLoopRow = -1;
                _patternLoopCounter = 0;
                _patternLoopPending = false;

                // reset the synth's sate
                synthReset();

                _waveDevice.Play();
            }
            else if (_waveDevice.PlaybackState == PlaybackState.Paused)
            {
                // resume playback
                _waveDevice.Play();
            }
        }

        /// <summary>
        /// Pauses playback.
        /// </summary>
        public void Pause()
        {
            // check if a file is loaded
            if (_modSeq == null) throw new InvalidOperationException("MOD sequence has not been loaded.");

            if (_waveDevice.PlaybackState == PlaybackState.Playing)
            {
                _waveDevice.Pause();
            }
        }

        /// <summary>
        /// Stops playback.
        /// </summary>
        public void Stop()
        {
            // check if a file is loaded
            if (_modSeq == null) throw new InvalidOperationException("MOD sequence has not been loaded.");

            if (_waveDevice.PlaybackState != PlaybackState.Stopped)
            {
                _waveDevice.Stop();
            }
        }

        public void SkipPattern()
        {
            _currentPatternIndex++;
            printPatternHeader();
            _currentRow = -1;
            _currentTick = _ticksPerRow;
            _samplesToNextTick = 0;
        }

        protected virtual void OnPlaybackStopped(EventArgs e)
        {
            EventHandler<EventArgs> handler = PlaybackStopped!;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void waveDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            Console.WriteLine("[PLAYER] Playback has stopped.");

            EventArgs args = new EventArgs();
            OnPlaybackStopped(args);
        }

        private void printPatternHeader()
        {
            Console.WriteLine("--- Pattern {0,2} ---", _modSeq!.PatternTable(_currentPatternIndex));
        }

        // common code invoked when performing a Note-On
        private void noteOn(int channelIndex, ModPatternCell cell)
        {
            // if sample and period number are undefined then this is not a note-on
            if ((cell.SamplePeriod == 0) && (cell.SampleNumber == 0)) return;

            // handle if the sample period is not provided
            if (cell.SamplePeriod != 0)
            {
                // handle if instrument is not provided
                if (cell.SampleNumber != 0)
                {
                    // play note normally
                    _channels[channelIndex].NoteOn(_modSeq!.Instruments(cell.SampleNumber - 1), cell.SamplePeriod);
                    // set the note's volume to the initial instrument's initial volume
                    _channels[channelIndex].Volume = _channels[channelIndex].Instrument.InitialVolume;
                }
                else
                {
                    // play the note, but using the last played instrument
                    _channels[channelIndex].NoteOn(_channels[channelIndex].Instrument, cell.SamplePeriod);
                }
                
            }
            else
            {
                // sample period is not provided, we do not start a new note, but in fact reset volume and change instrument
                // on-the-fly instrument change
                _channels[channelIndex].Instrument = _modSeq!.Instruments(cell.SampleNumber - 1);
                _channels[channelIndex].Volume = _channels[channelIndex].Instrument.InitialVolume;
            }
        }

        // generates samples and writes them to the audio buffer
        private void generateSamples(int numSamples)
        {
            // loop for the number of samples
            for (int i = 0; i < numSamples; i++)
            {
                float leftAccumulator = 0;
                float rightAccumulator = 0;

                // loop through all the active voices, grabbing the next sample
                for (int channelIndex = 0; channelIndex < MAX_CHANNELS; channelIndex++)
                {
                    ModChannel channel = _channels[channelIndex];

                    // skip inactive voices
                    if (!channel.IsPlaying) continue;

                    // get the voice's sample
                    float sample = channel.GetSample();

                    // apply global forced volume multiplier
                    sample *= VOLUME_MULTIPLIER;

                    leftAccumulator += sample * channel.PanLeft;
                    rightAccumulator += sample * channel.PanRight;
                }

                // clamp the accumulators to the valid range
                leftAccumulator = Math.Min(Math.Max(leftAccumulator, short.MinValue), short.MaxValue);
                rightAccumulator = Math.Min(Math.Max(rightAccumulator, short.MinValue), short.MaxValue);

                // write the accumulators to the buffer
                _waveWriter.Write((short)leftAccumulator);
                _waveWriter.Write((short)rightAccumulator);
            }
        }

        #region IWaveProvider

        public WaveFormat WaveFormat => _waveFormat;

        /// <summary>
        /// Method called by the wave output device when it wants data.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public int Read(byte[] buffer, int offset, int count)
        {
            // determine how many samples the device is asking for
            int bufferSize = count / _waveFormat.BlockAlign;
            int numSamplesMade = 0;

            // if the song has reached the end, return 0 bytes
            if (_currentPatternIndex >= _modSeq!.SongLength) return 0;

            // generate samples
            while (numSamplesMade < bufferSize)
            {
                // generate samples until we either reach the next tick or we fill the buffer
                int samplesToGen = (int)Math.Min(Math.Floor(_samplesToNextTick), bufferSize - numSamplesMade);

                // generate that number of samples
                generateSamples(samplesToGen);
                numSamplesMade += samplesToGen;

                // subtract the number of samples we've generated from the number needed to reach the next tick
                _samplesToNextTick -= samplesToGen;

                // if the number of samples to reach the next tick is >= 1, then continue with the loop
                // (and we should break out automatically)
                if (_samplesToNextTick >= 1) continue;

                // we've reached the next tick, increment the tick counter
                _currentTick++;

                // check if we've finished the current row
                if (_currentTick >= _ticksPerRow)
                {
                    // reset the tick counter
                    _currentTick = 0;

                    // if there's a pattern break pending, do it
                    if (_patternBreakRow != -1)
                    {
                        _currentRow = _patternBreakRow;
                        _patternBreakRow = -1;
                        _currentPatternIndex++;
                        printPatternHeader();
                    }
                    else if (_patternLoopPending)   // if there's a pattern loop pending, do it
                    {
                        _currentRow = _patternLoopRow;
                        _patternLoopCounter++;
                        _patternLoopPending = false;
                    }
                    else
                    {
                        // increment the row as normal
                        _currentRow++;
                    }

                    // if there's a pattern jump pending, do it
                    if (_patternJumpIndex != -1)
                    {
                        _currentRow = 0;
                        _currentPatternIndex = _patternJumpIndex;
                        _patternJumpIndex = -1;
                        printPatternHeader();
                    }

                    // check if we've finished the current pattern
                    if (_currentRow >= 64)
                    {
                        // reset the row counter and advance to the next pattern
                        _currentRow = 0;
                        _currentPatternIndex++;

                        // clear pattern loop data; loops cannot cross pattern boundaries
                        _patternLoopRow = -1;
                        _patternLoopCounter = 0;
                        _patternLoopPending = false;

                        // if we've reached the end of the song, break out of the sample-generating loop
                        if (_currentPatternIndex >= _modSeq!.SongLength) break;

                        printPatternHeader();
                    }
                }

                // process things for the current tick on the current row
                ModPatternRow row = _modSeq!.Patterns(_modSeq.PatternTable(_currentPatternIndex)).Rows(_currentRow);
                for (int channelIndex = 0; channelIndex < _modSeq.NumChannels; channelIndex++)
                {
                    ModPatternCell cell = row.Cells(channelIndex);
                    ModChannel channel = _channels[channelIndex];

                    // if the cell is empty, we can skip to the next one
                    if (cell.Effect == ModEffects.Empty) continue;

                    // handle based on effect
                    switch (cell.Effect)
                    {
                        case ModEffects.Arpeggio:
                            // perform effect setup only on the first tick of the row
                            if (_currentTick == 0)
                            {
                                // start the note
                                noteOn(channelIndex, cell);

                                // there is no effect memory, so prepare for the arpeggio effect only if the argument is non-zero
                                if (cell.EffectArgument != 0)
                                {
                                    channel.ArpeggioCounter = 0;
                                    int periodBase = periodToNote(channel.Instrument, channel.SamplePeriodBase);

                                    // if the base was not found, we got a problem!
                                    if (periodBase == -1) throw new InvalidOperationException("Problem with arpeggio");

                                    int ftIndex = channel.Instrument.FineTune;
                                    if (ftIndex < 0) ftIndex += 16;
                                    int[] periodTable = periodTables[ftIndex];

                                    int arpStep1 = (cell.EffectArgument & 0xF0) >> 4;
                                    int arpStep2 = cell.EffectArgument & 0xF;

                                    channel.ArpeggioStep1 = periodTable[periodBase + arpStep1];
                                    channel.ArpeggioStep2 = periodTable[periodBase + arpStep2];
                                }
                                
                            }
                            // if the effect was non-zero we are now primed for the arpeggio code
                            // only perform the arpeggio effect if the argument is non-zero
                            if (cell.EffectArgument != 0)
                            {
                                // increment the arpeggio counter
                                channel.ArpeggioCounter++;
                                if (channel.ArpeggioCounter % 3 == 0)
                                {
                                    channel.SamplePeriod = channel.SamplePeriodBase;
                                }
                                else if (channel.ArpeggioCounter % 3 == 1)
                                {
                                    channel.SamplePeriod = channel.ArpeggioStep1;
                                }
                                else
                                {
                                    channel.SamplePeriod = channel.ArpeggioStep2;
                                }


                            }
                            break;

                        case ModEffects.SetFilter:
                            // this effect only matters on the first tick
                            if (_currentTick == 0)
                            {
                                // can perform a note-on if this is the first tick in the row
                                noteOn(channelIndex, cell);

                                Console.WriteLine("[PLAYER] Ch.{0}, Row {1} - Unimplemented SetFilter {2}", channelIndex + 1, _currentRow, cell.EffectArgument);
                            }
                            break;

                        case ModEffects.SetSpeed:
                            // this effect only matters on the first tick
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);

                                // changes the speed of playback
                                if (cell.EffectArgument == 0)
                                {
                                    // TODO: should stop playback
                                    throw new NotImplementedException();
                                }
                                else if (cell.EffectArgument >= 32)
                                {
                                    // some other way of setting tempo, effect argument is BPM
                                    _samplesPerTick = (float)(PLAYBACK_RATE / (cell.EffectArgument * 0.4));
                                }
                                else
                                {
                                    _ticksPerRow = cell.EffectArgument;
                                }
                            }
                            break;

                        case ModEffects.SetVolume:
                            // this effect only matters on the first tick
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);

                                // set the volume of the current note
                                channel.Volume = cell.EffectArgument;
                            }
                            break;

                        case ModEffects.SetPanning:
                            // this effect only matters on the first tick
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);

                                // set the pan of the current note
                                channel.SetPan(cell.EffectArgument);
                            }
                            break;

                        case ModEffects.PatternBreak:
                            // this effect only matters on the first tick
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);

                                // on the conclusion of this row, jump to the next pattern in the table
                                // and set the row to a specific value
                                _patternBreakRow = ((cell.EffectArgument & 0xF0) >> 4) * 10 + (cell.EffectArgument & 0xF);
                            }
                            break;

                        case ModEffects.PositionJump:
                            // this effect only matters on the first tick
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);

                                // on the conclusion of this row, jump to a position in the pattern table
                                _patternJumpIndex = cell.EffectArgument;
                            }
                            break;

                        case ModEffects.PatternLoop:
                            // this effect only matters on the first tick
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);

                                // handle the effect argument
                                if (cell.EffectArgument == 0)
                                {
                                    // only reset the counter if the current loop row is not the same as the current row
                                    // (this avoids us getting stuck in an infinite loop)
                                    if (_patternLoopRow != _currentRow) _patternLoopCounter = 0;
                                    // set the loop return row
                                    _patternLoopRow = _currentRow;
                                }
                                else
                                {
                                    // if the loop counter is less than the argument, then a loop will occur on conclusion of this row
                                    if (_patternLoopCounter < cell.EffectArgument) _patternLoopPending = true;   
                                }
                            }
                            break;

                        case ModEffects.VolumeSlide:
                            // branch based on current tick
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);

                                // set the volume slide value
                                if ((cell.EffectArgument & 0xF0) != 0)
                                {
                                    channel.VolumeSlideValue = ((cell.EffectArgument & 0xF0) >> 4);
                                }
                                else
                                {
                                    channel.VolumeSlideValue = -(cell.EffectArgument & 0xF);
                                }
                            }
                            else
                            {
                                // slide volume up or down on each tick of the row except the first
                                channel.Volume += channel.VolumeSlideValue;
                            }
                            break;

                        case ModEffects.Vibrato:
                            // the first tick is used to set the effect arguments
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);

                                // if set non-zero portions of the cell's effect argument, for effect memory
                                if ((cell.EffectArgument & 0xF0) != 0)
                                {
                                    // vibrato speed
                                    channel.VibratoSpeed = cell.EffectArgument >> 4;
                                }
                                if ((cell.EffectArgument & 0xF) != 0)
                                {
                                    // vibrato depth
                                    channel.VibratoDepth = cell.EffectArgument & 0xF;
                                }
                            }
                            // modify the note's period with the table
                            channel.SamplePeriod = channel.SamplePeriodBase + (sineTable[channel.VibratoPosition] * channel.VibratoDepth) / 128;

                            // advance the vibrato position
                            channel.VibratoPosition += channel.VibratoSpeed;
                            if (channel.VibratoPosition >= 64) channel.VibratoPosition -= 64;
                            break;

                        case ModEffects.VibratoVolumeSlide:
                            // behaves like Axy with 400
                            // branch based on current tick
                            if (_currentTick == 0)
                            {
                                // in theory, a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);

                                // set the volume slide value
                                if ((cell.EffectArgument & 0xF0) != 0)
                                {
                                    channel.VolumeSlideValue = ((cell.EffectArgument & 0xF0) >> 4);
                                }
                                else
                                {
                                    channel.VolumeSlideValue = -(cell.EffectArgument & 0xF);
                                }
                            }
                            else
                            {
                                // slide volume up or down on each tick of the row except the first
                                channel.Volume += channel.VolumeSlideValue;
                            }

                            // and then handle the continuation of vibrato
                            // modify the note's period with the table
                            channel.SamplePeriod = channel.SamplePeriodBase + (sineTable[channel.VibratoPosition] * channel.VibratoDepth) / 128;

                            // advance the vibrato position
                            channel.VibratoPosition += channel.VibratoSpeed;
                            if (channel.VibratoPosition >= 64) channel.VibratoPosition -= 64;
                            break;

                        case ModEffects.PortamentoUp:
                        case ModEffects.PortamentoDown:
                            // branch based on current tick
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);
                            }
                            else
                            {
                                if (cell.Effect == ModEffects.PortamentoDown)
                                {
                                    // decreases note pitch by xx units on every tick of the row except the first
                                    channel.SamplePeriod += cell.EffectArgument;
                                }
                                else
                                {
                                    // increases current note pitch by xx units on every tick of the row except the first
                                    channel.SamplePeriod -= cell.EffectArgument;
                                }
                            }
                            break;

                        case ModEffects.TonePortamento:
                            // Tone portamento NEVER triggers a note-on; it only alters a note that is already playing.
                            // if a sample period is defined, then set the new portamento target; otherwise use the current target.
                            // branch based on current tick
                            // Tone Portamento WILL reset note volume, however
                            if (_currentTick == 0)
                            {
                                // reset tone portamento speed to 0 to help avoid edge cases of "orphaned" effects shifting the note in undesired ways
                                channel.TonePortamentoSpeed = 0;
                                // if sample period is defined
                                if (cell.SamplePeriod != 0)
                                {
                                    channel.TonePortamentoPeriodTarget = cell.SamplePeriod;
                                    // reset the note volume ONLY IF there's an instrument defined in the cell
                                    if (cell.SampleNumber != 0) channel.Volume = channel.Instrument.InitialVolume;
                                    // and also change the period base to the new sample period
                                    channel.SamplePeriodBase = cell.SamplePeriod;
                                }
                                // if effect argument is non-zero AND cell period is non-zero, set it
                                if ((cell.EffectArgument != 0) && (cell.SamplePeriod != 0))
                                {
                                    channel.TonePortamentoSpeed = cell.EffectArgument;
                                }

                            }
                            // NOTE: While most Note effects do not actually "do their thing" on the first tick, Tone Portamento DOES.
                            // At least, it sounds much closer to how it should when I allow it to occur on the first tick

                            // slide the pitch of the previous note towards the target note by xx units on every tick of the row except the first
                            if (channel.SamplePeriod != channel.TonePortamentoPeriodTarget)
                            {
                                // determine how much to slide by
                                int slideRate = Math.Min(
                                    Math.Abs(channel.TonePortamentoPeriodTarget - channel.SamplePeriod),
                                    channel.TonePortamentoSpeed
                                    );
                                
                                // apply the proper sign to the slide
                                slideRate *= Math.Sign(channel.TonePortamentoPeriodTarget - channel.SamplePeriod);

                                // perform the slide
                                channel.SamplePeriod += slideRate;
                            }
                            break;

                        case ModEffects.TonePortamentoVolumeSlide:
                            // behaves like Axy with 300
                            // branch based on current tick
                            if (_currentTick == 0)
                            {
                                // it is highly unlikely that note-ons can occur alongside this effect
                                // set the volume slide value
                                if ((cell.EffectArgument & 0xF0) != 0)
                                {
                                    channel.VolumeSlideValue = ((cell.EffectArgument & 0xF0) >> 4);
                                }
                                else
                                {
                                    channel.VolumeSlideValue = -(cell.EffectArgument & 0xF);
                                }
                            }
                            else
                            {
                                // slide volume up or down on each tick of the row except the first
                                channel.Volume += channel.VolumeSlideValue;

                                // perform the tone-portamento portion of the effect
                                // slide the pitch of the previous note towards the target note by xx units on every tick of the row except the first
                                if (channel.SamplePeriod != channel.TonePortamentoPeriodTarget)
                                {
                                    // determine how much to slide by
                                    int slideRate = Math.Min(
                                        Math.Abs(channel.TonePortamentoPeriodTarget - channel.SamplePeriod),
                                        channel.TonePortamentoSpeed
                                        );

                                    // apply the proper sign to the slide
                                    slideRate *= Math.Sign(channel.TonePortamentoPeriodTarget - channel.SamplePeriod);

                                    // perform the slide
                                    channel.SamplePeriod += slideRate;
                                }
                            }
                            break;

                        case ModEffects.SetFinetune:
                            // this effect only matters on the first tick
                            if (_currentTick == 0)
                            {
                                // overrides the finetune value for the currently playing note
                                // this command only works with a note next to it
                                if (cell.SamplePeriod != 0)
                                {
                                    noteOn(channelIndex, cell);
                                    channel.SetFineTune(cell.EffectArgument);
                                }
                            }
                            break;

                        case ModEffects.SampleOffset:
                            // this effect only matters on the first tick
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);

                                // "starts playing the current sample from position xx * 256 instead of position 0"
                                // "ineffective if there is no note in the same pattern cell"
                                channel.SamplePosition = cell.EffectArgument * 256;
                            }
                            break;

                        case ModEffects.Retrigger:
                            // retriggers the current note every x ticks
                            // TODO: implement this: "This effect works with parameters greater than the current Speed setting if the row after it also contains an E9x effect."
                            if (_currentTick % cell.EffectArgument == 0)
                            {
                                // if instrument and period are non-zero use the current cell's value
                                if (cell.SamplePeriod != 0)
                                {
                                    noteOn(channelIndex, cell);
                                }
                                else
                                {
                                    // retrigger the currently active note, but save the volume
                                    int rtVolume = channel.Volume;
                                    channel.NoteOn(channel.Instrument, channel.SamplePeriod);
                                    channel.Volume = rtVolume;
                                }
                                
                            }
                            break;

                        case ModEffects.FineVolumeSlideDown:
                            // this effect only matters on the first tick
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);
                                // instantly decrease volume by the specified amount
                                channel.Volume -= cell.EffectArgument;
                            }
                            break;

                        case ModEffects.FineVolumeSlideUp:
                            // this effect only matters on the first tick
                            if (_currentTick == 0)
                            {
                                // a note-on can occur alongside this effect
                                noteOn(channelIndex, cell);
                                // instantly incease volume by the specified amount
                                channel.Volume += cell.EffectArgument;
                            }
                            break;

                        case ModEffects.NoteCut:
                            // perform a note-on on the first tick
                            if (_currentTick == 0)
                            {
                                noteOn(channelIndex, cell);
                            }
                            // set the note volume to 0 after x ticks have passed
                            if (_currentTick >= cell.EffectArgument)
                            {
                                channel.Volume = 0;
                            }
                            break;

                        case ModEffects.NoteDelay:
                            // don't play the note until the tick specified by the effect argument has been reached
                            if (_currentTick == cell.EffectArgument)
                            {
                                noteOn(channelIndex, cell);
                            }
                            break;

                        default:
                            // unhandled effect
                            throw new Exception(string.Format("Unhandled Note Effect {0}", cell.Effect));
                    }
                }

                // tick has been processed, increment the number of samples to the next tick
                _samplesToNextTick += _samplesPerTick;
            }

            // copy the bytes from the synth to the buffer
            _waveStream.Seek(0, SeekOrigin.Begin);

            byte[] synthBytes = _waveReader.ReadBytes((int)_waveStream.Length);
            Array.Copy(synthBytes, buffer, synthBytes.Length);

            // clear the buffer
            _waveStream.Seek(0, SeekOrigin.Begin);
            _waveStream.SetLength(0);

            return synthBytes.Length;
        }

        #endregion
        #region IDisposable
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ModPlayer()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
