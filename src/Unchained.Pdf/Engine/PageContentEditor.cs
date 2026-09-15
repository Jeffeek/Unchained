using System.Buffers;
using System.IO.Compression;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IPageContentEditor" /> implementation. Text and images are appended as
///     new <c>/Contents</c> streams with matching <c>/Resources</c> entries; blank pages are
///     inserted through the shared page-tree rebuild used by <see cref="PageOrganizer" />.
/// </summary>
public sealed class PageContentEditor : IPageContentEditor
{
    /// <inheritdoc />
    public async Task<int> AddBlankPageAsync(
        IPdfDocument document,
        double width = 612,
        double height = 792,
        int? atPageNumber = null,
        CancellationToken ct = default
    )
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var destCount = adapter.Core.PageCount;
        var insertAt = atPageNumber ?? (destCount + 1);
        if (insertAt < 1 || insertAt > destCount + 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atPageNumber),
                insertAt,
                $"Insert position must be between 1 and {destCount + 1}."
            );
        }

#pragma warning disable CA2007
        await using var blank = CreateBlankDocument(width, height);
#pragma warning enable CA2007
        await new PageOrganizer().InsertPagesAsync(document, insertAt, blank, ct).ConfigureAwait(false);
        return insertAt;
    }

    /// <inheritdoc />
    public Task DrawTextAsync(
        IPdfDocument document,
        int pageNumber,
        string text,
        double x,
        double y,
        TextDrawOptions? options = null,
        CancellationToken ct = default
    ) => Task.Run(() => DrawText(document, pageNumber, text, x, y, options ?? TextDrawOptions.Default), ct);

    /// <inheritdoc />
    public Task DrawImageAsync(
        IPdfDocument document,
        int pageNumber,
        ImageContent image,
        double x,
        double y,
        double width,
        double height,
        CancellationToken ct = default
    ) => Task.Run(() => DrawImage(document, pageNumber, image, x, y, width, height), ct);

    // ── Draw text ─────────────────────────────────────────────────────────────

    private static void DrawText(
        IPdfDocument document,
        int pageNumber,
        string text,
        double x,
        double y,
        TextDrawOptions options
    )
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var (existing, builder) = MutationHelper.CollectWithBuilder(adapter);
        var targetDict = adapter.Core.GetPage(pageNumber);

        var fontKey = UniqueResourceKey(targetDict, PdfName.Font.Value, "F", adapter.Core);
        var fontObj = builder.Add(MakeFontDict(options.FontName));
        var content = BuildTextBytes(text, x, y, fontKey, options);

        CommitPageContent(
            adapter,
            existing,
            builder,
            targetDict,
            content,
            [(PdfName.Font.Value, fontKey, fontObj.ToReference())]
        );
    }

    private static byte[] BuildTextBytes(string text, double x, double y, string fontKey, TextDrawOptions options)
    {
        var buf = new ArrayBufferWriter<byte>(128);
        var csw = new ContentStreamWriter(buf);

        csw.Op("q"u8);
        csw.Float(options.Color.R);
        csw.Float(options.Color.G);
        csw.Float(options.Color.B);
        csw.Op("rg"u8);
        csw.Op("BT"u8);
        csw.Name(fontKey);
        csw.Float(options.FontSize);
        csw.Op("Tf"u8);
        csw.Float((float)x);
        csw.Float((float)y);
        csw.Op("Td"u8);
        csw.LiteralString(text);
        csw.Op("Tj"u8);
        csw.Op("ET"u8);
        csw.Op("Q"u8);

        return buf.WrittenMemory.ToArray();
    }

    // ── Draw image ────────────────────────────────────────────────────────────

    private static void DrawImage(
        IPdfDocument document,
        int pageNumber,
        ImageContent image,
        double x,
        double y,
        double width,
        double height
    )
    {
        if (image.RgbData.Length != image.Width * image.Height * 3)
        {
            throw new ArgumentException(
                $"RgbData length {image.RgbData.Length} does not match {image.Width}x{image.Height}x3 = {image.Width * image.Height * 3}.",
                nameof(image)
            );
        }

        if (image.Alpha is not null && image.Alpha.Length != image.Width * image.Height)
        {
            throw new ArgumentException(
                $"Alpha length {image.Alpha.Length} does not match {image.Width}x{image.Height} = {image.Width * image.Height}.",
                nameof(image)
            );
        }

        var adapter = MutationHelper.Cast(nameof(document), document);
        var (existing, builder) = MutationHelper.CollectWithBuilder(adapter);
        var targetDict = adapter.Core.GetPage(pageNumber);

        var imageKey = UniqueResourceKey(targetDict, PdfName.XObject.Value, "Im", adapter.Core);

        PdfIndirectReference? softMaskRef = null;
        if (image.Alpha is not null)
            softMaskRef = builder.Add(MakeImageStream(image.Width, image.Height, image.Alpha, isMask: true)).ToReference();

        var imageObj = builder.Add(MakeImageStream(image.Width, image.Height, image.RgbData, isMask: false, softMaskRef));
        var content = BuildImageBytes(imageKey, x, y, width, height);

        CommitPageContent(
            adapter,
            existing,
            builder,
            targetDict,
            content,
            [(PdfName.XObject.Value, imageKey, imageObj.ToReference())]
        );
    }

    private static byte[] BuildImageBytes(string imageKey, double x, double y, double width, double height)
    {
        var buf = new ArrayBufferWriter<byte>(64);
        var csw = new ContentStreamWriter(buf);

        // Image space is the unit square; the CTM scales it to width x height and translates to (x, y).
        csw.Op("q"u8);
        csw.Float((float)width);
        csw.Float(0);
        csw.Float(0);
        csw.Float((float)height);
        csw.Float((float)x);
        csw.Float((float)y);
        csw.Op("cm"u8);
        csw.Name(imageKey);
        csw.Op("Do"u8);
        csw.Op("Q"u8);

        return buf.WrittenMemory.ToArray();
    }

    private static PdfStream MakeImageStream(
        int width,
        int height,
        byte[] samples,
        bool isMask,
        PdfIndirectReference? softMask = null
    )
    {
        var data = Compress(samples);
        var entries = new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.XObject,
            [PdfName.Subtype.Value] = PdfName.Get("Image"),
            [PdfName.Width.Value] = new PdfInteger(width),
            [PdfName.Height.Value] = new PdfInteger(height),
            [PdfName.ColorSpace.Value] = PdfName.Get(isMask ? "DeviceGray" : "DeviceRGB"),
            [PdfName.BitsPerComponent.Value] = new PdfInteger(8),
            [PdfName.Filter.Value] = PdfName.FlateDecode,
            [PdfName.Length.Value] = new PdfInteger(data.Length)
        };
        if (softMask is not null)
            entries[PdfName.SMask.Value] = softMask;

        return new PdfStream(new PdfDictionary(entries), data);
    }

    // ── Shared commit ─────────────────────────────────────────────────────────

    private static void CommitPageContent(
        PdfDocumentAdapter adapter,
        IReadOnlyCollection<PdfIndirectObject> existing,
        ObjectGraphBuilder builder,
        PdfDictionary targetDict,
        byte[] contentBytes,
        IReadOnlyList<(string Category, string Key, PdfObject Ref)> resources
    )
    {
        var streamObj = builder.Add(
            new PdfStream(
                new PdfDictionary(
                    new Dictionary<string, PdfObject>
                    {
                        [PdfName.Length.Value] = new PdfInteger(contentBytes.Length)
                    }
                ),
                contentBytes
            )
        );

        var swaps = new Dictionary<int, PdfIndirectObject>();
        foreach (var obj in existing.Where(o => ReferenceEquals(o.Value, targetDict)))
        {
            var rebuilt = PageContentComposer.RebuildPage(
                (PdfDictionary)obj.Value,
                streamObj.ToReference(),
                prepend: false,
                resources,
                adapter.Core
            );
            swaps[obj.ObjectNumber] = new PdfIndirectObject(obj.ObjectNumber, obj.Generation, rebuilt);
        }

        var finalObjects = existing
            .Select(o => swaps.GetValueOrDefault(o.ObjectNumber, o))
            .Concat(builder.Objects)
            .ToList();

        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Picks a resource name in the given category that is not already present on the page, so
    // multiple draws on the same page never clobber each other's shared /Resources entries.
    private static string UniqueResourceKey(PdfDictionary page, string category, string prefix, PdfDocumentCore core)
    {
        var resources = core.ResolveDict(page[PdfName.Resources]);
        var categoryDict = core.ResolveDict(resources?[PdfName.Get(category)]);
        var used = new HashSet<string>(categoryDict?.Entries.Keys ?? []);

        for (var i = 0;; i++)
        {
            var key = $"{prefix}{i}";
            if (used.Add(key)) return key;
        }
    }

    private static PdfDictionary MakeFontDict(string baseFontName) =>
        new(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Font,
                [PdfName.Subtype.Value] = PdfName.Type1,
                [PdfName.BaseFont.Value] = PdfName.Get(baseFontName)
            }
        );

    private static byte[] Compress(ReadOnlySpan<byte> data)
    {
        using var ms = new MemoryStream();
        using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data);
        return ms.ToArray();
    }

    private static IPdfDocument CreateBlankDocument(double width, double height)
    {
        var pagesRef = new PdfIndirectReference(2, 0);
        var pageDict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Page,
                [PdfName.Parent.Value] = pagesRef,
                [PdfName.MediaBox.Value] = new PdfArray(
                    [new PdfReal(0), new PdfReal(0), new PdfReal(width), new PdfReal(height)]
                )
            }
        );
        var pagesDict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Pages,
                [PdfName.Kids.Value] = new PdfArray([new PdfIndirectReference(1, 0)]),
                [PdfName.Count.Value] = new PdfInteger(1)
            }
        );
        var catalogDict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Catalog,
                [PdfName.Pages.Value] = pagesRef
            }
        );

        var objects = new List<PdfIndirectObject>
        {
            new(1, 0, pageDict),
            new(2, 0, pagesDict),
            new(3, 0, catalogDict)
        };
        var trailer = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Size.Value] = new PdfInteger(4),
                [PdfName.Root.Value] = new PdfIndirectReference(3, 0)
            }
        );

        return ObjectGraphBuilder.SerializeToDocument(objects, trailer);
    }
}
