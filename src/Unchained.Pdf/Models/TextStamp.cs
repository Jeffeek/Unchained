namespace Unchained.Pdf.Models;

/// <summary>
///     A text overlay to be drawn on one or more PDF pages.
///     Coordinates are in PDF user space (points, origin bottom-left).
/// </summary>
/// <param name="Text">Text to render.</param>
/// <param name="X">Horizontal position of the text baseline origin, in points.</param>
/// <param name="Y">Vertical position of the text baseline, in points.</param>
/// <param name="FontName">Base font name (Standard 14 only). Default: <c>Helvetica</c>.</param>
/// <param name="FontSize">Font size in points. Default: 24.</param>
/// <param name="GrayLevel">Non-stroking gray level: 0 = black, 1 = white. Default: 0 (black).</param>
/// <param name="RotationDegrees">Counter-clockwise rotation in degrees. Default: 0 (horizontal).</param>
/// <param name="IsBackground">
///     When <see langword="true" />, the stamp is prepended to <c>/Contents</c> so it appears
///     behind existing page content. When <see langword="false" /> (default) it is appended
///     and appears in front.
/// </param>
public sealed record TextStamp(
    string Text,
    float X,
    float Y,
    string FontName = "Helvetica",
    float FontSize = 24f,
    float GrayLevel = 0f,
    float RotationDegrees = 0f,
    bool IsBackground = false
);
