using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Unchained.Drawing.Extensions;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;
using Unchained.Pdf.Writing;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Adapts <see cref="PdfDocumentCore" /> to the public <see cref="IPdfDocument" /> interface.
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
                    GetInfoString(info, "Title"),
                    GetInfoString(info, "Author"),
                    GetInfoString(info, "Subject"),
                    GetInfoString(info, "Keywords"),
                    GetInfoString(info, "Creator"),
                    GetInfoString(info, "Producer"),
                    null,
                    null);
        }
    }

    /// <inheritdoc />
    public bool IsEncrypted => Core.IsEncrypted;

    /// <inheritdoc />
    public PdfPermissions Permissions => Core.EncryptionPermissions;

    /// <inheritdoc />
    public PdfEncryptionAlgorithm? CryptoAlgorithm => Core.EncryptionAlgorithm;

    /// <inheritdoc />
    public bool IsDisposed => _disposed == 1;

    /// <inheritdoc />
    public bool IsLinearized => Core.IsLinearized;

    /// <inheritdoc />
    public bool IsTagged
    {
        get
        {
            var markInfo = Core.Catalog[PdfName.MarkInfo];
            var dict = markInfo switch
            {
                PdfDictionary d => d,
                PdfIndirectReference r => Core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
                _ => null
            };
            return dict?[PdfName.Marked] is PdfBoolean { Value: true };
        }
    }

    /// <inheritdoc />
    public bool IsPdfaCompliant
    {
        get
        {
            var xmp = GetXmpMetadata();
            return xmp is not null &&
                   xmp.Contains("pdfaid", StringComparison.OrdinalIgnoreCase) &&
                   (xmp.Contains("part>1", StringComparison.Ordinal) ||
                    xmp.Contains("part>2", StringComparison.Ordinal) ||
                    xmp.Contains("part>3", StringComparison.Ordinal));
        }
    }

    /// <inheritdoc />
    public bool IsPdfUaCompliant
    {
        get
        {
            var xmp = GetXmpMetadata();
            return xmp is not null && xmp.Contains("pdfuaid", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc />
    public (string First, string Second)? Id
    {
        get
        {
            var idArr = Core.Trailer.Get<PdfArray>(PdfName.Get("ID"));
            if (idArr is null || idArr.Count < 2)
                return null;

            var first = IdEntryToHex(idArr[0]);
            var second = IdEntryToHex(idArr[1]);

            return (first, second);
        }
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

    // ── Viewer preferences ────────────────────────────────────────────────────

    /// <inheritdoc />
    public ViewerPreferences GetViewerPreferences()
    {
        var vp = ResolveDict(Core.Catalog[PdfName.ViewerPreferences]);

        return vp is null
            ? ViewerPreferences.Default
            : new ViewerPreferences(
                vp[PdfName.Get("HideToolbar")] is PdfBoolean { Value: true },
                vp[PdfName.Get("HideMenubar")] is PdfBoolean { Value: true },
                vp[PdfName.Get("HideWindowUI")] is PdfBoolean { Value: true },
                vp[PdfName.Get("FitWindow")] is PdfBoolean { Value: true },
                vp[PdfName.Get("CenterWindow")] is PdfBoolean { Value: true },
                vp[PdfName.Get("DisplayDocTitle")] is PdfBoolean { Value: true },
                (vp.GetName("Direction") ?? string.Empty) == "R2L" ? ReadingDirection.RightToLeft : ReadingDirection.LeftToRight,
                (vp.GetName("Duplex") ?? string.Empty) switch
                {
                    "Simplex" => DuplexMode.Simplex,
                    "DuplexFlipShortEdge" => DuplexMode.DuplexFlipShortEdge,
                    "DuplexFlipLongEdge" => DuplexMode.DuplexFlipLongEdge,
                    _ => DuplexMode.None
                },
                ParsePageMode(vp.GetName("NonFullScreenPageMode"))
            );
    }

    /// <inheritdoc />
    public PageLayout PageLayout => (Core.Catalog.GetName(PdfName.PageLayout.Value) ?? string.Empty) switch
    {
        "SinglePage" => PageLayout.SinglePage,
        "OneColumn" => PageLayout.OneColumn,
        "TwoColumnLeft" => PageLayout.TwoColumnLeft,
        "TwoColumnRight" => PageLayout.TwoColumnRight,
        "TwoPageLeft" => PageLayout.TwoPageLeft,
        "TwoPageRight" => PageLayout.TwoPageRight,
        _ => PageLayout.Default
    };

    /// <inheritdoc />
    public PageMode PageMode => ParsePageMode(Core.Catalog.GetName(PdfName.PageMode.Value));

    // ── XMP metadata ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public string? GetXmpMetadata()
    {
        var metaRef = Core.Catalog[PdfName.Metadata];
        var stream = metaRef switch
        {
            PdfStream s => s,
            PdfIndirectReference r => Core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
            _ => null
        };
        if (stream is null) return null;

        var decoded = StreamFilters.Decode(stream);
        return decoded.Span.FromUtf8Span();
    }

    // ── Named destinations ────────────────────────────────────────────────────

    /// <inheritdoc />
    public IReadOnlyList<NamedDestination> GetNamedDestinations()
    {
        var result = new List<NamedDestination>();
        var catalog = Core.Catalog;

        // Try /Names /Dests name tree (PDF 1.2+)
        var namesDict = ResolveDict(catalog[PdfName.Names]);
        var destsTree = namesDict is not null ? ResolveDict(namesDict[PdfName.Dests]) : null;
        if (destsTree is not null)
            CollectNameTree(destsTree, result);

        // Legacy /Dests dict (PDF 1.0)
        var legacyDests = ResolveDict(catalog[PdfName.Dests]);

        if (legacyDests is null)
            return result;

        foreach (var (name, value) in legacyDests.Entries)
        {
            var resolved = value is PdfIndirectReference r
                ? Core.ResolveIndirect(r.ObjectNumber).Value
                : value;
            var pageNum = ResolveDestPageFromObject(resolved);
            if (pageNum > 0)
                result.Add(new NamedDestination(name, pageNum));
        }

        return result;
    }

    public IReadOnlyList<OptionalContentGroup> GetLayers()
    {
        var result = new List<OptionalContentGroup>();
        var ocProps = ResolveDict(Core.Catalog[PdfName.Get("OCProperties")]);
        if (ocProps is null) return result;

        // Collect the set of OCGs that are OFF in the default (/D) configuration.
        var off = new HashSet<int>();
        var defaultCfg = ResolveDict(ocProps[PdfName.Get("D")]);
        if (defaultCfg?[PdfName.Get("OFF")] is PdfArray offArr)
        {
            foreach (var e in offArr.Elements)
            {
                if (e is PdfIndirectReference offRef)
                    off.Add(offRef.ObjectNumber);
            }
        }

        if (ocProps[PdfName.Get("OCGs")] is not PdfArray ocgs)
            return result;

        foreach (var e in ocgs.Elements)
        {
            if (e is not PdfIndirectReference r) continue;
            if (Core.ResolveIndirect(r.ObjectNumber).Value is not PdfDictionary ocg) continue;

            var name = ocg[PdfName.Get("Name")] is PdfString s
                ? Encoding.Latin1.GetString(s.Bytes.Span)
                : string.Empty;
            result.Add(new OptionalContentGroup(name, r.ObjectNumber, !off.Contains(r.ObjectNumber)));
        }

        return result;
    }

    // ── Bookmarks ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public IReadOnlyList<Bookmark> GetBookmarks()
    {
        var outlines = ResolveDict(Core.Catalog[PdfName.Outlines]);
        return outlines is null ? [] : ReadOutlineLevel(outlines);
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
        CollectFields(fields, string.Empty, result);

        return result;
    }

    /// <summary>
    ///     Disposes the current core and replaces it with <paramref name="newCore" />.
    ///     Called by <see cref="TableGenerator" /> and <see cref="DocumentMerger" /> after
    ///     in-place document mutation (e.g. <c>AppendTableAsync</c>, <c>MergeAsync</c>).
    /// </summary>
    internal void ReplaceCore(PdfDocumentCore newCore)
    {
        Core.Dispose();
        Core = newCore;
        Pages = new PdfPageCollectionAdapter(Core);
    }

    private static string IdEntryToHex(PdfObject obj) =>
        obj is PdfString s
            ? BitConverter.ToString(s.Bytes.ToArray()).Replace("-", string.Empty)
            : string.Empty;

    /// <summary>
    ///     Performs a full-rewrite serialization: collects every in-use indirect object
    ///     from the document, rebuilds the xref table with fresh byte offsets, and
    ///     writes a clean PDF via <see cref="PdfWriter" />.
    /// </summary>
    internal byte[] Serialize(SaveOptions? options)
    {
        var objects = Core.CollectObjects().ToList();

        // ── OptimizeSize — compress streams + deduplicate objects ─────────────
        if (options?.OptimizeSize == true)
        {
            // Run both optimizers in-place via the mutation helpers before collecting.
            DocumentOptimizer.OptimizeInPlace(this);
            DocumentOptimizer.OptimizeResourcesInPlace(this);
            objects = Core.CollectObjects().ToList();
        }

        // ── AllowReusePageContent — deduplicate identical content streams ──────
        if (options?.AllowReusePageContent == true)
            objects = DeduplicateContentStreams(objects);

        var maxObjNum = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;

        var trailerEntries = new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(maxObjNum + 1),
            [PdfName.Root.Value] = Core.Trailer[PdfName.Root] ?? throw new PdfException("Document trailer is missing required /Root entry.")
        };

        if (Core.Trailer[PdfName.Info] is { } infoRef)
            trailerEntries[PdfName.Info.Value] = infoRef;

        // ── Encryption ───────────────────────────────────────────────────────
        if (options?.Encryption is { } encOpts)
        {
            // Generate a fresh /ID for this save (required for key derivation).
            var fileId = RandomNumberGenerator.GetBytes(16);
            var (ctx, encryptDict) = PdfEncryption.CreateWriteContext(encOpts, fileId);

            var encObjNum = maxObjNum + 1;
            var encryptRef = new PdfIndirectReference(encObjNum, 0);
            trailerEntries["Encrypt"] = encryptRef;
            trailerEntries["ID"] = new PdfArray([new PdfString(fileId), new PdfString(fileId)]);
            trailerEntries[PdfName.Size.Value] = new PdfInteger(encObjNum + 1);

            // Encrypt all objects except the /Encrypt dict itself.
            var encryptedObjects = objects
                .Select(obj => ctx.EncryptObject(obj))
                .Append(new PdfIndirectObject(encObjNum, 0, encryptDict))
                .ToList();

            var trailer = new PdfDictionary(trailerEntries);
            var buffer = new ArrayBufferWriter<byte>();
            using var writer = new PdfWriter(buffer);
            writer.Write(encryptedObjects, trailer);
            return buffer.WrittenMemory.ToArray();
        }

        // ── Unencrypted path ─────────────────────────────────────────────────
        {
            var trailer = new PdfDictionary(trailerEntries);

            if (options?.Linearize == true)
                return LinearizedWriter.Write(objects, trailer, Core);

            var buffer = new ArrayBufferWriter<byte>();
            using var writer = new PdfWriter(buffer);
            writer.Write(objects, trailer);
            return buffer.WrittenMemory.ToArray();
        }
    }

    private static PageMode ParsePageMode(string? name) => (name ?? string.Empty) switch
    {
        "UseNone" => PageMode.UseNone,
        "UseOutlines" => PageMode.UseOutlines,
        "UseThumbs" => PageMode.UseThumbs,
        "FullScreen" => PageMode.FullScreen,
        "UseOC" => PageMode.UseOC,
        "UseAttachments" => PageMode.UseAttachments,
        _ => PageMode.Default
    };

    private void CollectNameTree(PdfDictionary node, ICollection<NamedDestination> result)
    {
        // Leaf node: /Names array of (string, dest) pairs
        if (node.Get<PdfArray>(PdfName.Get("Names")) is { } names)
        {
            for (var i = 0; i + 1 < names.Count; i += 2)
            {
                var key = names[i] is PdfString ks
                    ? Encoding.Latin1.GetString(ks.Bytes.Span)
                    : (names[i] as PdfName)?.Value ?? string.Empty;
                var dest = names[i + 1] is PdfIndirectReference nr
                    ? Core.ResolveIndirect(nr.ObjectNumber).Value
                    : names[i + 1];
                var pageNum = ResolveDestPageFromObject(dest);
                if (pageNum > 0 && key.Length > 0)
                    result.Add(new NamedDestination(key, pageNum));
            }
        }

        // Intermediate node: /Kids array of child nodes
        if (node.Get<PdfArray>(PdfName.Kids) is not { } kids)
            return;

        foreach (var kid in kids.Elements)
        {
            var childDict = kid is PdfIndirectReference kr
                ? Core.ResolveIndirect(kr.ObjectNumber).Value as PdfDictionary
                : kid as PdfDictionary;
            if (childDict is not null)
                CollectNameTree(childDict, result);
        }
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

    // Resolves a destination directly from a PdfObject (array or GoTo action dict).
    private int ResolveDestPageFromObject(PdfObject dest)
    {
        var arr = dest switch
        {
            PdfArray a => a,
            PdfDictionary d when d.GetName("S") == "GoTo" =>
                d.Get<PdfArray>(PdfName.Dest),
            _ => null
        };
        if (arr is null || arr.Count == 0)
            return 0;

        if (arr[0] is not PdfIndirectReference pageRef)
            return 0;

        for (var i = 1; i <= Core.PageCount; i++)
        {
            if (ReferenceEquals(Core.GetPage(i), Core.Dereference(pageRef)))
                return i;
        }

        return 0;
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

    // Deduplicates identical content stream objects across pages.
    // Uses SHA256 of stream bytes as the hash key; canonical = lowest object number.
    private static List<PdfIndirectObject> DeduplicateContentStreams(
        IReadOnlyList<PdfIndirectObject> objects
    )
    {
        var seenHashes = new Dictionary<string, int>(StringComparer.Ordinal);
        var remapping = new Dictionary<int, int>();

        foreach (var obj in objects)
        {
            if (obj.Value is not PdfStream stream) continue;
            // Only deduplicate unfiltered content streams (plain operators).
            if (stream.Dictionary[PdfName.Filter] is not null) continue;
            if (stream.Data.Length == 0) continue;

            var hash = Convert.ToBase64String(
                SHA256.HashData(stream.Data.Span));

            if (seenHashes.TryGetValue(hash, out var canonical))
                remapping[obj.ObjectNumber] = canonical;
            else
                seenHashes[hash] = obj.ObjectNumber;
        }

        if (remapping.Count == 0) return objects.ToList();

        return objects
            .Where(o => !remapping.ContainsKey(o.ObjectNumber))
            .Select(o => PdfObjectRemapper.RemapSelective(o, remapping) as PdfIndirectObject ?? o)
            .ToList();
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
