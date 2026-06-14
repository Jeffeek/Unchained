using System.Collections;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Adapts <see cref="PdfDocumentCore" /> page access to the <see cref="IPageCollection" /> interface.
///     Pages are resolved lazily from the document core on each access; no pages are
///     preloaded at construction time.
/// </summary>
internal sealed class PdfPageCollectionAdapter(PdfDocumentCore core) : IPageCollection
{
    /// <inheritdoc cref="IPageCollection.this[int]" />
    public IPdfPage this[int pageNumber] =>
        new PdfPageAdapter(core.GetPage(pageNumber), pageNumber, core);

    /// <inheritdoc />
    public int Count => core.PageCount;

    /// <inheritdoc />
    public IEnumerator<IPdfPage> GetEnumerator()
    {
        for (var i = 1; i <= Count; i++)
            yield return new PdfPageAdapter(core.GetPage(i), i, core);
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
///     Adapts a raw PDF page dictionary to the <see cref="IPdfPage" /> interface.
///     Dimensions are read from <c>/CropBox</c> when present, falling back to
///     <c>/MediaBox</c> (ISO 32000-1 §14.11.2). Content operators are parsed on
///     demand from the page's <c>/Contents</c> stream(s).
/// </summary>
/// <remarks>
///     This adapter is intentionally thin: each resource family (fonts, images, shadings,
///     tiling patterns, colour spaces, transparency, annotations, content streams) is parsed
///     by a dedicated stateless reader under <c>Engine/PageResources/</c>. The adapter owns only
///     the page dictionary, page number and document core, and the page-geometry logic
///     (<see cref="Width" />/<see cref="Height" />/<see cref="Rotate" /> and the inheritable
///     box lookups) that those three values are tightly coupled to.
/// </remarks>
internal sealed class PdfPageAdapter(PdfDictionary page, int pageNumber, PdfDocumentCore core) : IPdfPage
{
    // Exposes the document core for XObject resolution and font embedding in M5.
    internal PdfDocumentCore Core => core;

    /// <inheritdoc />
    public int PageNumber { get; } = pageNumber;

    /// <inheritdoc />
    public double CropOriginX => GetArrayBoxValue("CropBox", 0) ?? GetArrayBoxValue("MediaBox", 0) ?? 0;

    /// <inheritdoc />
    public double CropOriginY => GetArrayBoxValue("CropBox", 1) ?? GetArrayBoxValue("MediaBox", 1) ?? 0;

    /// <inheritdoc />
    // CropBox (if present) defines the visible area; fall back to MediaBox.
    // [llx lly urx ury]; width = |urx - llx|, height = |ury - lly|.
    // Rotation swaps the meaning: for Rotate 90/270 the logical width = ury-lly.
    public double Width
    {
        get
        {
            var rotate = Rotate;
            return rotate is 90 or 270
                ? Math.Abs(GetBoxValue(3) - GetBoxValue(1))
                : Math.Abs(GetBoxValue(2) - GetBoxValue(0));
        }
    }

    /// <inheritdoc />
    public double Height
    {
        get
        {
            var rotate = Rotate;
            return rotate is 90 or 270
                ? Math.Abs(GetBoxValue(2) - GetBoxValue(0))
                : Math.Abs(GetBoxValue(3) - GetBoxValue(1));
        }
    }

    /// <inheritdoc />
    public int Rotate
    {
        get
        {
            var val = GetInheritedInteger(PdfName.Rotate, 0);
            return ((val % 360) + 360) % 360; // normalise to 0/90/180/270
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ContentOperator> GetContentOperators() =>
        PageContentReader.GetContentOperators(page, core);

    /// <inheritdoc />
    public IReadOnlyList<TextSpan> GetTextSpans() =>
        TextExtractor.Extract(GetContentOperators(), PageFontResolver.ResolveFontNames(page, core));

    /// <inheritdoc />
    public string ExtractText() =>
        TextExtractor.SpansToText(GetTextSpans());

    /// <inheritdoc />
    public IReadOnlyList<Annotation> GetAnnotations() =>
        PageAnnotationReader.GetAnnotations(page, core);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetFontNameMap() =>
        PageFontResolver.ResolveFontNames(page, core);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, byte[]?> GetEmbeddedFontBytes() =>
        PageFontResolver.GetEmbeddedFontBytes(page, core);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, (double Fill, double Stroke, string BlendMode, string? SoftMaskName)> GetExtGStateAlphas() =>
        PageTransparencyResolver.GetExtGStateAlphas(page, core);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SoftMaskInfo> GetSoftMasks(int widthPx, int heightPx) =>
        PageTransparencyResolver.GetSoftMasks(page, core, widthPx, heightPx);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ShadingInfo> GetShadings() =>
        PageShadingResolver.GetShadings(page, core);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, TilingPatternInfo> GetTilingPatterns() =>
        PageShadingResolver.GetTilingPatterns(page, core);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, CompositeFontInfo> GetCompositeFonts() =>
        PageFontResolver.GetCompositeFonts(page, core);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> GetToUnicodeMaps() =>
        PageFontResolver.GetToUnicodeMaps(page, core);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ImageXObject> GetImageXObjects() =>
        PageImageExtractor.GetImageXObjects(page, core);

    /// <summary>
    ///     Resolves Type 3 fonts from the page font resources.
    /// </summary>
    /// <returns>
    ///     A read-only dictionary where the key is the font resource name and the
    ///     value is a <see cref="Type3FontInfo"/> describing the font matrix,
    ///     encoding, glyph widths, and glyph drawing programs.
    /// </returns>
    internal IReadOnlyDictionary<string, Type3FontInfo> GetType3Fonts() =>
        PageFontResolver.GetType3Fonts(page, core);

    /// <summary>
    ///     Resolves named color spaces from the page <c>/Resources/ColorSpace</c>
    ///     dictionary.
    /// </summary>
    /// <returns>
    ///     A read-only dictionary where the key is the color space resource name and
    ///     the value is a resolved <see cref="ColorSpaceInfo"/> describing how color
    ///     values should be interpreted and converted to RGB.
    /// </returns>
    internal IReadOnlyDictionary<string, ColorSpaceInfo> GetColorSpaces() =>
        PageColorSpaceResolver.GetColorSpaces(page, core);

    // ── Page geometry ─────────────────────────────────────────────────────────

    // Walk up the page tree to find an inherited integer value.
    private int GetInheritedInteger(PdfName key, int defaultValue)
    {
        PdfObject? current = page;
        while (current is not null)
        {
            var dict = core.ResolveDict(current);
            if (dict is null) break;

            var obj = dict[key];

            switch (obj)
            {
                case PdfInteger n:
                    return (int)n.Value;
                case PdfReal r2:
                    return (int)r2.Value;
                default:
                    // Climb to parent
                    current = dict[PdfName.Parent];
                break;
            }
        }

        return defaultValue;
    }

    // Returns the raw value at the given index from /CropBox if present,
    // otherwise from /MediaBox. Both follow [llx lly urx ury] order.
    private double GetBoxValue(int index) =>
        GetArrayBoxValue("CropBox", index) ??
        GetArrayBoxValue("MediaBox", index) ??
        0;

    private double? GetArrayBoxValue(string name, int index)
    {
        // /MediaBox and /CropBox are inheritable page attributes (§7.7.3.4): a page leaf
        // may omit them and inherit from an ancestor /Pages node. Walk up /Parent until
        // the entry is found. Also resolve indirect references on both the array and its
        // elements.
        var key = PdfName.Get(name);
        PdfObject? current = page;
        var depth = 0;
        while (current is not null && depth++ < 64)
        {
            var dict = core.ResolveDict(current);
            if (dict is null) break;

            var boxObj = dict[key];
            if (boxObj is PdfIndirectReference br)
                boxObj = core.ResolveIndirect(br.ObjectNumber).Value;

            if (boxObj is PdfArray { Count: >= 4 } box)
            {
                var elem = box[index];
                if (elem is PdfIndirectReference er)
                    elem = core.ResolveIndirect(er.ObjectNumber).Value;
                return elem.ReadIntOrRealNullable();
            }

            current = dict[PdfName.Parent];
        }

        return null;
    }
}
