using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModPlayer.MOD
{
    public enum ModEffects
    {
        Arpeggio = 0,
        PortamentoUp,
        PortamentoDown,
        TonePortamento,
        Vibrato,
        TonePortamentoVolumeSlide,
        VibratoVolumeSlide,
        Tremolo,
        SetPanning,
        SampleOffset,
        VolumeSlide,
        PositionJump,
        SetVolume,
        PatternBreak,
        ExtendedEffect,     // not actually used, only here for spacing
        SetSpeed,

        // Extended Effects are below
        SetFilter = 0xE0,
        FinePortamentoUp,
        FinePortamentoDown,
        GlissandoControl,
        SetVibratoWaveform,
        SetFinetune,
        PatternLoop,
        SetTremoloWaveform,
        CoarseSetPanning,
        Retrigger,
        FineVolumeSlideUp,
        FineVolumeSlideDown,
        NoteCut,
        NoteDelay,
        PatternDelay,
        InvertLoop,

        // Special sentinel value for indicating a completely empty cell (no period value)
        Empty = 0x420,
    }

    /// <summary>
    /// Represents the type of interpolation to use when retrieving a sample at a given position.
    /// </summary>
    public enum SampleInterpolation
    {
        /// <summary>
        /// Nearest-neighbor interpolation. Lowest quality.
        /// </summary>
        NearestNeighbor = 0,

        /// <summary>
        /// Linear interpolation. Low quality.
        /// </summary>
        Linear,
    }
}
