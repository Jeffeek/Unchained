using Unchained.Pdf.Models;

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

    /// <summary>
    /// Parses and returns all content stream operators for this page in stream order
    /// (ISO 32000-1 §7.8.2). Each <see cref="ContentOperator"/> contains the operator
    /// keyword and its preceding operand values.
    /// <para>
    /// Returns an empty list when the page has no <c>/Contents</c> entry.
    /// Multiple content streams (when <c>/Contents</c> is an array) are concatenated
    /// before parsing (§7.8.1).
    /// </para>
    /// </summary>
    IReadOnlyList<ContentOperator> GetContentOperators();
}
