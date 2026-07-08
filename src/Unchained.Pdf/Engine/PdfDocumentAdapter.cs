using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
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
        Pages = new PdfPageCollectionAdapter(core, this);
    }

    internal PdfDocumentCore Core { get; private set; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Bytes => Core.Source;

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
                    null
                );
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
            var dict = Core.ResolveDict(markInfo);
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
            return xmp is not null && xmp.Contains(PdfConstants.PdfAIdentifier, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc />
    public (string First, string Second)? Id
    {
        get
        {
            var idArr = Core.Trailer.Get<PdfArray>(PdfName.ID);
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
        var vp = Core.ResolveDict(Core.Catalog[PdfName.ViewerPreferences]);

        return vp is null
            ? ViewerPreferences.Default
            : new ViewerPreferences(
                vp[PdfName.HideToolbar] is PdfBoolean { Value: true },
                vp[PdfName.HideMenubar] is PdfBoolean { Value: true },
                vp[PdfName.HideWindowUI] is PdfBoolean { Value: true },
                vp[PdfName.FitWindow] is PdfBoolean { Value: true },
                vp[PdfName.CenterWindow] is PdfBoolean { Value: true },
                vp[PdfName.DisplayDocTitle] is PdfBoolean { Value: true },
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
        var stream = Core.ResolveStream(metaRef);
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
        var namesDict = Core.ResolveDict(catalog[PdfName.Names]);
        var destsTree = Core.ResolveDict(namesDict?[PdfName.Dests]);
        if (destsTree is not null)
            CollectNameTree(destsTree, result);

        // Legacy /Dests dict (PDF 1.0)
        var legacyDests = Core.ResolveDict(catalog[PdfName.Dests]);

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
        var ocProps = Core.ResolveDict(Core.Catalog[PdfName.OCProperties]);
        if (ocProps is null) return result;

        // Collect the set of OCGs that are OFF in the default (/D) configuration.
        var off = new HashSet<int>();
        var defaultCfg = Core.ResolveDict(ocProps[PdfName.D]);
        if (defaultCfg?[PdfName.OFF] is PdfArray offArr)
        {
            foreach (var offRef in offArr.Elements.OfType<PdfIndirectReference>())
                off.Add(offRef.ObjectNumber);
        }

        if (ocProps[PdfName.OCGs] is not PdfArray ocgs)
            return result;

        foreach (var r in ocgs.Elements.OfType<PdfIndirectReference>())
        {
            if (Core.ResolveIndirect(r.ObjectNumber).Value is not PdfDictionary ocg) continue;

            var name = ocg[PdfName.Name] is PdfString s
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
        var outlines = Core.ResolveDict(Core.Catalog[PdfName.Outlines]);
        return outlines is null ? [] : ReadOutlineLevel(outlines);
    }

    /// <inheritdoc />
    public IReadOnlyList<FormField> GetFormFields()
    {
        var acroForm = Core.ResolveDict(Core.Catalog[PdfName.AcroForm]);
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
        Pages = new PdfPageCollectionAdapter(Core, this);
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
            trailerEntries[PdfName.Encrypt.Value] = encryptRef;
            trailerEntries[PdfName.ID.Value] = new PdfArray([new PdfString(fileId), new PdfString(fileId)]);
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

    private void CollectNameTree(PdfDictionary node, ICollection<NamedDestination> result) =>
        MutationHelper.CollectTree(
            node,
            Core,
            result,
            PdfName.Names.Value,
            (_, arr, i, _) =>
            {
                var key = arr[i] is PdfString ks
                    ? Encoding.Latin1.GetString(ks.Bytes.Span)
                    : (arr[i] as PdfName)?.Value ?? string.Empty;
                if (key.Length == 0) return null;

                var dest = arr[i + 1] is PdfIndirectReference nr
                    ? Core.ResolveIndirect(nr.ObjectNumber).Value
                    : arr[i + 1];
                var pageNum = ResolveDestPageFromObject(dest);
                return pageNum > 0 ? new NamedDestination(key, pageNum) : null;
            }
        );

    private IReadOnlyList<Bookmark> ReadOutlineLevel(PdfDictionary node)
    {
        var result = new List<Bookmark>();
        var current = Core.ResolveDict(node[PdfName.First]);
        while (current is not null)
        {
            var title = current[PdfName.Title] is PdfString ts
                ? Encoding.Latin1.GetString(ts.Bytes.Span)
                : string.Empty;

            var pageNum = ResolveDestPage(current);
            var children = Core.ResolveDict(current[PdfName.First]) is not null
                ? ReadOutlineLevel(current)
                : null;

            result.Add(new Bookmark(title, pageNum, children));
            current = Core.ResolveDict(current[PdfName.Next]);
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
        foreach (var dict in fields.Elements.Select(x => Core.ResolveDict(x)).Where(static x => x != null))
        {
            var partialName = dict![PdfName.T] is PdfString ts
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
        var v = dict[PdfName.V];
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

        // Only deduplicate unfiltered, non-empty content streams (plain operators).
        var candidates = objects
            .Where(static o =>
                o.Value is PdfStream stream &&
                stream.Dictionary[PdfName.Filter] is null &&
                stream.Data.Length != 0
            );
        foreach (var obj in candidates)
        {
            var stream = (PdfStream)obj.Value;
            var hash = Convert.ToBase64String(
                SHA256.HashData(stream.Data.Span)
            );

            if (seenHashes.TryGetValue(hash, out var canonical))
                remapping[obj.ObjectNumber] = canonical;
            else
                seenHashes[hash] = obj.ObjectNumber;
        }

        return remapping.Count == 0
            ? objects.ToList()
            : objects
                .Where(o => !remapping.ContainsKey(o.ObjectNumber))
                .Select(o => PdfObjectRemapper.RemapSelective(o, remapping) as PdfIndirectObject ?? o)
                .ToList();
    }

    private static string? GetInfoString(PdfDictionary info, string key) =>
        info[key] is not PdfString str
            ? null
            : Encoding.Latin1.GetString(str.Bytes.Span);
}
