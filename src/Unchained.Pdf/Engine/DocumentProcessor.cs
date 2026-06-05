using System.Security.Cryptography.X509Certificates;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.Converters;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Default <see cref="IDocumentProcessor"/> implementation backed by Unchained's
/// own parser (<see cref="Unchained.Pdf.Parsing.PdfParser"/>) and writer
/// (<see cref="Unchained.Pdf.Writing.PdfWriter"/>).
/// <para>
/// Because the PDF parser is CPU-bound and synchronous, all operations are
/// dispatched to the thread-pool via <see cref="Task.Run(System.Action)"/>.
/// A <see cref="SemaphoreSlim"/> limits the number of concurrent parse operations
/// to <see cref="Environment.ProcessorCount"/> (or the value supplied at construction)
/// so that bursts of parallel requests do not over-subscribe the thread-pool.
/// </para>
/// </summary>
public sealed class DocumentProcessor : IDocumentProcessor
{
    private readonly SemaphoreSlim _gate;
    private readonly bool _ignoreCorruptedObjects;
    private int _disposed;

    /// <summary>
    /// Creates a new <see cref="DocumentProcessor"/>.
    /// </summary>
    /// <param name="maxConcurrency">
    /// Maximum number of PDF parse operations that may run concurrently.
    /// Defaults to <see cref="Environment.ProcessorCount"/> when <see langword="null"/>.
    /// </param>
    /// <param name="ignoreCorruptedObjects">
    /// When <see langword="true"/>, objects that fail to parse are silently replaced with
    /// <c>null</c> instead of throwing <see cref="Core.PdfException"/>.
    /// Useful for processing real-world PDFs with isolated corrupt objects.
    /// </param>
    public DocumentProcessor(int? maxConcurrency = null, bool ignoreCorruptedObjects = false)
    {
        var concurrency = maxConcurrency ?? Environment.ProcessorCount;
        _gate = new SemaphoreSlim(concurrency, concurrency);
        _ignoreCorruptedObjects = ignoreCorruptedObjects;
    }

