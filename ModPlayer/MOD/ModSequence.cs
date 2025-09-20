using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModPlayer.MOD
{
    /// <summary>
    /// Encapsulates a MOD sequence (song/module).
    /// </summary>
    internal class ModSequence
    {
        // song name
        private string? _songName;

        // the number of channels the song supports/uses, defaults to 4
        private int _numChannels = 4;
        // the number of instruments that are defined for the song, defaults to 31
        private int _numInstruments = 31;
        private ModInstrument[]? _instruments;
        // the number of patterns played in the song (not the total number of patterns in the file)
        private int _numSongPatterns;
        // the number of patterns defined within the file
        private int _numPatterns;
        // the array of patterns
        private ModPattern[]? _patterns;
        // the song end jump position
        private int _songEndJumpPosition;
        // the song pattern table
        private byte[]? _patternTable;

        // the song's initial speed (ticks per row)
        private int _initialSpeed = 6;

        /// <summary>
        /// Creates a new instance of this class using the provided stream.
        /// </summary>
        /// <param name="stream"></param>
        private void createFromStream(Stream stream)
        {
            // check arguments
            ArgumentNullException.ThrowIfNull(stream);

            // remember the initial position of the stream
            long startPosition = stream.Position;

            BinaryReader br = new BinaryReader(stream);

            // seek to offset 1080 to read the file format tag
            stream.Seek(1080, SeekOrigin.Current);
            string formatTag = StringHelper.TrimNull(Encoding.ASCII.GetString(br.ReadBytes(4)));

            // determine the number of channels/instruments from the tag
            switch (formatTag)
            {
                case "M.K.":
                case "FLT4":
                case "M!K!":
                case "4CHN":
                    // standard tags; 4 channels, 31 instruments
                    _numChannels = 4;
                    _numInstruments = 31;
                    break;

                case "6CHN":
                    // standard tag; 6 channels, 31 instruments
                    _numChannels = 6;
                    _numInstruments = 31;
                    break;

                case "8CHN":
                case "OCTA":
                    // standard tags; 8 channels, 31 instruments
                    _numChannels = 8;
                    _numInstruments = 31;
                    break;

                default:
                    // file format is unrecognized
                    throw new InvalidDataException(String.Format("Unrecognized MOD format tag '{0}'", formatTag));
            }

            // create the array of instruments
            _instruments = new ModInstrument[_numInstruments];

            // seek back to read the number of patterns
            stream.Seek(startPosition + 950, SeekOrigin.Begin);
            _numSongPatterns = br.ReadByte();
            _songEndJumpPosition = br.ReadByte();

            // read the song pattern table
            _patternTable = br.ReadBytes(128);

            // determine how many patterns are defined in the file by finding the highest pattern number in the table,
            // then adding 1
            _numPatterns = 0;
            for (int i = 0; i < 128; i++)
            {
                _numPatterns = Math.Max(_numPatterns, _patternTable[i]);
            }
            _numPatterns++;
            _patterns = new ModPattern[_numPatterns];

            // determine the position where the sample data begins
            // (4 bytes * num channels * 64 lines)
            long sampleDataPosition = startPosition + 1084 + (_numPatterns * _numChannels * 256);
            // the offset within the sample data for the next instrument
            long sampleDataOffset = 0;

            // seek back to the start to read in more data
            stream.Seek(startPosition, SeekOrigin.Begin);

            // read the song name
            _songName = StringHelper.TrimNull(Encoding.ASCII.GetString(br.ReadBytes(20)));

            // read in the data for each of the instruments
            for (int i = 0; i < _numInstruments; i++)
            {
                // seek to the instrument's record
                stream.Seek(startPosition + 20 + i * 30, SeekOrigin.Begin);

                // read in the record's data
                string sampleName = StringHelper.TrimNull(Encoding.ASCII.GetString(br.ReadBytes(22)));
                int sampleLength = EndianHelper.SwapEndianU16(br.ReadUInt16()) * 2;
                byte rawFineTune = br.ReadByte();
                byte sampleLinearVolume = br.ReadByte();
                int sampleRepeatOffset = EndianHelper.SwapEndianU16(br.ReadUInt16()) * 2;
                int sampleRepeatLength = EndianHelper.SwapEndianU16(br.ReadUInt16()) * 2;

                // skip the instrument if no sample data is defined for it (sampleLength < 3)
                if (sampleLength < 3) continue;

                // convert the raw finetune value to the actual finetune value
                sbyte sampleFineTune;
                if (rawFineTune >= 8)
                {
                    sampleFineTune = (sbyte)(16 - rawFineTune);
                }
                else
                {
                    sampleFineTune = (sbyte)rawFineTune;
                }

                // seek to the start of the instrument's sample data and read it in as a raw array of bytes
                // (the sample data is actually 8-bit signed bytes)
                stream.Seek(sampleDataPosition + sampleDataOffset, SeekOrigin.Begin);

                // create the proper array and use a Buffer to bulk-copy
                sbyte[] sampleData = new sbyte[sampleLength];
                Buffer.BlockCopy(br.ReadBytes(sampleLength), 0, sampleData, 0, sampleLength);

                // increment the sample data offset for the next instrument
                sampleDataOffset += sampleLength;

                // create the ModInstrument object
                _instruments[i] = new ModInstrument(sampleName,
                    sampleFineTune,
                    sampleLinearVolume,
                    sampleRepeatOffset,
                    sampleRepeatLength,
                    sampleData);
            }

            // read in the song's patterns
            stream.Seek(startPosition + 1084, SeekOrigin.Begin);
            for (int patternIndex = 0; patternIndex < _numPatterns; patternIndex++)
            {
                // create the array for the rows in the pattern
                ModPatternRow[] rows = new ModPatternRow[64];

                // process the rows in the pattern
                for (int rowIndex = 0; rowIndex < 64; rowIndex++)
                {
                    // create the array of ModPatternCells for this row
                    ModPatternCell[] cells = new ModPatternCell[_numChannels];

                    // channel loop
                    for (int channel = 0; channel < _numChannels; channel++)
                    {
                        // read in the 32 bits used for the cell and extract the relevant information from it
                        uint cellData = br.ReadUInt32();

                        int sampleNumber = (int)(((cellData & 0xF00000) >> 20) | (cellData & 0xF0));
                        int samplePeriod = (int)(((cellData & 0xFF00) >> 8) | ((cellData & 0xF) << 8));
                        int rawEffect = (int)((cellData & 0xF0000) >> 16);
                        ModEffects effect;
                        int effectArgument = (int)((cellData & 0xFF000000) >> 24);

                        // handle extended effects
                        if (rawEffect == 0xE)
                        {
                            // the effect is extended, remap its value
                            effect = (ModEffects)((rawEffect << 4) | ((effectArgument & 0xF0) >> 4));
                            // trim the effect argument to just the low nibble
                            effectArgument &= 0xF;
                        }
                        else if (cellData == 0)
                        {
                            // the cell is empty, so nothing happens whatsoever, use the sentinel value
                            effect = ModEffects.Empty;
                        }
                        else
                        {
                            effect = (ModEffects)rawEffect;
                        }

                        // create the cell and add it to the array
                        cells[channel] = new ModPatternCell(sampleNumber, samplePeriod, effect, effectArgument);
                    }

                    // set the entry in the array
                    rows[rowIndex] = new ModPatternRow(cells);
                }

                // create the pattern object
                _patterns[patternIndex] = new ModPattern(rows);
            }

            // finally, change the song's initial speed from the default if the very first note
            // in the very first played pattern has a SetTempo effect
            ModPatternCell firstCell = _patterns[_patternTable[0]].Rows(0).Cells(0);
            if (firstCell.Effect == ModEffects.SetSpeed)
            {
                _initialSpeed = firstCell.EffectArgument;
            }
        }

        /// <summary>
        /// Initializes a new instance of the ModSequence class from the specified file.
        /// </summary>
        /// <param name="filename"></param>
        public ModSequence(string filename)
        {
            // check arguments
            ArgumentNullException.ThrowIfNull(filename);
            // make sure the file exists
            FileInfo fileInfo = new FileInfo(filename);
            if (!fileInfo.Exists) throw new FileNotFoundException(filename);

            // attempt to load the file
            FileStream fStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

            createFromStream(fStream);

            // close the file stream
            fStream.Close();
        }

        public string SongName => _songName!;

        public int NumChannels => _numChannels;
        public int NumInstruments => _numInstruments;
        public ModInstrument Instruments(int index)
        {
            // check arguments
            if ((index < 0) || (index >= _instruments!.Length)) throw new ArgumentOutOfRangeException("index");

            return _instruments[index];
        }

        /// <summary>
        /// The length of the song in patterns.
        /// </summary>
        public int SongLength => _numSongPatterns;

        /// <summary>
        /// The number of patterns defined within the file.
        /// </summary>
        public int NumPatterns => _numPatterns;

        public ModPattern Patterns(int index)
        {
            // check arguments
            if ((index < 0) || (index >= _patterns!.Length)) throw new ArgumentOutOfRangeException("index");

            return _patterns[index];
        }

        public int SongEndJumpPosition => _songEndJumpPosition;
        public int PatternTable(int index)
        {
            if ((index < 0) || (index >= _patternTable!.Length)) throw new ArgumentOutOfRangeException("index");

            return _patternTable[index];
        }
        public int InitialSpeed => _initialSpeed;
    }
}
