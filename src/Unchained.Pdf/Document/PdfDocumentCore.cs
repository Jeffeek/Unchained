using Unchained.Pdf.Core;
using Unchained.Pdf.Parsing;

namespace Unchained.Pdf.Document;

/// <summary>
/// Internal document model. Owns the source byte buffer and provides lazy,
/// cached resolution of indirect objects via the cross-reference table.
/// <para>
/// Objects are parsed on first access and stored in an internal cache;
/// subsequent dereferences of the same object number return the cached instance
/// without re-parsing. Memory consumption is proportional to the number of
/// objects accessed, not the total number present in the file.
/// </para>
/// </summary>
internal sealed class PdfDocumentCore : IDisposable
{
    private readonly ReadOnlyMemory<byte> _source;
    private readonly CrossReferenceTable _xref;
    private readonly PdfDictionary _trailer;
    private readonly PdfParser _parser;
    private readonly Dictionary<int, PdfIndirectObject> _cache = new();
    private bool _disposed;

    private PdfDocumentCore(
        ReadOnlyMemory<byte> source,
        CrossReferenceTable xref,
        PdfDictionary trailer)
    {
        _source = source;
        _xref = xref;
        _trailer = trailer;
        _parser = new PdfParser(source);
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the structural skeleton of a PDF document from <paramref name="source"/>
    /// and returns a ready-to-use <see cref="PdfDocumentCore"/>.
    /// Only the cross-reference table and trailer dictionary are parsed at this point;
    /// all other objects are resolved lazily on first access.
    /// </summary>
    /// <exception cref="PdfException">
    /// Thrown when the file structure (<c>startxref</c>, cross-reference table,
    /// or trailer dictionary) is missing or malformed.
    /// </exception>
    public static PdfDocumentCore Parse(ReadOnlyMemory<byte> source)
    {
        var parser = new PdfParser(source);
        var (xref, trailer) = parser.ParseStructure();
        return new PdfDocumentCore(source, xref, trailer);
    }

    // ── Document properties ───────────────────────────────────────────────────

    /// <summary>The raw trailer dictionary from the most-recent update section.</summary>
    public PdfDictionary Trailer => _trailer;

    /// <summary>
    /// The document catalog dictionary (<c>/Type /Catalog</c>), resolved from
    /// the <c>/Root</c> entry in <see cref="Trailer"/>.
    /// </summary>
    /// <exception cref="PdfException">Thrown when <c>/Root</c> is absent or malformed.</exception>
    public PdfDictionary Catalog => Resolve<PdfDictionary>(
        GetRequiredRef("Root"), "Catalog must be a dictionary.");

    /// <summary>
    /// The document information dictionary (<c>/Info</c>), or <see langword="null"/>
    /// if the document does not carry one.
    /// </summary>
    public PdfDictionary? Info =>
        _trailer["Info"] is PdfIndirectReference infoRef
            ? Resolve<PdfDictionary>(infoRef, "Info must be a dictionary.")
            : null;

    /// <summary>
    /// The total number of pages declared in the root Pages tree node (<c>/Count</c>).
    /// Triggers resolution of the Catalog and root Pages objects on first access.
    /// </summary>
    public int PageCount
    {
        get
        {
            var pages = Resolve<PdfDictionary>(
                GetRefFromDict(Catalog, "Pages"), "Pages must be a dictionary.");
            return (int)(pages.Get<PdfInteger>(PdfName.Count)?.Value ?? 0);
        }
    }

    // ── Object resolution ─────────────────────────────────────────────────────

    /// <summary>
    /// Follows one level of indirection: if <paramref name="obj"/> is a
    /// <see cref="PdfIndirectReference"/> it is resolved and its value returned;
    /// otherwise <paramref name="obj"/> is returned unchanged.
    /// Does not recurse — call again if the resolved value is itself a reference.
    /// </summary>
    public PdfObject Dereference(PdfObject obj) => obj switch
    {
        PdfIndirectReference r => ResolveIndirect(r.ObjectNumber).Value,
        _ => obj,
    };

    /// <summary>
    /// Resolves <paramref name="reference"/>, dereferences one level, and casts to
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="PdfException">
    /// Thrown when the resolved value cannot be cast to <typeparamref name="T"/>.
    /// <paramref name="errorMessage"/> is used as the exception message.
    /// </exception>
    public T Resolve<T>(PdfIndirectReference reference, string errorMessage) where T : PdfObject =>
        Dereference(ResolveIndirect(reference.ObjectNumber).Value) as T
            ?? throw new PdfException(errorMessage);

    /// <summary>
    /// Resolves and parses the indirect object identified by <paramref name="objectNumber"/>,
    /// caching it for subsequent requests.
    /// </summary>
    /// <exception cref="PdfException">
    /// Thrown when the object is marked free in the xref table, when the xref entry
    /// is missing, or when the object body cannot be parsed.
    /// </exception>
    /// <exception cref="NotImplementedException">
    /// Thrown when the object lives in a compressed object stream (§7.5.7),
    /// which is not yet implemented.
    /// </exception>
    public PdfIndirectObject ResolveIndirect(int objectNumber)
    {
        if (_cache.TryGetValue(objectNumber, out var cached))
            return cached;

        var entry = _xref.GetEntry(objectNumber);
        if (entry.IsFree)
            throw new PdfException($"Object {objectNumber} is marked free in the xref table.");
        if (entry.Type == CrossReferenceEntryType.Compressed)
            throw new NotImplementedException("Object streams (§7.5.7) — implementation pending.");

        var obj = _parser.ReadObject(entry.Offset);
        _cache[objectNumber] = obj;
        return obj;
    }

    // ── Page access ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the page dictionary for the given 1-based <paramref name="pageNumber"/>
    /// by walking the page tree (ISO 32000-1 §7.7.3).
    /// Only the nodes on the direct path to the target page are resolved.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageNumber"/> is less than 1 or greater than <see cref="PageCount"/>.
    /// </exception>
    /// <exception cref="PdfException">Thrown when the page tree structure is malformed.</exception>
    public PdfDictionary GetPage(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber,
                $"Page number must be between 1 and {PageCount}.");
        }

