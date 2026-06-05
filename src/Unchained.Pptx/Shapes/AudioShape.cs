using Unchained.Pptx.Media;

namespace Unchained.Pptx.Shapes;

/// <summary>
/// A shape that plays an audio clip embedded in or linked from the presentation.
/// Full playback is not supported in M1–M4; this class preserves the audio metadata
/// and bytes faithfully through load/save round-trips.
/// </summary>
public sealed class AudioShape : Shape
{
    /// <summary>The audio clip. <see langword="null"/> if the clip could not be resolved.</summary>
    public EmbeddedAudio? Audio { get; set; }

    /// <summary><see langword="true"/> when playback starts automatically on slide entry.</summary>
    public bool AutoPlay { get; set; }

    /// <summary><see langword="true"/> when the speaker icon is hidden during playback.</summary>
    public bool HideIcon { get; set; }

    /// <summary><see langword="true"/> when playback loops continuously.</summary>
    public bool Loop { get; set; }

    /// <summary><see langword="true"/> when playback continues across slide transitions.</summary>
    public bool PlayAcrossSlides { get; set; }
}
