using System.IO.Compression;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Engine;

/// <summary>Default <see cref="IDocumentOptimizer" /> implementation.</summary>
public sealed class DocumentOptimizer : IDocumentOptimizer
{
    private const int CompressionThresholdBytes = 128;

    /// <inheritdoc />
    public Task OptimizeAsync(IPdfDocument document, CancellationToken ct = default) =>
        Task.Run(() => Optimize(document), ct);

    /// <inheritdoc />
    public Task OptimizeResourcesAsync(IPdfDocument document, CancellationToken ct = default) =>
        Task.Run(() => OptimizeResources(document), ct);

    /// <summary>Synchronous compress-streams pass — used internally by <c>SaveOptions.OptimizeSize</c>.</summary>
    internal static void OptimizeInPlace(IPdfDocument document) => Optimize(document);

    /// <summary>Synchronous deduplication pass — used internally by <c>SaveOptions.OptimizeSize</c>.</summary>
    internal static void OptimizeResourcesInPlace(IPdfDocument document) => OptimizeResources(document);

    // ── Optimize — compress uncompressed streams ──────────────────────────────

    private static void Optimize(IPdfDocument document)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var existing = adapter.Core.CollectObjects();
        var changed = false;
        var finalObjects = existing.Select(o =>
                {
                    if (o.Value is not PdfStream stream) return o;
                    // Skip already-filtered streams and tiny ones that wouldn't benefit.
                    if (stream.Dictionary[PdfName.Filter] is not null) return o;
                    if (stream.Data.Length < CompressionThresholdBytes) return o;

                    var compressed = Compress(stream.Data.Span);
                    if (compressed.Length >= stream.Data.Length) return o; // no gain

                    var newDict = new PdfDictionary(
                        new Dictionary<string, PdfObject>(stream.Dictionary.Entries)
                        {
                            [PdfName.Filter.Value] = PdfName.FlateDecode,
                            [PdfName.Length.Value] = new PdfInteger(compressed.Length)
                        }
                    );
                    changed = true;
                    return new PdfIndirectObject(o.ObjectNumber, o.Generation, new PdfStream(newDict, compressed));
                }
            )
            .ToList();

        if (changed)
            MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }

    // ── OptimizeResources — deduplicate identical streams ────────────────────

    private static void OptimizeResources(IPdfDocument document)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        var existing = adapter.Core.CollectObjects();

        // Group streams by their byte content; keep the lowest object number as canonical.
        var seenHashes = new Dictionary<string, int>(StringComparer.Ordinal); // hash → canonical objNum
        var remapping = new Dictionary<int, int>();                           // old objNum → canonical objNum

        foreach (var obj in existing.Where(static o => o.Value is PdfStream))
        {
            var stream = (PdfStream)obj.Value;
            var key = ComputeKey(stream);
            if (seenHashes.TryGetValue(key, out var canonical))
                remapping[obj.ObjectNumber] = canonical;
            else
                seenHashes[key] = obj.ObjectNumber;
        }

        if (remapping.Count == 0) return;

        // Remap all indirect references and drop duplicate stream objects.
        var finalObjects = existing
            .Where(o => !remapping.ContainsKey(o.ObjectNumber))
            .Select(o => RemapObject(o, remapping))
            .ToList();

        MutationHelper.SerializeAndReplace(adapter, finalObjects);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static byte[] Compress(ReadOnlySpan<byte> data)
    {
        using var ms = new MemoryStream();
        using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, true))
            zlib.Write(data);
        return ms.ToArray();
    }

    private static string ComputeKey(PdfStream stream)
    {
        // Use a cheap content hash: Length + first/last 64 bytes + total length.
        var data = stream.Data.Span;
        var prefix = data.Length > 64 ? data[..64] : data;
        var suffix = data.Length > 128 ? data[^64..] : ReadOnlySpan<byte>.Empty;
        return $"{data.Length}:{Convert.ToBase64String(prefix)}{Convert.ToBase64String(suffix)}";
    }

    private static PdfIndirectObject RemapObject(PdfIndirectObject obj, IReadOnlyDictionary<int, int> remapping)
    {
        var remapped = PdfObjectRemapper.RemapSelective(obj, remapping);
        return remapped as PdfIndirectObject ?? obj;
    }
}
