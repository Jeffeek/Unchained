namespace Unchained.Pdf.Models;

/// <summary>
/// A rectangular area on a page to redact, in PDF user-space points (origin at the
/// lower-left of the page, Y increasing upward — the same coordinate space as
/// <c>/MediaBox</c>). Content whose drawing position falls inside the rectangle is removed
/// from the page's content stream and the area is painted over with <see cref="FillColor"/>.
/// </summary>
/// <param name="PageNumber">1-based page the region applies to.</param>
/// <param name="X">Lower-left X in user-space points.</param>
/// <param name="Y">Lower-left Y in user-space points.</param>
/// <param name="Width">Width in points.</param>
/// <param name="Height">Height in points.</param>
/// <param name="FillColor">
/// RGB fill (each 0–1) painted over the redacted area. Defaults to black (0,0,0).
/// </param>
public sealed record RedactionRegion(
    int PageNumber,
    double X,
    double Y,
    double Width,
    double Height,
    (double R, double G, double B) FillColor = default
)
{
    /// <summary>The inclusive upper-right X coordinate.</summary>
    public double Right => X + Width;

    /// <summary>The inclusive upper-right Y coordinate.</summary>
    public double Top => Y + Height;

    /// <summary>True when the point (px, py) in user space lies within this region.</summary>
    public bool Contains(double px, double py) =>
        px >= X && px <= Right && py >= Y && py <= Top;
}
