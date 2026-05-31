namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Represents a single page within an <see cref="IPdfDocument"/>.
/// All dimensions are in PDF user space units (points), where 1 pt = 1/72 inch.
/// </summary>
public interface IPdfPage
{
    /// <summary>
    /// The 1-based page number of this page within its containing document.
    /// </summary>
    int PageNumber { get; }

    /// <summary>
    /// The width of the page's media box in points (horizontal dimension).
    /// </summary>
    double Width { get; }

    /// <summary>
    /// The height of the page's media box in points (vertical dimension).
    /// </summary>
    double Height { get; }

    /// <summary>
    /// <see langword="true"/> when <see cref="Width"/> is greater than <see cref="Height"/>.
    /// </summary>
    bool IsLandscape => Width > Height;
}
