using System.Buffers;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Default <see cref="ITableGenerator"/> implementation.
/// All layout is computed in a single pre-pass before any PDF operators are emitted.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class TableGenerator : ITableGenerator
{
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
        var layout = TableLayout.Compute(data.Headers.Count, style, hasTitle: data.Title is not null);
        var builder = new ObjectGraphBuilder();
        var resourcesRef = AddSharedResources(builder, style);

        // Reserve /Pages number so page dicts can reference it as /Parent.
        var pagesNum = builder.NextNumber();
        var pagesRef = new PdfIndirectReference(pagesNum, 0);

        var pageRefs = BuildPages(
            builder,
            data,
            layout,
            style,
            pagesRef,
            resourcesRef
        );

        var pagesObj = builder.AddAt(pagesNum, MakePagesDict(pageRefs));
        var catalogObj = builder.Add(MakeCatalogDict(pagesObj.ToReference()));
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

        var layout = TableLayout.Compute(data.Headers.Count, style, hasTitle: data.Title is not null);
        var builder = new ObjectGraphBuilder(startAt: maxObjNum + 1);
        var resourcesRef = AddSharedResources(builder, style);

        // Locate the /Pages root (to update /Kids and /Count).
        var pagesRef = adapter.Core.Catalog[PdfName.Pages] as PdfIndirectReference
                       ?? throw new PdfException("Document catalog is missing a /Pages indirect reference.");
        var pagesObjNum = pagesRef.ObjectNumber;

        var existingPagesObj = existing.First(o => o.ObjectNumber == pagesObjNum);
        var existingPagesDict = existingPagesObj.Value as PdfDictionary
                                ?? throw new PdfException("The /Pages object is not a dictionary.");

        var newPageRefs = BuildPages(
            builder,
            data,
            layout,
            style,
            pagesRef,
            resourcesRef
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

        var totalMax = finalObjects.Max(static o => o.ObjectNumber);
        var rootRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Document trailer is missing /Root.");
        var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(totalMax + 1),
            [PdfName.Root.Value] = rootRef
        });

        var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(finalObjects, trailer);
        adapter.ReplaceCore(newDoc.Core);
    }

    // ── Page building ─────────────────────────────────────────────────────────

    private static List<PdfIndirectReference> BuildPages(
        ObjectGraphBuilder builder,
        TableData data,
        TableLayout layout,
        TableStyle style,
        PdfObject pagesRef,
        PdfObject resourcesRef
    )
    {
        var pageRefs = new List<PdfIndirectReference>();
        var rowSlices = SliceRows(data.Rows, layout.RowsPerPage);
        var isFirst = true;

        if (rowSlices.Count == 0)
            rowSlices.Add([]);

        foreach (var contentObj in rowSlices.Select(slice => AddContentStream(
                     builder,
                     data.Headers,
                     slice,
                     layout,
                     style,
                     isFirst
                         ? data.Title
                         : null)))
        {
            isFirst = false;
            var pageObj = builder.Add(MakePageDict(pagesRef, contentObj.ToReference(), resourcesRef));
            pageRefs.Add(pageObj.ToReference());
        }

        return pageRefs;
    }

    private static PdfIndirectObject AddContentStream(
        ObjectGraphBuilder builder,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        TableLayout layout,
        TableStyle style,
        string? title
    )
    {
        var contentBuffer = new ArrayBufferWriter<byte>(initialCapacity: 4096);
        var csw = new ContentStreamWriter(contentBuffer);
        EmitTablePage(
            csw,
            headers,
            rows,
            layout,
            style,
            title
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
        string? title
    )
    {
        const float margin = TableLayout.Margin;
        var curY = TableLayout.PageHeight - margin;

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
        csw.Float(0.85f);
        csw.Op("g"u8);
        csw.Float(margin);
        csw.Float(curY - layout.HeaderRowHeight);
        csw.Float(layout.TableWidth);
        csw.Float(layout.HeaderRowHeight);
        csw.Op("re"u8);
        csw.Op("f"u8);

        // ReSharper disable once GrammarMistakeInComment
        // Header text — one BT..ET for the full row.
        csw.Op("BT"u8);
        csw.Name("F2");
        csw.Float(style.HeaderFontSize);
        csw.Op("Tf"u8);
        var headerBaseline = curY - layout.HeaderRowHeight + style.CellPaddingPt;
        for (var c = 0; c < headers.Count; c++)
        {
            var cellX = margin + style.CellPaddingPt + (c * layout.ColumnWidths[c]);
            SetTextMatrix(csw, cellX, headerBaseline);
            csw.LiteralString(headers[c]);
            csw.Op("Tj"u8);
        }

        csw.Op("ET"u8);
        curY -= layout.HeaderRowHeight;

        var tableTopY = curY;

        // Data rows.
        for (var rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            var row = rows[rowIdx];
            var rowBottomY = curY - layout.RowHeight;

            if (style.AlternatingRowColor && rowIdx % 2 == 0)
            {
                csw.Float(0.95f);
                csw.Op("g"u8);
                csw.Float(margin);
                csw.Float(rowBottomY);
                csw.Float(layout.TableWidth);
                csw.Float(layout.RowHeight);
                csw.Op("re"u8);
                csw.Op("f"u8);
            }

            csw.Op("BT"u8);
            csw.Name("F1");
            csw.Float(style.CellFontSize);
            csw.Op("Tf"u8);
            var rowBaseline = rowBottomY + style.CellPaddingPt;
            for (var c = 0; c < headers.Count; c++)
            {
                var xOffset = c > 0 ? layout.ColumnWidths[..c].Sum() : 0f;
                var cellX = margin + style.CellPaddingPt + xOffset;
                SetTextMatrix(csw, cellX, rowBaseline);
                csw.LiteralString(c < row.Count ? row[c] : "");
                csw.Op("Tj"u8);
            }

            csw.Op("ET"u8);

            curY -= layout.RowHeight;
        }

        // Borders — all path segments stroked in one call.
        if (style.DrawBorders)
        {
            csw.Float(0f);
            csw.Op("G"u8); // black stroke
            csw.Float(0.5f);
            csw.Op("w"u8); // 0.5 pt line width

            var tableHeight = tableTopY - curY + layout.HeaderRowHeight;

            // Outer rectangle.
            csw.Float(margin);
            csw.Float(curY);
            csw.Float(layout.TableWidth);
            csw.Float(tableHeight);
            csw.Op("re"u8);

            // Horizontal line below header.
            csw.Float(margin);
            csw.Float(tableTopY);
            csw.Op("m"u8);
            csw.Float(margin + layout.TableWidth);
            csw.Float(tableTopY);
            csw.Op("l"u8);

            // Horizontal line below each data row (except the last — covered by outer rect).
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

            // Vertical line between each column pair.
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
        // Identity matrix with translation (x, y): "1 0 0 1 x y Tm"
        csw.Float(1);
        csw.Float(0);
        csw.Float(0);
        csw.Float(1);
        csw.Float(x);
        csw.Float(y);
        csw.Op("Tm"u8);
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
            [PdfName.Subtype.Value] = PdfName.Get("Type1"),
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

    private static PdfDictionary MakeCatalogDict(PdfObject pagesRef) =>
        new(new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.Catalog,
            [PdfName.Pages.Value] = pagesRef
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
}
