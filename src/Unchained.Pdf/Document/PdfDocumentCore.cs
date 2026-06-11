using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Document;

/// <summary>
/// Internal document model. Owns the source byte buffer and provides lazy,
/// cached resolution of indirect objects via the cross-reference table.
/// <para>
/// Objects are parsed on first access and stored in an internal cache;
/// subsequent dereferences of the same object number return the cached instance
/// without reparsing. Memory consumption is proportional to the number of
/// objects accessed, not the total number present in the file.
/// </para>
/// </summary>
internal sealed class PdfDocumentCore : IDisposable
{
    // ReSharper disable once NotAccessedField.Local
    private readonly ReadOnlyMemory<byte> _source;
    private readonly CrossReferenceTable _xref;
    private readonly PdfParser _parser;
    private readonly Dictionary<int, PdfIndirectObject> _cache = new();
    // Object stream cache: stream object number → (objectNumber → decoded PdfObject)
    // Avoids re-decompressing the same object stream when multiple objects are resolved from it.
    private readonly Dictionary<int, Dictionary<int, PdfObject>> _objectStreamCache = new();
    private bool _disposed;

    private PdfDocumentCore(ReadOnlyMemory<byte> source, CrossReferenceTable xref, PdfDictionary trailer)
    {
        _source = source;
        _xref = xref;
        Trailer = trailer;
        _parser = new PdfParser(source);
    }

    /// <summary>
    /// When <see langword="true"/>, <see cref="ResolveIndirect"/> returns
    /// <see cref="PdfNull.Instance"/> instead of throwing when an object cannot be parsed.
    /// Useful for processing real-world PDFs with isolated corrupt objects.
    /// </summary>
    internal bool IgnoreCorruptedObjects { get; set; }

    /// <summary>
    /// Evicts all resolved objects from the in-memory cache.
    /// Subsequent accesses will reparse from the source buffer.
    /// Call this to reduce memory pressure after processing a large document.
    /// </summary>
    internal void TrimCache()
    {
        _cache.Clear();
        _objectStreamCache.Clear();
    }

