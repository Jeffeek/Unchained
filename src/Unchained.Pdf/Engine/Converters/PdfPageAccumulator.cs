using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Engine.Converters;

/// <summary>
/// Builds a multi-page PDF document from scratch using <see cref="ObjectGraphBuilder"/>.
/// Callers add pages with pre-rendered content stream bytes and per-page font maps,
/// then call <see cref="Build"/> to get an <see cref="IPdfDocument"/>.
/// </summary>
internal sealed class PdfPageAccumulator
{
    private readonly ObjectGraphBuilder _builder = new();
    private readonly int _pagesNum;
    private readonly PdfIndirectReference _pagesRef;
    private readonly List<PdfIndirectReference> _pageRefs = [];

    internal PdfPageAccumulator()
    {
        _pagesNum = _builder.NextNumber();
        _pagesRef = new PdfIndirectReference(_pagesNum, 0);
    }

    /// <summary>
    /// Adds a named font (Standard 14) to the builder and returns its indirect reference.
    /// Multiple calls with the same font name each create a separate object — callers are
    /// responsible for sharing references across pages when appropriate.
    /// </summary>
    internal PdfIndirectReference AddFont(string baseFontName, string? encoding = "WinAnsiEncoding")
    {
        var entries = new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("Font"),
            ["Subtype"] = PdfName.Get("Type1"),
            ["BaseFont"] = PdfName.Get(baseFontName)
        };
        if (encoding is not null)
            entries["Encoding"] = PdfName.Get(encoding);

        return _builder.Add(new PdfDictionary(entries)).ToReference();
    }

    /// <summary>
    /// Adds a page with the given dimensions, content stream bytes, and font resource map.
    /// </summary>
    /// <param name="widthPt">Page width in points.</param>
    /// <param name="heightPt">Page height in points.</param>
    /// <param name="contentBytes">Decoded (uncompressed) content stream bytes.</param>
    /// <param name="fontMap">
    /// Mapping from resource key (e.g. <c>"F1"</c>) to a font <see cref="PdfIndirectReference"/>
    /// previously added via <see cref="AddFont"/>.
    /// </param>
    internal void AddPage(
        float widthPt,
        float heightPt,
        ReadOnlySpan<byte> contentBytes,
        IReadOnlyDictionary<string, PdfIndirectReference> fontMap
    )
    {
        var contentDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Length"] = new PdfInteger(contentBytes.Length)
        });
        var contentRef = _builder.Add(new PdfStream(contentDict, contentBytes.ToArray())).ToReference();

        var fontEntries = fontMap.ToDictionary(
            static kv => kv.Key,
            static PdfObject (kv) => kv.Value);

        var pageDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("Page"),
            ["Parent"] = _pagesRef,
            ["MediaBox"] = new PdfArray([
                new PdfInteger(0), new PdfInteger(0),
                new PdfReal(widthPt), new PdfReal(heightPt)
            ]),
            ["Contents"] = contentRef,
            ["Resources"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Font"] = new PdfDictionary(fontEntries)
            })
        });

        _pageRefs.Add(_builder.Add(pageDict).ToReference());
    }

    /// <summary>Finalizes and returns the complete PDF document.</summary>
    internal IPdfDocument Build()
    {
        var pagesDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("Pages"),
            ["Kids"] = new PdfArray(_pageRefs.Cast<PdfObject>().ToArray()),
            ["Count"] = new PdfInteger(_pageRefs.Count)
        });
        _builder.AddAt(_pagesNum, pagesDict);

        var catalogDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("Catalog"),
            ["Pages"] = _pagesRef
        });
        var catalogRef = _builder.Add(catalogDict).ToReference();

        return ObjectGraphBuilder.Finalize(_builder, catalogRef);
    }
}