        var pagesRef = GetRefFromDict(Catalog, "Pages");
        return FindPageInTree(pagesRef, pageNumber, ref pageNumber);
    }

    private PdfDictionary FindPageInTree(PdfIndirectReference nodeRef, int target, ref int remaining)
    {
        var node = Resolve<PdfDictionary>(nodeRef, "Page tree node must be a dictionary.");
        var type = node.GetName("Type");

        if (type == "Page")
        {
            remaining--;
            return remaining == 0 ? node
                : throw new PdfException($"Page tree traversal error at node {nodeRef}.");
        }

        var kids = node.Get<PdfArray>(PdfName.Kids)
            ?? throw new PdfException("Pages node missing /Kids array.");

        foreach (var kid in kids.Elements)
        {
            if (kid is not PdfIndirectReference kidRef)
                throw new PdfException("Kids entry is not an indirect reference.");

            var kidNode = Resolve<PdfDictionary>(kidRef, "Kid must be a dictionary.");
            var kidType = kidNode.GetName("Type");

            if (kidType == "Page")
            {
                remaining--;
                if (remaining == 0) return kidNode;
            }
            else
            {
                // Skip entire subtrees that don't contain the target page.
                var subtreeCount = (int)(kidNode.Get<PdfInteger>(PdfName.Count)?.Value ?? 1);
                if (remaining <= subtreeCount)
                    return FindPageInTree(kidRef, target, ref remaining);
                remaining -= subtreeCount;
            }
        }

        throw new PdfException($"Could not find page {target} in page tree.");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private PdfIndirectReference GetRequiredRef(string key) =>
        _trailer[key] as PdfIndirectReference
            ?? throw new PdfException($"Trailer is missing required /{key} indirect reference.");

    private static PdfIndirectReference GetRefFromDict(PdfDictionary dict, string key) =>
        dict[key] as PdfIndirectReference
            ?? throw new PdfException($"Dictionary is missing required /{key} indirect reference.");

    /// <summary>
    /// Clears the object cache and marks the instance as disposed.
    /// The source byte buffer is not freed here; its lifetime is managed externally.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cache.Clear();
    }
}
