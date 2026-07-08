using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;

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
        var (existing, builder) = MutationHelper.CollectWithBuilder(adapter);

        var xmpBytes = xmpXml.ToUtf8Span();
        var metaStream = builder.Add(
            new PdfStream(
                new PdfDictionary(
                    new Dictionary<string, PdfObject>
                    {
                        [PdfName.Type.Value] = PdfName.Metadata,
                        [PdfName.Subtype.Value] = PdfName.XML,
                        [PdfName.Length.Value] = new PdfInteger(xmpBytes.Length)
                    }
                ),
                xmpBytes.ToArray()
            )
        );

        var finalObjects = MutationHelper.ModifyCatalog(
            adapter,
            existing,
            catEntries =>
            {
                catEntries[PdfName.Metadata.Value] = metaStream.ToReference();
            }
        );
        finalObjects.AddRange(builder.Objects);

        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }

    private static void RemoveXmp(IPdfDocument document)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var existing = adapter.Core.CollectObjects();

        var finalObjects = MutationHelper.ModifyCatalog(
            adapter,
            existing,
            static catEntries =>
            {
                catEntries.Remove(PdfName.Metadata.Value);
            }
        );

        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }
}