    // ── Factory ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Rebuilds the cross-reference table from the found objects, and returns a
    /// <see cref="PdfDocumentCore"/> suitable for reading recovered content.
    /// Use this when <see cref="Parse"/> throws due to a corrupted xref.
    /// </summary>
    public static PdfDocumentCore Repair(ReadOnlyMemory<byte> source)
    {
        var span = source.Span;
        var entries = new Dictionary<int, CrossReferenceEntry>();

        // Scan for patterns matching "N G obj" (object headers).
        for (var i = 0; i < span.Length - 6; i++)
        {
            // Look for whitespace followed by digits
            if (span[i] != (byte)'\n' && span[i] != (byte)'\r' && span[i] != (byte)' ')
                continue;

            var start = i + 1;
            if (start >= span.Length || !IsDigit(span[start])) continue;

            // Parse object number
            var pos = start;
            while (pos < span.Length && IsDigit(span[pos])) pos++;
            if (pos >= span.Length || span[pos] != ' ')
                continue;

            if (!int.TryParse(
                    System.Text.Encoding.ASCII.GetString(span[start..pos]),
                    out var objNum) || objNum <= 0) continue;

            // Parse generation number
            var genStart = pos + 1;
            pos = genStart;
            while (pos < span.Length && IsDigit(span[pos]))
                pos++;
            if (pos + 4 >= span.Length || span[pos] != ' ')
                continue;

            if (!int.TryParse(System.Text.Encoding.ASCII.GetString(span[genStart..pos]), out var gen)) continue;

            // Confirm " obj" follows
            if (span[pos + 1] != 'o' || span[pos + 2] != 'b' || span[pos + 3] != 'j')
                continue;
            if (span[pos + 4] != ' ' && span[pos + 4] != '\n' && span[pos + 4] != '\r')
                continue;

            entries.TryAdd(objNum, new CrossReferenceEntry(start - 1, gen, CrossReferenceEntryType.InUse));
        }

        if (entries.Count == 0)
            throw new PdfException("Repair failed: no PDF objects found in the byte stream.");

        // Add free head entry (object 0).
        entries[0] = new CrossReferenceEntry(0, PdfConstants.XrefFreeGenerationNumber, CrossReferenceEntryType.Free);

        // Build synthetic xref using the scanned offsets.
        var syntheticXref = new CrossReferenceTable(entries, trailerOffset: 0);

        // Parse the recovered document. Trailer might be missing — synthesise one from the catalog.
        var parser = new PdfParser(source);
        PdfDictionary? trailer = null;
        try
        {
            (_, trailer) = parser.ParseStructure();
        }
        catch
        {
             /* ignore if trailer is corrupt */
        }

        // Find catalog from scanned objects if trailer is missing.
        if (trailer?["Root"] is not null)
            return new PdfDocumentCore(source, syntheticXref, trailer);

        var tempCore = new PdfDocumentCore(source, syntheticXref, new PdfDictionary());
        PdfIndirectReference? catalogRef = null;
        foreach (var objNum in entries.Keys.Where(static n => n > 0))
        {
            try
            {
                var obj = tempCore.ResolveIndirect(objNum);
                if (obj.Value is not PdfDictionary d || d.GetName("Type") != "Catalog")
                    continue;

                catalogRef = new PdfIndirectReference(objNum, 0);
                break;
            }
            catch
            {
                /* skip unreadable objects */
            }
        }

        if (catalogRef is null)
            throw new PdfException("Repair failed: could not locate document catalog.");

        var size = entries.Keys.Max() + 1;
        trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Root.Value] = catalogRef,
            [PdfName.Size.Value] = new PdfInteger(size)
        });

        return new PdfDocumentCore(source, syntheticXref, trailer);

        static bool IsDigit(byte b) => b is >= (byte)'0' and <= (byte)'9';
    }

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
    public static PdfDocumentCore Parse(ReadOnlyMemory<byte> source, string? password = null)
    {
        var parser = new PdfParser(source);
        var (xref, trailer) = parser.ParseStructure();
        var doc = new PdfDocumentCore(source, xref, trailer);
        doc.InitializeEncryption(password);
        return doc;
    }

    // ── Document properties ───────────────────────────────────────────────────

    // ── Encryption state ──────────────────────────────────────────────────────

    private PdfEncryptionContext? _encryption;
    private int _encryptObjNum = -1; // object number of /Encrypt — not decrypted

    /// <summary>
    /// <see langword="true"/> when the PDF was saved in linearized (web-optimized) form.
    /// Detected by scanning the first 1024 bytes for the <c>/Linearized</c> keyword,
    /// as specified in ISO 32000-1 Annex F §F.3.1.
    /// </summary>
    public bool IsLinearized
    {
        get
        {
            // Scan up to the first 1024 bytes per spec.
            var span = _source.Span;
            var limit = Math.Min(span.Length, PdfConstants.XrefScanWindowBytes);
            var target = "/Linearized"u8;
            for (var i = 0; i <= limit - target.Length; i++)
            {
                if (span.Slice(i, target.Length).SequenceEqual(target))
                    return true;
            }

            return false;
        }
    }

    /// <summary><see langword="true"/> when the source PDF was password-protected.</summary>
    public bool IsEncrypted => _encryption is not null;

    /// <summary>
    /// The encryption algorithm used to protect this document,
    /// or <see langword="null"/> for unencrypted documents.
    /// </summary>
    public PdfEncryptionAlgorithm? EncryptionAlgorithm => _encryption?.Algorithm;

    /// <summary>
    /// Operations permitted when the document is opened with the user password.
    /// Returns <see cref="PdfPermissions.All"/> for unencrypted documents.
    /// </summary>
    public PdfPermissions EncryptionPermissions => _encryption?.Permissions ?? PdfPermissions.All;

    private void InitializeEncryption(string? password)
    {
        var encryptEntry = Trailer[PdfName.Get("Encrypt")];
        if (encryptEntry is null) return;

        PdfDictionary encryptDict;

        switch (encryptEntry)
        {
            case PdfIndirectReference r:
            {
                _encryptObjNum = r.ObjectNumber;
                // Read the /Encrypt object raw (bypassing decryption — it is never encrypted).
                var entry = _xref.GetEntry(r.ObjectNumber);
                var rawObj = entry.Type == CrossReferenceEntryType.Compressed
                    ? ResolveFromObjectStream(r.ObjectNumber, (int)entry.Offset)
                    : _parser.ReadObject(entry.Offset);
                _cache[r.ObjectNumber] = rawObj; // cache the unencrypted version
                encryptDict = rawObj.Value as PdfDictionary ?? throw new PdfException("/Encrypt entry is not a dictionary.");
                break;
            }
            case PdfDictionary d:
            {
                encryptDict = d;
                break;
            }
            default:
            {
                throw new PdfException($"Unexpected /Encrypt type: {encryptEntry.GetType().Name}.");
            }
        }

        // Get the first element of the /ID array as the file identifier.
        var fileId = Array.Empty<byte>();
        if (Trailer.Get<PdfArray>(PdfName.Get("ID")) is [PdfString idStr, ..])
            fileId = idStr.Bytes.ToArray();

        _encryption = PdfEncryption.CreateReadContext(encryptDict, fileId, password ?? string.Empty)
                      ?? throw new PdfEncryptedException(
                          $"Unsupported PDF encryption (V={encryptDict.Get<PdfInteger>("V")?.Value}, " +
                          $"R={encryptDict.Get<PdfInteger>("R")?.Value}).");
    }

    /// <summary>The raw trailer dictionary from the most-recent update section.</summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public PdfDictionary Trailer { get; }

    /// <summary>
    /// The document catalog dictionary (<c>/Type /Catalog</c>), resolved from
    /// the <c>/Root</c> entry in <see cref="Trailer"/>.
    /// </summary>
    /// <exception cref="PdfException">Thrown when <c>/Root</c> is absent or malformed.</exception>
    // ReSharper disable once MemberCanBePrivate.Global
    public PdfDictionary Catalog => Resolve<PdfDictionary>(GetRequiredRef("Root"), "Catalog must be a dictionary.");

    /// <summary>
    /// The document information dictionary (<c>/Info</c>), or <see langword="null"/>
    /// if the document does not carry one.
    /// </summary>
    public PdfDictionary? Info =>
        Trailer["Info"] is PdfIndirectReference infoRef
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
            var pages = Resolve<PdfDictionary>(GetRefFromDict(Catalog, "Pages"), "Pages must be a dictionary.");
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
    // ReSharper disable once MemberCanBePrivate.Global
    public PdfObject Dereference(PdfObject obj) => obj switch
    {
        PdfIndirectReference r => ResolveIndirect(r.ObjectNumber).Value,
        _ => obj
    };

    /// <summary>
    /// Resolves <paramref name="reference"/>, dereferences one level, and casts to
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="PdfException">
    /// Thrown when the resolved value cannot be cast to <typeparamref name="T"/>.
    /// <paramref name="errorMessage"/> is used as the exception message.
    /// </exception>
    private T Resolve<T>(PdfIndirectReference reference, string errorMessage)
        where T : PdfObject => Dereference(ResolveIndirect(reference.ObjectNumber).Value) as T ?? throw new PdfException(errorMessage);

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

        PdfIndirectObject obj;
        try
        {
            obj = entry.Type == CrossReferenceEntryType.Compressed
                ? ResolveFromObjectStream(objectNumber, streamObjNum: (int)entry.Offset)
                : _parser.ReadObject(entry.Offset);
        }
        catch when (IgnoreCorruptedObjects)
        {
            obj = new PdfIndirectObject(objectNumber, 0, PdfNull.Instance);
        }

        // Decrypt if the document is encrypted (skip the /Encrypt object itself).
        if (_encryption is not null && objectNumber != _encryptObjNum)
            obj = _encryption.DecryptObject(obj);

        _cache[objectNumber] = obj;

        return obj;
    }

    // ── Serialization support ─────────────────────────────────────────────────

    /// <summary>
    /// Resolves every in-use indirect object in the document and returns them
    /// sorted by object number. Called by the serialization layer to collect
    /// the full object graph for a full-rewrite save.
    /// </summary>
    internal IReadOnlyList<PdfIndirectObject> CollectObjects() =>
        _xref.InUseObjectNumbers
            .Select(ResolveIndirect)
            .OrderBy(static o => o.ObjectNumber)
            .ToList();

    // ── Object stream resolution (§7.5.7) ────────────────────────────────────

    /// <summary>
    /// Resolves an object that is stored inside a compressed object stream.
    /// The stream is decoded and its embedded objects are parsed and cached so
    /// subsequent calls for siblings from the same stream are free.
    /// </summary>
    private PdfIndirectObject ResolveFromObjectStream(int objectNumber, int streamObjNum)
    {
        if (_objectStreamCache.TryGetValue(streamObjNum, out var streamObjects))
        {
            return !streamObjects.TryGetValue(objectNumber, out var value)
                ? throw new PdfException($"Object {objectNumber} not found in object stream {streamObjNum}.")
                : new PdfIndirectObject(objectNumber, 0, value);
        }

        streamObjects = DecodeObjectStream(streamObjNum);
        _objectStreamCache[streamObjNum] = streamObjects;

        return !streamObjects.TryGetValue(objectNumber, out var value2)
            ? throw new PdfException($"Object {objectNumber} not found in object stream {streamObjNum}.")
            : new PdfIndirectObject(objectNumber, 0, value2);
    }

    // Decompresses an object stream and parses all embedded objects.
    // Returns a map from object number → decoded PdfObject.
    private Dictionary<int, PdfObject> DecodeObjectStream(int streamObjNum)
    {
        // The object stream is itself a regular in-use indirect object.
        var streamEntry = _xref.GetEntry(streamObjNum);
        if (streamEntry.Type == CrossReferenceEntryType.Compressed)
            throw new PdfException($"Object stream {streamObjNum} is itself compressed — nested object streams are not supported.");

        var streamIndirect = _parser.ReadObject(streamEntry.Offset);
        if (streamIndirect.Value is not PdfStream objStream)
            throw new PdfException($"Object {streamObjNum} is expected to be an object stream but is {streamIndirect.Value.GetType().Name}.");

        var n = (int)(objStream.Dictionary.Get<PdfInteger>("N")?.Value ?? throw new PdfException($"Object stream {streamObjNum} missing required /N entry."));
        var first = (int)(objStream.Dictionary.Get<PdfInteger>("First")?.Value ?? throw new PdfException($"Object stream {streamObjNum} missing required /First entry."));

        var decoded = StreamFilters.Decode(objStream);

        // Header section: N pairs of "<objectNumber> <byteOffset>" separated by whitespace.
        // All byte offsets are relative to the start of the body (i.e., offset <first>).
        var headerParser = new PdfParser(decoded);
        var headerLexer = new Lexer(decoded);

        var index = new (int ObjNum, int Offset)[n];
        for (var i = 0; i < n; i++)
        {
            var objNum = (int)ExpectIntegerFromLexer(headerLexer);
            var byteOffset = (int)ExpectIntegerFromLexer(headerLexer);
            index[i] = (objNum, byteOffset);
        }

        // Body section: parse each object value at its declared offset within the stream.
        var result = new Dictionary<int, PdfObject>(n);
        foreach (var (objNum, byteOffset) in index)
        {
            var bodyLexer = new Lexer(decoded, first + byteOffset);
            result[objNum] = headerParser.ReadValue(bodyLexer);
        }

        return result;
    }

    private static long ExpectIntegerFromLexer(Lexer lexer)
    {
        var t = lexer.ReadNext();

        return t.Kind != PdfTokenKind.Integer
            ? throw new PdfException($"Expected integer in object stream header, got {t.Kind}.", t.Offset)
            : ParseRawInteger(t.Raw.Span);
    }

    private static long ParseRawInteger(ReadOnlySpan<byte> span)
    {
        var negative = span[0] == (byte)'-';
        var start = (negative || span[0] == (byte)'+') ? 1 : 0;
        long value = 0;
        for (var i = start; i < span.Length; i++)
            value = (value * 10) + (span[i] - '0');

        return negative ? -value : value;
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
            throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, $"Page number must be between 1 and {PageCount}.");

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
            return remaining == 0
                ? node
                : throw new PdfException($"Page tree traversal error at node {nodeRef}.");
        }

        var kids = node.Get<PdfArray>(PdfName.Kids) ?? throw new PdfException("Pages node missing /Kids array.");

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
        Trailer[key] as PdfIndirectReference ?? throw new PdfException($"Trailer is missing required /{key} indirect reference.");

    private static PdfIndirectReference GetRefFromDict(PdfDictionary dict, string key) =>
        dict[key] as PdfIndirectReference ?? throw new PdfException($"Dictionary is missing required /{key} indirect reference.");

    /// <summary>
    /// Clears the object cache and marks the instance as disposed.
    /// The source byte buffer is not freed here; its lifetime is managed externally.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cache.Clear();
        _objectStreamCache.Clear();
    }
}
