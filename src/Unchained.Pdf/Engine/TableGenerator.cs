using System.Buffers;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="ITableGenerator" /> implementation.
///     All layout is computed in a single pre-pass before any PDF operators are emitted.
///     When <see cref="TableData.Tagged" /> is <see langword="true" />, each header cell is
///     wrapped in a <c>/TH</c> marked-content sequence and each data cell in a <c>/TD</c>
///     sequence, and a full <c>/StructTreeRoot</c> is injected into the document catalog.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class TableGenerator : ITableGenerator
{
    private const float BorderLineAlpha = 0.85f; // alpha for table border lines
    private const float CellFillAlpha = 0.95f;   // alpha for table cell background fill

    /// <inheritdoc />
    public Task<IPdfDocument> GenerateAsync(
        TableData data,
        TableStyle style,
        CancellationToken ct = default
    ) => Task.Run(() => Generate(data, style), ct);

    /// <inheritdoc />
    public Task AppendTableAsync(
        IPdfDocument document,
        TableData data,
        TableStyle style,
        CancellationToken ct = default
    ) => Task.Run(() => Append(document, data, style), ct);

    // ── Core logic ────────────────────────────────────────────────────────────

    private static IPdfDocument Generate(TableData data, TableStyle style)
    {
        var layout = TableLayout.Compute(data.Headers.Count, style, data.Title is not null, data);
        var builder = new ObjectGraphBuilder();
        var resourcesRef = AddSharedResources(builder, style);

        // Reserve /Pages number so page dicts can reference it as /Parent.
        var pagesNum = builder.NextNumber();
        var pagesRef = new PdfIndirectReference(pagesNum, 0);

        var (pageRefs, allTaggedItems) = BuildPages(
            builder,
            data,
            layout,
            style,
            pagesRef,
            resourcesRef
        );

        var pagesObj = builder.AddAt(pagesNum, MakePagesDict(pageRefs));
        var catalogEntries = new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.Catalog,
            [PdfName.Pages.Value] = pagesObj.ToReference()
        };

        if (data.Tagged && allTaggedItems.Count > 0)
            InjectTagging(builder, catalogEntries, allTaggedItems, pageRefs, data.Language);

        var catalogObj = builder.Add(new PdfDictionary(catalogEntries));

        return ObjectGraphBuilder.Finalize(builder, catalogObj.ToReference());
    }

    private static void Append(IPdfDocument document, TableData data, TableStyle style)
    {
        var adapter = document as PdfDocumentAdapter
                      ?? throw new ArgumentException(
                          $"Document was not created by Unchained. Expected {nameof(PdfDocumentAdapter)}, got {document.GetType().Name}.",
                          nameof(document));

        var existing = adapter.Core.CollectObjects();
        var maxObjNum = existing.Count > 0 ? existing.Max(static o => o.ObjectNumber) : 0;

        var layout = TableLayout.Compute(data.Headers.Count, style, data.Title is not null, data);
        var builder = new ObjectGraphBuilder(maxObjNum + 1);
        var resourcesRef = AddSharedResources(builder, style);

        var pagesRef = adapter.Core.Catalog[PdfName.Pages] as PdfIndirectReference
                       ?? throw new PdfException("Document catalog is missing a /Pages indirect reference.");
        var pagesObjNum = pagesRef.ObjectNumber;

        var existingPagesObj = existing.First(o => o.ObjectNumber == pagesObjNum);
        var existingPagesDict = existingPagesObj.Value as PdfDictionary
                                ?? throw new PdfException("The /Pages object is not a dictionary.");

        // Track page index offset for MCID uniqueness when appending tagged tables.
        var existingPageCount = (int)(existingPagesDict.Get<PdfInteger>(PdfName.Count)?.Value ?? 0);

        var (newPageRefs, allTaggedItems) = BuildPages(
            builder,
            data,
            layout,
            style,
            pagesRef,
            resourcesRef,
            existingPageCount
        );

        var existingKids = (existingPagesDict.Get<PdfArray>(PdfName.Kids) ?? PdfArray.Empty).Elements;
        var allKids = existingKids.Concat(newPageRefs).ToArray();
        var rebuiltPagesObj = new PdfIndirectObject(
            pagesObjNum,
            0,
            new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Pages,
                [PdfName.Kids.Value] = new PdfArray(allKids),
                [PdfName.Count.Value] = new PdfInteger(allKids.Length)
            }));

        var finalObjects = existing
            .Select(o => o.ObjectNumber == pagesObjNum ? rebuiltPagesObj : o)
            .Concat(builder.Objects)
            .ToList();

        // Inject tagged structure into catalog when data.Tagged is true.
        if (data.Tagged && allTaggedItems.Count > 0)
        {
            var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
            var catalogObjNum = catalogRef.ObjectNumber;
            var catalogIdx = finalObjects.FindIndex(o => o.ObjectNumber == catalogObjNum);
            if (catalogIdx >= 0 && finalObjects[catalogIdx].Value is PdfDictionary catalogDict)
            {
                var catalogEntries = new Dictionary<string, PdfObject>(catalogDict.Entries);

                // Combine existing page refs with new ones for the parent tree.
                var allPageRefs = Enumerable.Range(1, existingPageCount)
                    .Select(PdfIndirectReference? (i) => FindPageRef(adapter.Core, i))
                    .OfType<PdfIndirectReference>()
                    .Concat(newPageRefs)
                    .ToList();

                InjectTagging(builder, catalogEntries, allTaggedItems, allPageRefs, data.Language);
                finalObjects[catalogIdx] = new PdfIndirectObject(
                    catalogObjNum,
                    0,
                    new PdfDictionary(catalogEntries)
                );

                // Add new structure tree objects to finalObjects.
                finalObjects.AddRange(builder.Objects.Where(o =>
                    finalObjects.All(e => e.ObjectNumber != o.ObjectNumber)));
            }
        }

        var totalMax = finalObjects.Max(static o => o.ObjectNumber);
        var rootRef2 = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Document trailer is missing /Root.");
        var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(totalMax + 1),
            [PdfName.Root.Value] = rootRef2
        });

        var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(finalObjects, trailer);
        adapter.ReplaceCore(newDoc.Core);
    }

    // ── Page building ─────────────────────────────────────────────────────────

    private static (List<PdfIndirectReference> PageRefs, List<TaggedContentItem> TaggedItems) BuildPages(
        ObjectGraphBuilder builder,
        TableData data,
        TableLayout layout,
        TableStyle style,
        PdfObject pagesRef,
        PdfObject resourcesRef,
        int pageIndexOffset = 0
    )
    {
        var pageRefs = new List<PdfIndirectReference>();
        var allTaggedItems = new List<TaggedContentItem>();
        var rowSlices = SliceRows(data.Rows, layout.RowsPerPage);
        var isFirst = true;

        if (rowSlices.Count == 0)
            rowSlices.Add([]);

        var pageIndex = pageIndexOffset;
        foreach (var slice in rowSlices)
        {
            var taggedItems = new List<TaggedContentItem>();
            var contentObj = AddContentStream(
                builder,
                data.Headers,
                slice,
                layout,
                style,
                isFirst ? data.Title : null,
                data.Tagged,
                pageIndex,
                taggedItems
            );
            isFirst = false;
            allTaggedItems.AddRange(taggedItems);

            var pageObj = builder.Add(MakePageDict(pagesRef, contentObj.ToReference(), resourcesRef));
            pageRefs.Add(pageObj.ToReference());
            pageIndex++;
        }

        return (pageRefs, allTaggedItems);
    }

    private static PdfIndirectObject AddContentStream(
        ObjectGraphBuilder builder,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        TableLayout layout,
        TableStyle style,
        string? title,
        bool tagged,
        int pageIndex,
        ICollection<TaggedContentItem> taggedItems
    )
    {
        var contentBuffer = new ArrayBufferWriter<byte>(4096);
        var csw = new ContentStreamWriter(contentBuffer);
        EmitTablePage(
            csw,
            headers,
            rows,
            layout,
            style,
            title,
            tagged,
            pageIndex,
            taggedItems
        );
        var contentBytes = contentBuffer.WrittenMemory.ToArray();
        var streamDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Length.Value] = new PdfInteger(contentBytes.Length)
        });

        return builder.Add(new PdfStream(streamDict, contentBytes));
    }

    // ── Content stream emission ───────────────────────────────────────────────

    private static void EmitTablePage(
        ContentStreamWriter csw,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        TableLayout layout,
        TableStyle style,
        string? title,
        bool tagged,
        int pageIndex,
        ICollection<TaggedContentItem> taggedItems
    )
    {
        const float margin = TableLayout.Margin;
        var curY = TableLayout.PageHeight - margin;
        var mcid = 0;

        csw.Op("q"u8);

        if (title is not null)
        {
            csw.Op("BT"u8);
            csw.Name("F2");
            csw.Float(style.HeaderFontSize);
            csw.Op("Tf"u8);
            var titleBaseline = curY - layout.TitleHeight + style.CellPaddingPt;
            SetTextMatrix(csw, margin + style.CellPaddingPt, titleBaseline);
            csw.LiteralString(title);
            csw.Op("Tj"u8);
            csw.Op("ET"u8);
            curY -= layout.TitleHeight;
        }

        // Header background (light gray).
        csw.Float(BorderLineAlpha);
        csw.Op("g"u8);
        csw.Float(margin);
        csw.Float(curY - layout.HeaderRowHeight);
        csw.Float(layout.TableWidth);
        csw.Float(layout.HeaderRowHeight);
        csw.Op("re"u8);
        csw.Op("f"u8);

        // Header row — /TH per cell when tagged.
        var headerBaseline = curY - layout.HeaderRowHeight + style.CellPaddingPt;
        for (var c = 0; c < headers.Count; c++)
        {
            var cellX = margin + style.CellPaddingPt + (c > 0 ? layout.ColumnWidths[..c].Sum() : 0f);

            if (tagged)
            {
                csw.MarkedContentBegin("TH", mcid);
                taggedItems.Add(new TaggedContentItem("TH", mcid, pageIndex));
                mcid++;
            }

            csw.Op("BT"u8);
            csw.Name("F2");
            csw.Float(style.HeaderFontSize);
            csw.Op("Tf"u8);
            SetTextMatrix(csw, cellX, headerBaseline);
            csw.LiteralString(headers[c]);
            csw.Op("Tj"u8);
            csw.Op("ET"u8);

            if (tagged)
                csw.MarkedContentEnd();
        }

        curY -= layout.HeaderRowHeight;
        var tableTopY = curY;

        // Data rows — /TD per cell when tagged.
        for (var rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            var row = rows[rowIdx];
            var rowBottomY = curY - layout.RowHeight;

            if (style.AlternatingRowColor && rowIdx % 2 == 0)
            {
                csw.Float(CellFillAlpha);
                csw.Op("g"u8);
                csw.Float(margin);
                csw.Float(rowBottomY);
                csw.Float(layout.TableWidth);
                csw.Float(layout.RowHeight);
                csw.Op("re"u8);
                csw.Op("f"u8);
            }

            var rowBaseline = rowBottomY + style.CellPaddingPt;
            for (var c = 0; c < headers.Count; c++)
            {
                var xOffset = c > 0 ? layout.ColumnWidths[..c].Sum() : 0f;
                var cellX = margin + style.CellPaddingPt + xOffset;
                var cellText = c < row.Count ? row[c] : string.Empty;

                if (tagged)
                {
                    csw.MarkedContentBegin("TD", mcid);
                    taggedItems.Add(new TaggedContentItem("TD", mcid, pageIndex));
                    mcid++;
                }

                csw.Op("BT"u8);
                csw.Name("F1");
                csw.Float(style.CellFontSize);
                csw.Op("Tf"u8);
                SetTextMatrix(csw, cellX, rowBaseline);
                csw.LiteralString(cellText);
                csw.Op("Tj"u8);
                csw.Op("ET"u8);

                if (tagged)
                    csw.MarkedContentEnd();
            }

            curY -= layout.RowHeight;
        }

        // Borders.
        if (style.DrawBorders)
        {
            csw.Float(0f);
            csw.Op("G"u8);
            csw.Float(0.5f);
            csw.Op("w"u8);

            var tableHeight = tableTopY - curY + layout.HeaderRowHeight;

            csw.Float(margin);
            csw.Float(curY);
            csw.Float(layout.TableWidth);
            csw.Float(tableHeight);
            csw.Op("re"u8);

            csw.Float(margin);
            csw.Float(tableTopY);
            csw.Op("m"u8);
            csw.Float(margin + layout.TableWidth);
            csw.Float(tableTopY);
            csw.Op("l"u8);

            var rowCurY = tableTopY;
            for (var r = 0; r < rows.Count - 1; r++)
            {
                rowCurY -= layout.RowHeight;
                csw.Float(margin);
                csw.Float(rowCurY);
                csw.Op("m"u8);
                csw.Float(margin + layout.TableWidth);
                csw.Float(rowCurY);
                csw.Op("l"u8);
            }

            var xOffset = 0f;
            for (var c = 1; c < headers.Count; c++)
            {
                xOffset += layout.ColumnWidths[c - 1];
                var vX = margin + xOffset;
                csw.Float(vX);
                csw.Float(curY);
                csw.Op("m"u8);
                csw.Float(vX);
                csw.Float(tableTopY + layout.HeaderRowHeight);
                csw.Op("l"u8);
            }

            csw.Op("S"u8);
        }

        csw.Op("Q"u8);
    }

    private static void SetTextMatrix(ContentStreamWriter csw, float x, float y)
    {
        csw.Float(1);
        csw.Float(0);
        csw.Float(0);
        csw.Float(1);
        csw.Float(x);
        csw.Float(y);
        csw.Op("Tm"u8);
    }

    // ── Tagging ───────────────────────────────────────────────────────────────

    private static void InjectTagging(
        ObjectGraphBuilder builder,
        IDictionary<string, PdfObject> catalogEntries,
        IReadOnlyList<TaggedContentItem> taggedItems,
        IReadOnlyList<PdfIndirectReference> pageRefs,
        string? language
    )
    {
        catalogEntries[PdfName.MarkInfo.Value] = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Marked.Value] = PdfBoolean.True
            });

        if (language is not null)
            catalogEntries[PdfName.Lang.Value] = PdfString.FromLatin1(language);

        catalogEntries[PdfName.ViewerPreferences.Value] = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["DisplayDocTitle"] = PdfBoolean.True
            });

        var structTreeRef = StructureTreeBuilder.Build(taggedItems, pageRefs, builder);
        catalogEntries[PdfName.StructTreeRoot.Value] = structTreeRef;
    }

    // ── Object construction helpers ───────────────────────────────────────────

    private static PdfIndirectReference AddSharedResources(ObjectGraphBuilder builder, TableStyle style)
    {
        var fontNormalObj = builder.Add(MakeFontDict(style.FontName));
        var fontBoldObj = builder.Add(MakeFontDict(style.FontName + "-Bold"));
        return builder.Add(new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Font.Value] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["F1"] = fontNormalObj.ToReference(),
                ["F2"] = fontBoldObj.ToReference()
            })
        })).ToReference();
    }

    private static PdfDictionary MakeFontDict(string baseFontName) =>
        new(new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.Font,
            [PdfName.Subtype.Value] = PdfName.Type1,
            [PdfName.BaseFont.Value] = PdfName.Get(baseFontName)
        });

    private static PdfDictionary MakePageDict(
        PdfObject pagesRef,
        PdfObject contentsRef,
        PdfObject resourcesRef
    ) => new(new Dictionary<string, PdfObject>
    {
        [PdfName.Type.Value] = PdfName.Page,
        [PdfName.Parent.Value] = pagesRef,
        [PdfName.MediaBox.Value] = new PdfArray([
            new PdfInteger(0), new PdfInteger(0),
            new PdfInteger((int)TableLayout.PageWidth),
            new PdfInteger((int)TableLayout.PageHeight)
        ]),
        [PdfName.Resources.Value] = resourcesRef,
        [PdfName.Contents.Value] = contentsRef
    });

    private static PdfDictionary MakePagesDict(IReadOnlyCollection<PdfIndirectReference> kids) =>
        new(new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.Pages,
            [PdfName.Kids.Value] = new PdfArray(kids.Cast<PdfObject>().ToArray()),
            [PdfName.Count.Value] = new PdfInteger(kids.Count)
        });

    private static List<List<IReadOnlyList<string>>> SliceRows(
        IReadOnlyCollection<IReadOnlyList<string>> rows,
        int pageSize
    )
    {
        var slices = new List<List<IReadOnlyList<string>>>();
        for (var i = 0; i < rows.Count; i += pageSize)
            slices.Add(rows.Skip(i).Take(pageSize).ToList());
        return slices;
    }

    /// <summary>Finds the indirect reference for the page at <paramref name="pageNumber" /> (1-based).</summary>
    private static PdfIndirectReference FindPageRef(PdfDocumentCore core, int pageNumber)
    {
        var pageDict = core.GetPage(pageNumber);
        var xref = core.CollectObjects();

        foreach (var obj in xref.Where(obj => ReferenceEquals(obj.Value, pageDict)))
            return new PdfIndirectReference(obj.ObjectNumber, obj.Generation);

        throw new PdfException($"Could not find indirect reference for page {pageNumber}.");
    }
}
