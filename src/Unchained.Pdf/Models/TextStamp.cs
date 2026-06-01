namespace Unchained.Pdf.Models;

/// <summary>
/// A text overlay to be drawn on one or more PDF pages.
/// Coordinates are in PDF user space (points, origin bottom-left).
/// </summary>
public sealed record TextStamp(
    /// <summary>Text to render.</summary>
    string Text,
    /// <summary>Horizontal position of the text baseline origin, in points.</summary>
    float X,
    /// <summary>Vertical position of the text baseline, in points.</summary>
    float Y,
    /// <summary>Base font name (Standard 14 only for M4). Default: <c>Helvetica</c>.</summary>
    string FontName = "Helvetica",
    /// <summary>Font size in points. Default: 24.</summary>
    float FontSize = 24f,
    /// <summary>Non-stroking gray level: 0 = black, 1 = white. Default: 0 (black).</summary>
    float GrayLevel = 0f,
    /// <summary>Counter-clockwise rotation in degrees. Default: 0 (horizontal).</summary>
    float RotationDegrees = 0f,
    /// <summary>
    /// When <see langword="true"/>, the stamp is prepended to <c>/Contents</c> so it appears
    /// behind existing page content. When <see langword="false"/> (default) it is appended
    /// and appears in front.
    /// </summary>
    bool IsBackground = false
);
