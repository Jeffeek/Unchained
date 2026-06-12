using Unchained.Ooxml.Media;

namespace Unchained.Pptx.Shapes;

/// <summary>
///     A shape that plays a video clip embedded in or linked from the presentation.
///     Full playback is not supported in M1–M4; this class preserves the video metadata
///     and bytes faithfully through load/save round-trips.
/// </summary>
public sealed class VideoShape : Shape
{
    /// <summary>The video clip. <see langword="null" /> if the clip could not be resolved.</summary>
    public EmbeddedVideo? Video { get; set; }

    /// <summary>
    ///     A still image displayed before playback begins (poster frame).
    ///     <see langword="null" /> if no poster frame is set.
    /// </summary>
    public EmbeddedImage? PosterFrame { get; set; }

    /// <summary><see langword="true" /> when playback starts automatically on slide entry.</summary>
    public bool AutoPlay { get; set; }

    /// <summary><see langword="true" /> when playback loops continuously.</summary>
    public bool Loop { get; set; }

    /// <summary><see langword="true" /> when the video icon is hidden when the video is not playing.</summary>
    public bool HideWhenStopped { get; set; }
}
