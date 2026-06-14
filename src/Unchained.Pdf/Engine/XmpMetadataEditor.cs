using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Engine;

/// <summary>Default <see cref="IXmpMetadataEditor" /> implementation.</summary>
// ReSharper disable once MemberCanBeInternal
public sealed class XmpMetadataEditor : IXmpMetadataEditor
{
    /// <inheritdoc />
    public Task SetXmpMetadataAsync(
        IPdfDocument document,
        string xmpXml,
        CancellationToken ct = default
    ) => Task.Run(() => SetXmp(document, xmpXml), ct);

    /// <inheritdoc />
    public Task RemoveMetadataAsync(
        IPdfDocument document,
        CancellationToken ct = default
    ) => Task.Run(() => RemoveXmp(document), ct);

    private static void SetXmp(IPdfDocument document, string xmpXml)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var existing = adapter.Core.CollectObjects();
        var maxObjNum = existing.Count > 0 ? existing.Max(static o => o.ObjectNumber) : 0;
        var builder = new ObjectGraphBuilder(maxObjNum + 1);

        var xmpBytes = xmpXml.ToUtf8Span();
        var metaStream = builder.Add(new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Metadata,
                [PdfName.Subtype.Value] = PdfName.XML,
                [PdfName.Length.Value] = new PdfInteger(xmpBytes.Length)
            }),
            xmpBytes.ToArray()));

        var catalogObj = existing.First(static o =>
            o.Value is PdfDictionary d && d.IsCatalog());
        var catEntries = new Dictionary<string, PdfObject>(((PdfDictionary)catalogObj.Value).Entries)
        {
            [PdfName.Metadata.Value] = metaStream.ToReference()
        };
        var rebuiltCatalog = new PdfIndirectObject(catalogObj.ObjectNumber, catalogObj.Generation, new PdfDictionary(catEntries));

        var finalObjects = existing
            .Select(o => o.ObjectNumber == catalogObj.ObjectNumber ? rebuiltCatalog : o)
            .Concat(builder.Objects)
            .ToList();

        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }

    private static void RemoveXmp(IPdfDocument document)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var existing = adapter.Core.CollectObjects();

        var catalogObj = existing.First(static o => o.Value is PdfDictionary d && d.IsCatalog());
        var catEntries = new Dictionary<string, PdfObject>(((PdfDictionary)catalogObj.Value).Entries);
        catEntries.Remove(PdfName.Metadata.Value);

        var rebuiltCatalog = new PdfIndirectObject(catalogObj.ObjectNumber, catalogObj.Generation, new PdfDictionary(catEntries));
        var finalObjects = existing
            .Select(o => o.ObjectNumber == catalogObj.ObjectNumber ? rebuiltCatalog : o)
            .ToList();

        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }
}
