namespace Unchained.Pdf.Models;

/// <summary>
///     Styling for text drawn onto an existing page via
///     <see cref="Abstractions.IPageContentEditor.DrawTextAsync" />.
/// </summary>
/// <param name="FontName">
///     Base font name for the Type1 font resource (a Standard 14 name such as
///     <c>Helvetica</c>, <c>Times-Roman</c>, or <c>Courier</c>).
/// </param>
/// <param name="FontSize">Font size in points.</param>
/// <param name="Color">
///     Fill colour as an RGB triple with components in [0, 1]. Defaults to black.
/// </param>
public sealed record TextDrawOptions(
    string FontName = "Helvetica",
    float FontSize = 12f,
    (float R, float G, float B) Color = default
)
{
    /// <summary>Default styling: black 12&#8239;pt Helvetica.</summary>
    public static readonly TextDrawOptions Default = new();
}
