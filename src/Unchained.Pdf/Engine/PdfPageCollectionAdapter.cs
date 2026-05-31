using System.Collections;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Adapts <see cref="PdfDocumentCore"/> page access to the <see cref="IPageCollection"/> interface.
/// Pages are resolved lazily from the document core on each access; no pages are
/// preloaded at construction time.
/// </summary>
internal sealed class PdfPageCollectionAdapter(PdfDocumentCore core) : IPageCollection
{
    /// <inheritdoc cref="IPageCollection.this[int]" />
    public IPdfPage this[int pageNumber] =>
        new PdfPageAdapter(core.GetPage(pageNumber), pageNumber);

    /// <inheritdoc />
    public int Count => core.PageCount;

    /// <inheritdoc />
    public IEnumerator<IPdfPage> GetEnumerator()
    {
        for (var i = 1; i <= Count; i++)
            yield return new PdfPageAdapter(core.GetPage(i), i);
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Adapts a raw PDF page dictionary to the <see cref="IPdfPage"/> interface.
/// Dimensions are read from the page's <c>/MediaBox</c> array (ISO 32000-1 §14.11.2).
/// </summary>
internal sealed class PdfPageAdapter(PdfDictionary page, int pageNumber) : IPdfPage
{
    /// <inheritdoc />
    public int PageNumber { get; } = pageNumber;

    /// <inheritdoc />
    public double Width => GetMediaBoxValue(2);

    /// <inheritdoc />
    public double Height => GetMediaBoxValue(3);

    // MediaBox is [llx lly urx ury]; width = urx (index 2), height = ury (index 3).
    private double GetMediaBoxValue(int index)
    {
        if (page[PdfName.MediaBox] is not PdfArray mediaBox || mediaBox.Count < 4)
            return 0;

        return mediaBox[index] switch
        {
            PdfInteger i => i.Value,
            PdfReal r => r.Value,
            _ => 0
        };
    }
}
