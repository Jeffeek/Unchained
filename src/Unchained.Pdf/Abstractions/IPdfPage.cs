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
    /// The visible width of the page in points — taken from <c>/CropBox</c> when present,
    /// otherwise from <c>/MediaBox</c> (horizontal dimension).
    /// </summary>
    double Width { get; }

    /// <summary>
    /// The visible height of the page in points — taken from <c>/CropBox</c> when present,
    /// otherwise from <c>/MediaBox</c> (vertical dimension).
    /// </summary>
    double Height { get; }

    /// <summary>
    /// The X coordinate (in PDF user-space points) of the lower-left corner of the visible
    /// area (<c>/CropBox llx</c>, or 0 when no CropBox is defined). Renderers must subtract
    /// this from all content coordinates to clip correctly to the visible region.
    /// </summary>
    double CropOriginX { get; }

    /// <summary>
    /// The Y coordinate (in PDF user-space points) of the lower-left corner of the visible
    /// area (<c>/CropBox lly</c>, or 0 when no CropBox is defined).
    /// </summary>
    double CropOriginY { get; }

    /// <summary>
    /// Page rotation in degrees clockwise as specified by the <c>/Rotate</c> entry
    /// (ISO 32000-1 §7.7.3.3). Always 0, 90, 180, or 270.
    /// </summary>
    int Rotate { get; }

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

    /// <summary>
    /// Extracts text from this page as positioned <see cref="TextSpan"/> instances,
    /// sorted in reading order (top-to-bottom, left-to-right).
    /// <para>
    /// Advance widths are computed using hardcoded AFM tables for the Standard 14 fonts.
    /// Fonts not in the Standard 14 (embedded TrueType/OpenType/CFF) receive a fallback
    /// width of 500/1000 em per character — positions will be approximate until the full
    /// font subsystem is implemented in Milestone 6.
    /// </para>
    /// <para>CTM transformations (rotation, shear) are not applied in this release;
    /// only axis-aligned text is positioned accurately.</para>
    /// </summary>
    IReadOnlyList<TextSpan> GetTextSpans();

    /// <summary>
    /// Extracts all text from this page as a plain string in reading order.
    /// Lines are separated by <c>\n</c>; spans on the same line are joined with a space
    /// when there is a visible gap between them.
    /// </summary>
    string ExtractText();

    /// <summary>
    /// Returns all annotations attached to this page, parsed from the <c>/Annots</c> array.
    /// Returns an empty list when the page has no annotations.
    /// </summary>
    IReadOnlyList<Annotation> GetAnnotations();

    /// <summary>
    /// Returns a map from PDF font resource name (e.g. <c>F1</c>) to base font name
    /// (e.g. <c>Helvetica</c>) as declared in the page's <c>/Resources /Font</c> dictionary.
    /// Used by renderers to resolve the actual typeface for each <c>Tf</c> operator.
    /// </summary>
    IReadOnlyDictionary<string, string> GetFontNameMap();

    /// <summary>
    /// Returns a map from PDF font resource name (e.g. <c>F1</c>) to the raw bytes of the
    /// embedded font program (<c>/FontFile</c>, <c>/FontFile2</c>, or <c>/FontFile3</c>),
    /// or <see langword="null"/> when the font is not embedded (Standard 14, system font).
    /// </summary>
    IReadOnlyDictionary<string, byte[]?> GetEmbeddedFontBytes();

    /// <summary>
    /// Decodes and returns all raster image XObjects referenced in this page's
    /// <c>/Resources /XObject</c> dictionary. Only <c>/DeviceRGB</c> images with 8 bits per
    /// component are decoded; other colour spaces produce a solid mid-grey placeholder.
    /// Returns an empty dictionary when the page has no image XObjects.
    /// </summary>
    IReadOnlyDictionary<string, ImageXObject> GetImageXObjects();
}