    /// <inheritdoc />
    public async Task<IPdfDocument> LoadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var bytes = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
        return await ParseAsync(bytes, password: null, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IPdfDocument> LoadAsync(string filePath, string password, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(password);
        var bytes = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
        return await ParseAsync(bytes, password, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IPdfDocument> LoadAsync(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        return await ParseAsync(ms.ToArray(), password: null, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IPdfDocument> LoadAsync(Stream stream, string password, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(password);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        return await ParseAsync(ms.ToArray(), password, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<PdfAValidationResult> ValidatePdfAAsync(byte[] pdfBytes, PdfAProfile profile = PdfAProfile.PdfA1B, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        return Task.Run(() => PdfAValidator.Validate(pdfBytes, profile), ct);
    }

    /// <inheritdoc />
    public Task<PdfUAValidationResult> ValidatePdfUAAsync(byte[] pdfBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        return Task.Run(() => PdfUAValidator.Validate(pdfBytes), ct);
    }

    /// <inheritdoc />
    public async Task ConvertToPdfAAsync(
        IPdfDocument document,
        Stream outputStream,
        PdfAProfile profile = PdfAProfile.PdfA1B,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(outputStream);

        var adapter = CastAdapter(document);
        var converted = await Task.Run(() => PdfAConverter.Convert(adapter.Core, profile), ct).ConfigureAwait(false);
        await outputStream.WriteAsync(converted, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ConvertToPdfAAsync(
        IPdfDocument document,
        string filePath,
        PdfAProfile profile = PdfAProfile.PdfA1B,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var adapter = CastAdapter(document);
        var converted = await Task.Run(() => PdfAConverter.Convert(adapter.Core, profile), ct).ConfigureAwait(false);
        await File.WriteAllBytesAsync(filePath, converted, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SignAsync(
        IPdfDocument document,
        X509Certificate2 certificate,
        Stream outputStream,
        SignatureOptions? options = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(outputStream);

        var adapter = CastAdapter(document);
        var signed = await Task.Run(() => PdfSigner.Sign(adapter.Core, certificate, options ?? SignatureOptions.Default), ct).ConfigureAwait(false);
        await outputStream.WriteAsync(signed, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SignAsync(
        IPdfDocument document,
        X509Certificate2 certificate,
        string filePath,
        SignatureOptions? options = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var adapter = CastAdapter(document);
        var signed = await Task.Run(() => PdfSigner.Sign(adapter.Core, certificate, options ?? SignatureOptions.Default), ct).ConfigureAwait(false);
        await File.WriteAllBytesAsync(filePath, signed, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PdfSignatureInfo>> VerifySignaturesAsync(byte[] pdfBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        return Task.Run(() =>
            {
                var core = PdfDocumentCore.Parse(pdfBytes);
                return PdfSignatureVerifier.Verify(pdfBytes, core);
            },
            ct);
    }

    /// <inheritdoc />
    public Task ChangePasswordsAsync(
        IPdfDocument document,
        string newUserPassword,
        string newOwnerPassword,
        Stream outputStream,
        PdfEncryptionAlgorithm algorithm = PdfEncryptionAlgorithm.Aes256,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(newUserPassword);
        ArgumentNullException.ThrowIfNull(newOwnerPassword);
        ArgumentNullException.ThrowIfNull(outputStream);

        return SaveAsync(
            document,
            outputStream,
            options: BuildChangePasswordOptions(newUserPassword, newOwnerPassword, algorithm),
            ct
        );
    }

    /// <inheritdoc />
    public Task ChangePasswordsAsync(
        IPdfDocument document,
        string newUserPassword,
        string newOwnerPassword,
        string filePath,
        PdfEncryptionAlgorithm algorithm = PdfEncryptionAlgorithm.Aes256,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(newUserPassword);
        ArgumentNullException.ThrowIfNull(newOwnerPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return SaveAsync(
            document,
            filePath,
            options: BuildChangePasswordOptions(newUserPassword, newOwnerPassword, algorithm),
            ct
        );
    }

    // Builds SaveOptions for a password-change operation.
    // Empty passwords on both sides → remove encryption (SaveOptions.Default).
    private static SaveOptions BuildChangePasswordOptions(string userPwd, string ownerPwd, PdfEncryptionAlgorithm algorithm) =>
        userPwd.Length == 0 && ownerPwd.Length == 0
            ? SaveOptions.Default
            : // strip encryption
            new SaveOptions(Encryption: new EncryptionOptions(
                UserPassword: userPwd,
                OwnerPassword: ownerPwd,
                Algorithm: algorithm)
            );

    /// <inheritdoc />
    public async Task SaveAsync(
        IPdfDocument document,
        string filePath,
        SaveOptions? options = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var adapter = CastAdapter(document);
        var bytes = await SerializeAsync(adapter, options, ct).ConfigureAwait(false);
        await File.WriteAllBytesAsync(filePath, bytes, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        IPdfDocument document,
        Stream stream,
        SaveOptions? options = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        var adapter = CastAdapter(document);
        var bytes = await SerializeAsync(adapter, options, ct).ConfigureAwait(false);
        await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IPdfDocument> LoadFromTxtAsync(string text, TxtLoadOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Task.Run(() => TxtToPdfConverter.Convert(text, options ?? TxtLoadOptions.Default), ct);
    }

    /// <inheritdoc />
    public Task<IPdfDocument> LoadFromMarkdownAsync(string markdown, MdLoadOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return Task.Run(() => MarkdownToPdfConverter.Convert(markdown, options ?? MdLoadOptions.Default), ct);
    }

    /// <inheritdoc />
    public Task<IPdfDocument> LoadFromSvgAsync(string svgXml, SvgLoadOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(svgXml);
        return Task.Run(() => SvgToPdfConverter.Convert(svgXml, options ?? SvgLoadOptions.Default), ct);
    }

    // Acquires a gate slot and parses the byte array on the thread-pool.
    private async Task<IPdfDocument> ParseAsync(byte[] bytes, string? password, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var core = await Task.Run(
                () =>
                {
                    var c = PdfDocumentCore.Parse(bytes, password);
                    c.IgnoreCorruptedObjects = _ignoreCorruptedObjects;
                    return c;
                },
                ct).ConfigureAwait(false);
            return new PdfDocumentAdapter(core);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Task<byte[]> SerializeAsync(PdfDocumentAdapter adapter, SaveOptions? options, CancellationToken ct) =>
        Task.Run(() => adapter.Serialize(options), ct);

    // DocumentProcessor creates PdfDocumentAdapter instances exclusively, so any
    // IPdfDocument argument that is not one indicates a programming error.
    private static PdfDocumentAdapter CastAdapter(IPdfDocument document) =>
        document as PdfDocumentAdapter
        ?? throw new ArgumentException(
            $"Document was not created by this processor. Expected {nameof(PdfDocumentAdapter)}, got {document.GetType().Name}.",
            nameof(document));


    /// <inheritdoc />
    public Task SetMetadataAsync(
        IPdfDocument document,
        DocumentMetadata metadata,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(metadata);

        var adapter = CastAdapter(document);
        return Task.Run(() => SetMetadata(adapter, metadata), ct);
    }

    private static void SetMetadata(PdfDocumentAdapter adapter, DocumentMetadata metadata)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var maxObj = existing.Count > 0 ? existing.Max(static o => o.ObjectNumber) : 0;

        // Build the /Info dictionary — merge with existing entries if present.
        var infoEntries = new Dictionary<string, Core.PdfObject>();

        // Preserve existing /Info entries.
        if (adapter.Core.Info is { } existingInfo)
        {
            foreach (var (key, value) in existingInfo.Entries)
                infoEntries[key] = value;
        }

        if (ToStr(metadata.Title) is { } title) infoEntries["Title"] = title;
        if (ToStr(metadata.Author) is { } author) infoEntries["Author"] = author;
        if (ToStr(metadata.Subject) is { } subject) infoEntries["Subject"] = subject;
        if (ToStr(metadata.Keywords) is { } keywords) infoEntries["Keywords"] = keywords;
        if (ToStr(metadata.Creator) is { } creator) infoEntries["Creator"] = creator;
        if (ToStr(metadata.Producer) is { } producer) infoEntries["Producer"] = producer;

        var infoDict = new Core.PdfDictionary(infoEntries);

        // Check if an /Info object already exists in the trailer.
        if (adapter.Core.Trailer[Core.PdfName.Info] is Core.PdfIndirectReference existingRef)
        {
            // Replace the existing /Info object in-place.
            var objects = existing
                .Select(o => o.ObjectNumber == existingRef.ObjectNumber
                    ? new Core.PdfIndirectObject(o.ObjectNumber, o.Generation, infoDict)
                    : o)
                .ToList();

            var rootRef = adapter.Core.Trailer[Core.PdfName.Root] as Core.PdfIndirectReference ?? throw new Core.PdfException("Trailer missing /Root.");
            var trailer = new Core.PdfDictionary(new Dictionary<string, Core.PdfObject>
            {
                [Core.PdfName.Size.Value] = new Core.PdfInteger(maxObj + 1),
                [Core.PdfName.Root.Value] = rootRef,
                [Core.PdfName.Info.Value] = existingRef
            });

            var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(objects, trailer);
            adapter.ReplaceCore(newDoc.Core);
        }
        else
        {
            // Add a new /Info object.
            var infoObjNum = maxObj + 1;
            var infoRef = new Core.PdfIndirectReference(infoObjNum, 0);
            var objects = existing
                .Append(new Core.PdfIndirectObject(infoObjNum, 0, infoDict))
                .ToList();

            var rootRef = adapter.Core.Trailer[Core.PdfName.Root] as Core.PdfIndirectReference ?? throw new Core.PdfException("Trailer missing /Root.");
            var trailer = new Core.PdfDictionary(new Dictionary<string, Core.PdfObject>
            {
                [Core.PdfName.Size.Value] = new Core.PdfInteger(infoObjNum + 1),
                [Core.PdfName.Root.Value] = rootRef,
                [Core.PdfName.Info.Value] = infoRef
            });

            var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(objects, trailer);
            adapter.ReplaceCore(newDoc.Core);
        }

        return;

        // Write only the non-null fields from the supplied metadata.
        static Core.PdfString? ToStr(string? value) =>
            value is null ? null : Core.PdfString.FromLatin1(value);
    }

    /// <inheritdoc />
    public Task EmbedStandardFontsAsync(
        IPdfDocument document,
        IReadOnlyDictionary<string, byte[]> fontMap,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fontMap);
        var adapter = CastAdapter(document);
        return Task.Run(() => EmbedStandardFonts(adapter, fontMap), ct);
    }

    private static void EmbedStandardFonts(
        PdfDocumentAdapter adapter,
        IReadOnlyDictionary<string, byte[]> fontMap
    )
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var changed = false;

        for (var i = 0; i < existing.Count; i++)
        {
            var obj = existing[i];
            var dict = obj.Value as Core.PdfDictionary;
            if (dict is null) continue;
            if (dict.GetName("Type") != "Font") continue;

            var baseFont = dict.GetName(Core.PdfName.BaseFont.Value);
            if (baseFont is null) continue;

            // Strip style suffixes to find the base family name.
            var family = NormalizeBaseFont(baseFont);
            if (!fontMap.TryGetValue(family, out var fontBytes) &&
                !fontMap.TryGetValue(baseFont, out fontBytes))
                continue;

            // Check if already embedded.
            var descriptor = dict.Get<Core.PdfDictionary>("FontDescriptor") ??
                             (dict[Core.PdfName.Get("FontDescriptor")] is Core.PdfIndirectReference fd
                                 ? adapter.Core.ResolveIndirect(fd.ObjectNumber).Value as Core.PdfDictionary
                                 : null);

            if (descriptor is not null &&
                (descriptor[Core.PdfName.Get("FontFile")] is not null ||
                 descriptor[Core.PdfName.Get("FontFile2")] is not null ||
                 descriptor[Core.PdfName.Get("FontFile3")] is not null))
                continue; // Already embedded.

            // Build /FontDescriptor with /FontFile2 (TrueType).
            var maxObj = existing.Max(static o => o.ObjectNumber);
            var fontFileObjNum = ++maxObj;
            var fontFileDict = new Core.PdfDictionary(new Dictionary<string, Core.PdfObject>
            {
                [Core.PdfName.Length.Value] = new Core.PdfInteger(fontBytes.Length),
                ["Length1"] = new Core.PdfInteger(fontBytes.Length)
            });
            var fontFileObj = new Core.PdfIndirectObject(fontFileObjNum, 0, new Core.PdfStream(fontFileDict, fontBytes));
            existing.Add(fontFileObj);

            var descObjNum = ++maxObj;
            var descEntries = new Dictionary<string, Core.PdfObject>
            {
                ["Type"] = Core.PdfName.Get("FontDescriptor"),
                ["FontName"] = Core.PdfName.Get(baseFont),
                ["Flags"] = new Core.PdfInteger(32),
                ["FontBBox"] = new Core.PdfArray([
                    new Core.PdfInteger(-166), new Core.PdfInteger(-225),
                    new Core.PdfInteger(1000), new Core.PdfInteger(931)
                ]),
                ["ItalicAngle"] = new Core.PdfInteger(0),
                ["Ascent"] = new Core.PdfInteger(800),
                ["Descent"] = new Core.PdfInteger(-200),
                ["CapHeight"] = new Core.PdfInteger(716),
                ["StemV"] = new Core.PdfInteger(80),
                ["FontFile2"] = new Core.PdfIndirectReference(fontFileObjNum, 0)
            };
            existing.Add(new Core.PdfIndirectObject(descObjNum, 0, new Core.PdfDictionary(descEntries)));

            // Update the font dict to include the descriptor.
            var updatedEntries = new Dictionary<string, Core.PdfObject>(dict.Entries)
            {
                ["FontDescriptor"] = new Core.PdfIndirectReference(descObjNum, 0)
            };
            existing[i] = new Core.PdfIndirectObject(obj.ObjectNumber, obj.Generation, new Core.PdfDictionary(updatedEntries));
            changed = true;
        }

        if (changed)
            MutationHelper.SerializeAndReplace(adapter, existing);
    }

    private static string NormalizeBaseFont(string baseFont)
    {
        // "Helvetica-Bold" → "Helvetica", "Times-Roman" → "Times-Roman" (keep as-is for serif)
        var dash = baseFont.IndexOf('-');
        return dash > 0 ? baseFont[..dash] : baseFont;
    }

    /// <inheritdoc />
    public Task<string> SaveAsXmlAsync(IPdfDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = CastAdapter(document);
        return Task.Run(() => XmlDocumentConverter.SaveXml(adapter.Core), ct);
    }

    /// <inheritdoc />
    public Task<IPdfDocument> LoadFromXmlAsync(string xmlContent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(xmlContent);
        return Task.Run(() => XmlDocumentConverter.LoadFromXml(xmlContent), ct);
    }

    /// <inheritdoc/>
    public async Task<IPdfDocument> RepairAsync(byte[] bytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            return await ParseAsync(bytes, password: null, ct).ConfigureAwait(false);
        }
        catch
        {
            // Normal parse failed — try byte-scan recovery.
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var core = await Task.Run(() => PdfDocumentCore.Repair(bytes), ct).ConfigureAwait(false);
                return new PdfDocumentAdapter(core);
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _gate.Dispose();
    }

    // ── M12 — new methods ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<Core.PdfObject?> GetObjectByIdAsync(
        IPdfDocument document,
        int objectNumber,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);

        var adapter = CastAdapter(document);
        return Task.Run<Core.PdfObject?>(() =>
            {
                try { return adapter.Core.ResolveIndirect(objectNumber).Value; }
                catch { return null; }
            },
            ct);
    }

    /// <inheritdoc />
    public Task TrimCacheAsync(IPdfDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var adapter = CastAdapter(document);
        return Task.Run(() => adapter.Core.TrimCache(), ct);
    }

    /// <inheritdoc />
    public Task SetOpenActionAsync(
        IPdfDocument document,
        int pageNumber,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);

        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber));

        var adapter = CastAdapter(document);
        return Task.Run(() => SetOpenAction(adapter, pageNumber), ct);
    }

    private static void SetOpenAction(PdfDocumentAdapter adapter, int pageNumber)
    {
        if (pageNumber > adapter.Core.PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), $"Page number {pageNumber} exceeds document page count {adapter.Core.PageCount}.");

        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[Core.PdfName.Root] as Core.PdfIndirectReference ?? throw new Core.PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            throw new Core.PdfException("Catalog object not found.");

        var catalogDict = existing[catalogIdx].Value as Core.PdfDictionary ?? throw new Core.PdfException("Catalog is not a dictionary.");

        // Build a GoTo action pointing at the target page.
        var pageRef = FindPageRef(adapter.Core, pageNumber);
        var dest = new Core.PdfArray([
            pageRef,
            Core.PdfName.Get("XYZ"),
            Core.PdfNull.Instance,
            Core.PdfNull.Instance,
            Core.PdfNull.Instance
        ]);
        var action = new Core.PdfDictionary(new Dictionary<string, Core.PdfObject>
        {
            ["Type"] = Core.PdfName.Get("Action"),
            ["S"] = Core.PdfName.Get("GoTo"),
            ["D"] = dest
        });

        var newEntries = new Dictionary<string, Core.PdfObject>(catalogDict.Entries)
        {
            [Core.PdfName.OpenAction.Value] = action
        };
        existing[catalogIdx] = new Core.PdfIndirectObject(catalogRef.ObjectNumber, 0, new Core.PdfDictionary(newEntries));

        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    private static Core.PdfIndirectReference FindPageRef(PdfDocumentCore core, int pageNumber)
    {
        var pageDict = core.GetPage(pageNumber);

        foreach (var obj in core.CollectObjects().Where(obj => ReferenceEquals(obj.Value, pageDict)))
            return new Core.PdfIndirectReference(obj.ObjectNumber, obj.Generation);

        throw new Core.PdfException($"Could not find indirect reference for page {pageNumber}.");
    }

    /// <inheritdoc />
    public Task RemovePdfaComplianceAsync(IPdfDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = CastAdapter(document);
        return Task.Run(() => RemovePdfaCompliance(adapter), ct);
    }

    private static void RemovePdfaCompliance(PdfDocumentAdapter adapter)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[Core.PdfName.Root] as Core.PdfIndirectReference ?? throw new Core.PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            return;

        if (existing[catalogIdx].Value is not Core.PdfDictionary catalogDict)
            return;

        // Remove /OutputIntents from catalog.
        var entries = new Dictionary<string, Core.PdfObject>(catalogDict.Entries);
        entries.Remove("OutputIntents");

        // Strip pdfaid properties from XMP if present.
        if (entries.TryGetValue("Metadata", out var metaObj))
        {
            var metaStream = metaObj switch
            {
                Core.PdfStream s => s,
                Core.PdfIndirectReference r =>
                    adapter.Core.ResolveIndirect(r.ObjectNumber).Value as Core.PdfStream,
                _ => null
            };
            if (metaStream is not null)
            {
                var xmp = System.Text.Encoding.UTF8.GetString(Parsing.Filters.StreamFilters.Decode(metaStream).Span);
                var cleaned = StripXmpNamespace(xmp, "pdfaid");
                var cleanedBytes = System.Text.Encoding.UTF8.GetBytes(cleaned);
                var newStreamDict = new Core.PdfDictionary(
                    new Dictionary<string, Core.PdfObject>(metaStream.Dictionary.Entries)
                    {
                        ["Length"] = new Core.PdfInteger(cleanedBytes.Length)
                    });
                var newStream = new Core.PdfStream(newStreamDict, cleanedBytes);

                if (metaObj is Core.PdfIndirectReference metaRef)
                {
                    var metaIdx = existing.FindIndex(o => o.ObjectNumber == metaRef.ObjectNumber);

                    if (metaIdx >= 0)
                        existing[metaIdx] = new Core.PdfIndirectObject(metaRef.ObjectNumber, 0, newStream);
                }
                else
                    entries["Metadata"] = newStream;
            }
        }

        existing[catalogIdx] = new Core.PdfIndirectObject(catalogRef.ObjectNumber, 0, new Core.PdfDictionary(entries));
        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    /// <inheritdoc />
    public Task RemovePdfUaComplianceAsync(IPdfDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = CastAdapter(document);
        return Task.Run(() => RemovePdfUaCompliance(adapter), ct);
    }

    private static void RemovePdfUaCompliance(PdfDocumentAdapter adapter)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[Core.PdfName.Root] as Core.PdfIndirectReference ?? throw new Core.PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            return;

        if (existing[catalogIdx].Value is not Core.PdfDictionary catalogDict)
            return;

        // Remove /MarkInfo from catalog.
        var entries = new Dictionary<string, Core.PdfObject>(catalogDict.Entries);
        entries.Remove(Core.PdfName.MarkInfo.Value);

        // Strip pdfuaid properties from XMP if present.
        if (entries.TryGetValue("Metadata", out var metaObj))
        {
            var metaStream = metaObj switch
            {
                Core.PdfStream s => s,
                Core.PdfIndirectReference r =>
                    adapter.Core.ResolveIndirect(r.ObjectNumber).Value as Core.PdfStream,
                _ => null
            };
            if (metaStream is not null)
            {
                var xmp = System.Text.Encoding.UTF8.GetString(
                    Parsing.Filters.StreamFilters.Decode(metaStream).Span);
                var cleaned = StripXmpNamespace(xmp, "pdfuaid");
                var cleanedBytes = System.Text.Encoding.UTF8.GetBytes(cleaned);
                var newStreamDict = new Core.PdfDictionary(
                    new Dictionary<string, Core.PdfObject>(metaStream.Dictionary.Entries)
                    {
                        ["Length"] = new Core.PdfInteger(cleanedBytes.Length)
                    });
                var newStream = new Core.PdfStream(newStreamDict, cleanedBytes);

                if (metaObj is Core.PdfIndirectReference metaRef)
                {
                    var metaIdx = existing.FindIndex(o => o.ObjectNumber == metaRef.ObjectNumber);
                    if (metaIdx >= 0)
                        existing[metaIdx] = new Core.PdfIndirectObject(metaRef.ObjectNumber, 0, newStream);
                }
                else
                    entries["Metadata"] = newStream;
            }
        }

        existing[catalogIdx] = new Core.PdfIndirectObject(catalogRef.ObjectNumber, 0, new Core.PdfDictionary(entries));
        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    /// <summary>
    /// Removes all XML elements and attributes that belong to
    /// <paramref name="nsPrefix"/> from an XMP string.
    /// Uses simple string manipulation to avoid a full XML parse/rewrite cycle.
    /// </summary>
    private static string StripXmpNamespace(string xmp, string nsPrefix)
    {
        // Remove xmlns:nsPrefix="..." declarations.
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            xmp,
            $"""
             \s+xmlns:{nsPrefix}="[^"]*"
             """,
            string.Empty);

        // Remove <nsPrefix:xxx>...</nsPrefix:xxx> elements.
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            $"<{nsPrefix}:[^>]*/?>.*?</{nsPrefix}:[^>]+>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        // Remove self-closing <nsPrefix:xxx ... /> elements.
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            $@"<{nsPrefix}:[^/]*/\s*>",
            string.Empty);

        return cleaned;
    }
}
