using System.Buffers;
using System.Text;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Writing;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Adapts <see cref="PdfDocumentCore"/> to the public <see cref="IPdfDocument"/> interface.
/// </summary>
internal sealed class PdfDocumentAdapter : IPdfDocument
{
    private int _disposed;

    internal PdfDocumentAdapter(PdfDocumentCore core)
    {
        Core = core;
        Pages = new PdfPageCollectionAdapter(core);
    }

    internal PdfDocumentCore Core { get; private set; }

    /// <summary>
    /// Disposes the current core and replaces it with <paramref name="newCore"/>.
    /// Called by <see cref="TableGenerator"/> and <see cref="DocumentMerger"/> after
    /// in-place document mutation (e.g. <c>AppendTableAsync</c>, <c>MergeAsync</c>).
    /// </summary>
    internal void ReplaceCore(PdfDocumentCore newCore)
    {
        Core.Dispose();
        Core = newCore;
        Pages = new PdfPageCollectionAdapter(Core);
    }

    /// <inheritdoc />
    public int PageCount => Core.PageCount;

    /// <inheritdoc />
    public IPageCollection Pages { get; private set; }

    /// <inheritdoc />
    public DocumentMetadata Metadata
    {
        get
        {
            var info = Core.Info;
            return info is null
                ? DocumentMetadata.Empty
                : new DocumentMetadata(
                    Title: GetInfoString(info, "Title"),
                    Author: GetInfoString(info, "Author"),
                    Subject: GetInfoString(info, "Subject"),
                    Keywords: GetInfoString(info, "Keywords"),
                    Creator: GetInfoString(info, "Creator"),
                    Producer: GetInfoString(info, "Producer"),
                    CreationDate: null,
                    ModificationDate: null);
        }
    }

    /// <inheritdoc />
    public bool IsDisposed => _disposed == 1;

    /// <summary>
    /// Performs a full-rewrite serialization: collects every in-use indirect object
    /// from the document, rebuilds the xref table with fresh byte offsets, and
    /// writes a clean PDF via <see cref="PdfWriter"/>.
    /// </summary>
    internal byte[] Serialize(SaveOptions? options)
    {
        _ = options; // Currently ignored — reserved for future use (e.g. incremental update support)

        var objects = Core.CollectObjects();
        var maxObjNum = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;

        // Build a clean trailer — preserve /Root and /Info, drop /Prev and other
        // incremental-update entries that belong to the old file structure.
        var trailerEntries = new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(maxObjNum + 1),
            [PdfName.Root.Value] = Core.Trailer[PdfName.Root] ?? throw new PdfException("Document trailer is missing required /Root entry.")
        };

        if (Core.Trailer[PdfName.Info] is { } infoRef)
            trailerEntries[PdfName.Info.Value] = infoRef;

        var trailer = new PdfDictionary(trailerEntries);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new PdfWriter(buffer);
        writer.Write(objects, trailer);
        return buffer.WrittenMemory.ToArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            Core.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public IReadOnlyList<Bookmark> GetBookmarks()
    {
        var outlines = ResolveDict(Core.Catalog[PdfName.Outlines]);
        return outlines is null ? [] : ReadOutlineLevel(outlines);
    }

    private IReadOnlyList<Bookmark> ReadOutlineLevel(PdfDictionary node)
    {
        var result = new List<Bookmark>();
        var current = ResolveDict(node[PdfName.First]);
        while (current is not null)
        {
            var title = current[PdfName.Title] is PdfString ts
                ? Encoding.Latin1.GetString(ts.Bytes.Span)
                : string.Empty;

            var pageNum = ResolveDestPage(current);
            var children = ResolveDict(current[PdfName.First]) is not null
                ? ReadOutlineLevel(current)
                : null;

            result.Add(new Bookmark(title, pageNum, children));
            current = ResolveDict(current[PdfName.Next]);
        }

        return result;
    }

    private int ResolveDestPage(PdfDictionary item)
    {
        var dest = item[PdfName.Dest];
        if (dest is PdfIndirectReference dr)
            dest = Core.ResolveIndirect(dr.ObjectNumber).Value;

        if (dest is not PdfArray destArr || destArr.Count == 0)
            return 0;

        if (destArr[0] is not PdfIndirectReference pageRef)
            return 0;

        for (var i = 1; i <= Core.PageCount; i++)
        {
            var pageDict = Core.GetPage(i);
            if (Core.Dereference(pageRef) is PdfDictionary resolved &&
                ReferenceEquals(pageDict, resolved))
                return i;
        }

        return 0;
    }

    /// <inheritdoc />
    public IReadOnlyList<FormField> GetFormFields()
    {
        var acroForm = ResolveDict(Core.Catalog[PdfName.AcroForm]);
        if (acroForm is null)
            return [];

        var fields = acroForm.Get<PdfArray>(PdfName.Fields);
        if (fields is null)
            return [];

        var result = new List<FormField>();
        CollectFields(fields, prefix: string.Empty, result);

        return result;
    }

    private void CollectFields(PdfArray fields, string prefix, ICollection<FormField> result)
    {
        foreach (var dict in fields.Elements.Select(ResolveDict).OfType<PdfDictionary>())
        {
            var partialName = dict[PdfName.Get("T")] is PdfString ts
                ? Encoding.Latin1.GetString(ts.Bytes.Span)
                : string.Empty;

            var fullName = prefix.Length > 0 ? $"{prefix}.{partialName}" : partialName;
            var ft = dict.GetName("FT");

            // Non-terminal node (group field with /Kids but no /FT)
            if (ft is null && dict.Get<PdfArray>(PdfName.Kids) is { } kids)
            {
                CollectFields(kids, fullName, result);
                continue;
            }

            var value = DecodeFieldValue(dict);
            result.Add(new FormField(fullName, ft ?? string.Empty, value));
        }
    }

    private static string? DecodeFieldValue(PdfDictionary dict)
    {
        var v = dict[PdfName.Get("V")];
        return v switch
        {
            PdfString s => Encoding.Latin1.GetString(s.Bytes.Span),
            PdfName n => n.Value,
            _ => null
        };
    }

    private PdfDictionary? ResolveDict(PdfObject? obj) => obj switch
    {
        PdfDictionary d => d,
        PdfIndirectReference r => Core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
        _ => null
    };

    private static string? GetInfoString(PdfDictionary info, string key) =>
        info[key] is not PdfString str
            ? null
            : Encoding.Latin1.GetString(str.Bytes.Span);
}
