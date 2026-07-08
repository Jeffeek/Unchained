namespace Unchained.Pptx.Writing;

/// <summary>
///     OOXML <c>dir</c> attribute values used by <c>TransitionWriter</c> and <c>TransitionParser</c>.
///     Also used by <c>TextWriter</c> for text body direction.
/// </summary>
internal static class TransitionStrings
{
    /// <summary>Direction: horizontal (transitions, text body).</summary>
    public const string DirHorz = "horz";

    /// <summary>Direction: vertical (transitions, text body).</summary>
    public const string DirVert = "vert";

    /// <summary>Direction: vertical 270°.</summary>
    public const string DirVert270 = "vert270";

    /// <summary>Direction: mongolian vertical (stacked).</summary>
    public const string DirMongolianVert = "mongolianVert";

    /// <summary>Zoom direction: in.</summary>
    public const string ZoomDirIn = "in";

    /// <summary>Zoom direction: out.</summary>
    public const string ZoomDirOut = "out";
}
