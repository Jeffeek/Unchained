namespace Unchained.Pdf.Models;

/// <summary>
///     A single run of text extracted from a PDF page, with its position in PDF user space
///     (points, origin at bottom-left, y increasing upward).
/// </summary>
/// <param name="Text">The decoded text of this run.</param>
/// <param name="X">Horizontal position of the first glyph baseline origin, in points.</param>
/// <param name="Y">Vertical position of the first glyph baseline, in points.</param>
/// <param name="Width">Total advance width of this span, in points.</param>
/// <param name="FontSize">Font size as specified by the most recent <c>Tf</c> operator, in points.</param>
/// <param name="FontName">
///     Base font name as resolved from the page resource dictionary (e.g. <c>Helvetica</c>).
///     Empty string when the font cannot be resolved.
/// </param>
public sealed record TextSpan(
    string Text,
    double X,
    double Y,
    double Width,
    double FontSize,
    string FontName
);
