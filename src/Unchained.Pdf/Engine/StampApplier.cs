using System.Buffers;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Default <see cref="IStampApplier"/> implementation.
/// Each stamp is a new <c>/Contents</c> stream prepended or appended to existing page content.
/// </summary>
public sealed class StampApplier : IStampApplier
{
    private const string StampFontKey = "F_stamp";

    /// <inheritdoc />
    public Task StampAsync(IPdfDocument document, TextStamp stamp, CancellationToken ct = default) =>
        Task.Run(() => ApplyStamp(document, pageFilter: null, stamp), ct);

    /// <inheritdoc />
    public Task StampPageAsync(
        IPdfDocument document,
        int pageNumber,
        TextStamp stamp,
        CancellationToken ct = default
    ) => Task.Run(() => ApplyStamp(document, pageFilter: pageNumber, stamp), ct);

    // ── Core logic ────────────────────────────────────────────────────────────

    private static void ApplyStamp(IPdfDocument document, int? pageFilter, TextStamp stamp)
    {
        var adapter = document as PdfDocumentAdapter
                      ?? throw new ArgumentException(
                          $"Document was not created by Unchained. Expected {nameof(PdfDocumentAdapter)}, got {document.GetType().Name}.",
                          nameof(document));

        var existing = adapter.Core.CollectObjects();
        var maxObjNum = existing.Count > 0 ? existing.Max(static o => o.ObjectNumber) : 0;
        var builder = new ObjectGraphBuilder(startAt: maxObjNum + 1);

        // Build one shared font object for all pages.
        var fontObj = builder.Add(MakeFontDict(stamp.FontName));
        var contentBytes = BuildStampBytes(stamp);

        // Find pages (by reference equality with GetPage results when pageFilter set,
        // or all /Type /Page objects when stamping all pages).
        var targetPageDict = pageFilter.HasValue
            ? adapter.Core.GetPage(pageFilter.Value)
            : null;

        var swaps = new Dictionary<int, PdfIndirectObject>();

        foreach (var obj in existing)
        {
            if (obj.Value is not PdfDictionary pd)
                continue;

            if (pd.GetName(PdfName.Type.Value) != "Page")
                continue;

            if (targetPageDict is not null && !ReferenceEquals(pd, targetPageDict))
                continue;

            var streamObj = builder.Add(new PdfStream(
                new PdfDictionary(new Dictionary<string, PdfObject>
                {
                    [PdfName.Length.Value] = new PdfInteger(contentBytes.Length)
                }),
                contentBytes));

            var rebuilt = RebuildPage(
                pd,
                streamObj.ToReference(),
                fontObj.ToReference(),
                stamp.IsBackground,
                adapter.Core
            );
            swaps[obj.ObjectNumber] = new PdfIndirectObject(obj.ObjectNumber, obj.Generation, rebuilt);
        }

        var finalObjects = existing
            .Select(o => swaps.GetValueOrDefault(o.ObjectNumber, o))
            .Concat(builder.Objects)
            .ToList();

        var totalMax = finalObjects.Max(static o => o.ObjectNumber);
        var rootRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(totalMax + 1),
            [PdfName.Root.Value] = rootRef
        });

        var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(finalObjects, trailer);
        adapter.ReplaceCore(newDoc.Core);
    }

    // ── Content stream ────────────────────────────────────────────────────────

    private static byte[] BuildStampBytes(TextStamp stamp)
    {
        var buf = new ArrayBufferWriter<byte>(initialCapacity: 256);
        var csw = new ContentStreamWriter(buf);

        var radians = stamp.RotationDegrees * Math.PI / 180.0;
        var cosR = (float)Math.Cos(radians);
        var sinR = (float)Math.Sin(radians);

        csw.Op("q"u8);
        csw.Float(stamp.GrayLevel);
        csw.Op("g"u8);
        csw.Op("BT"u8);
        csw.Name(StampFontKey);
        csw.Float(stamp.FontSize);
        csw.Op("Tf"u8);
        // Text matrix: [cos -sin sin cos x y] — but PDF matrix is [a b c d e f]
        // where a=cos, b=sin, c=-sin, d=cos, e=x, f=y
        csw.Float(cosR);
        csw.Float(sinR);
        csw.Float(-sinR);
        csw.Float(cosR);
        csw.Float(stamp.X);
        csw.Float(stamp.Y);
        csw.Op("Tm"u8);
        csw.LiteralString(stamp.Text);
        csw.Op("Tj"u8);
        csw.Op("ET"u8);
        csw.Op("Q"u8);

        return buf.WrittenMemory.ToArray();
    }

    // ── Page dict rebuilding ──────────────────────────────────────────────────

    private static PdfDictionary RebuildPage(
        PdfDictionary page,
        PdfIndirectReference stampRef,
        PdfObject fontRef,
        bool isBackground,
        PdfDocumentCore core
    )
    {
        // Merge /Contents
        var existing = page[PdfName.Contents];
        PdfObject newContents;
        if (existing is null)
            newContents = stampRef;
        else
        {
            var existingList = existing is PdfArray a
                ? a.Elements.ToList()
                : [existing];
            var allRefs = isBackground
                ? new[] { stampRef }.Concat(existingList).ToArray()
                : existingList.Append(stampRef).ToArray();
            newContents = new PdfArray(allRefs);
        }

        // Merge /Resources /Font
        var existingResources = ResolveDict(page[PdfName.Resources], core);
        var existingFonts = existingResources?.Get<PdfDictionary>(PdfName.Font);
        var fontEntries = existingFonts?.Entries.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value) ?? new Dictionary<string, PdfObject>();
        fontEntries[StampFontKey] = fontRef;

        var newFontDict = new PdfDictionary(fontEntries);
        var resourceEntries = existingResources?.Entries.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value) ?? new Dictionary<string, PdfObject>();
        resourceEntries[PdfName.Font.Value] = newFontDict;

        var newResources = new PdfDictionary(resourceEntries);
        var entries = new Dictionary<string, PdfObject>(page.Entries)
        {
            [PdfName.Contents.Value] = newContents,
            [PdfName.Resources.Value] = newResources
        };

        return new PdfDictionary(entries);
    }

    private static PdfDictionary? ResolveDict(PdfObject? obj, PdfDocumentCore core) => obj switch
    {
        PdfDictionary d => d,
        PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
        _ => null
    };

    private static PdfDictionary MakeFontDict(string baseFontName) =>
        new(new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.Font,
            [PdfName.Subtype.Value] = PdfName.Get("Type1"),
            [PdfName.BaseFont.Value] = PdfName.Get(baseFontName)
        });
}
