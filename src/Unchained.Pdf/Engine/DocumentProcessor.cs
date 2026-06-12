using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Unchained.Drawing.Extensions;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.Converters;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IDocumentProcessor" /> implementation backed by Unchained's
///     own parser (<see cref="Unchained.Pdf.Parsing.PdfParser" />) and writer
///     (<see cref="Unchained.Pdf.Writing.PdfWriter" />).
///     <para>
///         Because the PDF parser is CPU-bound and synchronous, all operations are
///         dispatched to the thread-pool via <see cref="Task.Run(System.Action)" />.
///         A <see cref="SemaphoreSlim" /> limits the number of concurrent parse operations
///         to <see cref="Environment.ProcessorCount" /> (or the value supplied at construction)
///         so that bursts of parallel requests do not over-subscribe the thread-pool.
///     </para>
/// </summary>
public sealed class DocumentProcessor : IDocumentProcessor
{
    private readonly SemaphoreSlim _gate;
    private readonly bool _ignoreCorruptedObjects;
    private int _disposed;

    /// <summary>
    ///     Creates a new <see cref="DocumentProcessor" />.
    /// </summary>
    /// <param name="maxConcurrency">
    ///     Maximum number of PDF parse operations that may run concurrently.
    ///     Defaults to <see cref="Environment.ProcessorCount" /> when <see langword="null" />.
    /// </param>
    /// <param name="ignoreCorruptedObjects">
    ///     When <see langword="true" />, objects that fail to parse are silently replaced with
    ///     <c>null</c> instead of throwing <see cref="Core.PdfException" />.
    ///     Useful for processing real-world PDFs with isolated corrupt objects.
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
        return await ParseAsync(bytes, null, ct).ConfigureAwait(false);
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
        return await ParseAsync(ms.ToArray(), null, ct).ConfigureAwait(false);
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
    public async Task ConvertToPdfXAsync(
        IPdfDocument document,
        Stream outputStream,
        PdfXProfile profile = PdfXProfile.PdfX1A2001,
        string outputConditionIdentifier = "CGATS TR 001",
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(outputStream);

        var adapter = CastAdapter(document);
        var converted = await Task.Run(() => PdfXConverter.Convert(adapter.Core, profile, outputConditionIdentifier), ct).ConfigureAwait(false);
        await outputStream.WriteAsync(converted, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ConvertToPdfXAsync(
        IPdfDocument document,
        string filePath,
        PdfXProfile profile = PdfXProfile.PdfX1A2001,
        string outputConditionIdentifier = "CGATS TR 001",
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var adapter = CastAdapter(document);
        var converted = await Task.Run(() => PdfXConverter.Convert(adapter.Core, profile, outputConditionIdentifier), ct).ConfigureAwait(false);
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
            BuildChangePasswordOptions(newUserPassword, newOwnerPassword, algorithm),
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
            BuildChangePasswordOptions(newUserPassword, newOwnerPassword, algorithm),
            ct
        );
    }

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

    /// <inheritdoc />
    public async Task<IPdfDocument> RepairAsync(byte[] bytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            return await ParseAsync(bytes, null, ct).ConfigureAwait(false);
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
    public Task<PdfObject?> GetObjectByIdAsync(
        IPdfDocument document,
        int objectNumber,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);

        var adapter = CastAdapter(document);
        return Task.Run<PdfObject?>(() =>
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

    /// <inheritdoc />
    public Task SetOpenActionAsync(
        IPdfDocument document,
        PdfOpenAction action,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(action);

        var adapter = CastAdapter(document);
        return Task.Run(() => SetOpenActionFromModel(adapter, action), ct);
    }

    /// <inheritdoc />
    public Task RemovePdfaComplianceAsync(IPdfDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = CastAdapter(document);
        return Task.Run(() => RemovePdfaCompliance(adapter), ct);
    }

    /// <inheritdoc />
    public Task RemovePdfUaComplianceAsync(IPdfDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var adapter = CastAdapter(document);
        return Task.Run(() => RemovePdfUaCompliance(adapter), ct);
    }

    /// <inheritdoc />
    public Task ReplaceFontAsync(
        IPdfDocument document,
        string fontName,
        byte[] newFontBytes,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fontName);
        ArgumentNullException.ThrowIfNull(newFontBytes);

        var adapter = CastAdapter(document);
        return Task.Run(() => ReplaceFont(adapter, fontName, newFontBytes), ct);
    }

    /// <inheritdoc />
    public Task SubsetFontsAsync(
        IPdfDocument document,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = CastAdapter(document);
        return Task.Run(() => SubsetFonts(adapter), ct);
    }

    // Builds SaveOptions for a password-change operation.
    // Empty passwords on both sides → remove encryption (SaveOptions.Default).
    private static SaveOptions BuildChangePasswordOptions(string userPwd, string ownerPwd, PdfEncryptionAlgorithm algorithm) =>
        userPwd.Length == 0 && ownerPwd.Length == 0
            ? SaveOptions.Default
            : // strip encryption
            new SaveOptions(Encryption: new EncryptionOptions(
                userPwd,
                ownerPwd,
                algorithm)
            );

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

    private static void SetMetadata(PdfDocumentAdapter adapter, DocumentMetadata metadata)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var maxObj = existing.Count > 0 ? existing.Max(static o => o.ObjectNumber) : 0;

        // Build the /Info dictionary — merge with existing entries if present.
        var infoEntries = new Dictionary<string, PdfObject>();

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

        var infoDict = new PdfDictionary(infoEntries);

        // Check if an /Info object already exists in the trailer.
        if (adapter.Core.Trailer[PdfName.Info] is PdfIndirectReference existingRef)
        {
            // Replace the existing /Info object in-place.
            var objects = existing
                .Select(o => o.ObjectNumber == existingRef.ObjectNumber
                    ? new PdfIndirectObject(o.ObjectNumber, o.Generation, infoDict)
                    : o)
                .ToList();

            var rootRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
            var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Size.Value] = new PdfInteger(maxObj + 1),
                [PdfName.Root.Value] = rootRef,
                [PdfName.Info.Value] = existingRef
            });

            var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(objects, trailer);
            adapter.ReplaceCore(newDoc.Core);
        }
        else
        {
            // Add a new /Info object.
            var infoObjNum = maxObj + 1;
            var infoRef = new PdfIndirectReference(infoObjNum, 0);
            var objects = existing
                .Append(new PdfIndirectObject(infoObjNum, 0, infoDict))
                .ToList();

            var rootRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
            var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Size.Value] = new PdfInteger(infoObjNum + 1),
                [PdfName.Root.Value] = rootRef,
                [PdfName.Info.Value] = infoRef
            });

            var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(objects, trailer);
            adapter.ReplaceCore(newDoc.Core);
        }

        return;

        // Write only the non-null fields from the supplied metadata.
        static PdfString? ToStr(string? value) =>
            value is null ? null : PdfString.FromLatin1(value);
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
            var dict = obj.Value as PdfDictionary;
            if (dict is null) continue;
            if (dict.GetName("Type") != "Font") continue;

            var baseFont = dict.GetName(PdfName.BaseFont.Value);
            if (baseFont is null) continue;

            // Strip style suffixes to find the base family name.
            var family = NormalizeBaseFont(baseFont);
            if (!fontMap.TryGetValue(family, out var fontBytes) &&
                !fontMap.TryGetValue(baseFont, out fontBytes))
                continue;

            // Check if already embedded.
            var descriptor = dict.Get<PdfDictionary>("FontDescriptor") ??
                             (dict[PdfName.Get("FontDescriptor")] is PdfIndirectReference fd
                                 ? adapter.Core.ResolveIndirect(fd.ObjectNumber).Value as PdfDictionary
                                 : null);

            if (descriptor is not null &&
                (descriptor[PdfName.Get("FontFile")] is not null ||
                 descriptor[PdfName.Get("FontFile2")] is not null ||
                 descriptor[PdfName.Get("FontFile3")] is not null))
                continue; // Already embedded.

            // Build /FontDescriptor with /FontFile2 (TrueType).
            // Read actual metrics from the font file; fall back to conservative defaults.
            var metrics = TrueTypeMetrics.Read(fontBytes) ?? TrueTypeMetrics.HelveticaFallback;

            var maxObj = existing.Max(static o => o.ObjectNumber);
            var fontFileObjNum = ++maxObj;
            var fontFileDict = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Length.Value] = new PdfInteger(fontBytes.Length),
                ["Length1"] = new PdfInteger(fontBytes.Length)
            });
            var fontFileObj = new PdfIndirectObject(fontFileObjNum, 0, new PdfStream(fontFileDict, fontBytes));
            existing.Add(fontFileObj);

            var descObjNum = ++maxObj;
            var descEntries = new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Get("FontDescriptor"),
                ["FontName"] = PdfName.Get(baseFont),
                ["Flags"] = new PdfInteger(32),
                ["FontBBox"] = new PdfArray([
                    new PdfInteger(metrics.XMin), new PdfInteger(metrics.YMin),
                    new PdfInteger(metrics.XMax), new PdfInteger(metrics.YMax)
                ]),
                ["ItalicAngle"] = new PdfInteger(0),
                ["Ascent"] = new PdfInteger(metrics.Ascent),
                ["Descent"] = new PdfInteger(metrics.Descent),
                ["CapHeight"] = new PdfInteger(metrics.CapHeight),
                ["StemV"] = new PdfInteger(metrics.StemV),
                ["FontFile2"] = new PdfIndirectReference(fontFileObjNum, 0)
            };
            existing.Add(new PdfIndirectObject(descObjNum, 0, new PdfDictionary(descEntries)));

            // Update the font dict to include the descriptor.
            var updatedEntries = new Dictionary<string, PdfObject>(dict.Entries)
            {
                ["FontDescriptor"] = new PdfIndirectReference(descObjNum, 0)
            };
            existing[i] = new PdfIndirectObject(obj.ObjectNumber, obj.Generation, new PdfDictionary(updatedEntries));
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

    private static void SetOpenAction(PdfDocumentAdapter adapter, int pageNumber)
    {
        if (pageNumber > adapter.Core.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber),
                $"Page number {pageNumber} exceeds document page count {adapter.Core.PageCount}.");
        }

        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            throw new PdfException("Catalog object not found.");

        var catalogDict = existing[catalogIdx].Value as PdfDictionary ?? throw new PdfException("Catalog is not a dictionary.");

        // Build a GoTo action pointing at the target page.
        var pageRef = FindPageRef(adapter.Core, pageNumber);
        var dest = new PdfArray([
            pageRef,
            PdfName.Get("XYZ"),
            PdfNull.Instance,
            PdfNull.Instance,
            PdfNull.Instance
        ]);
        var action = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("Action"),
            ["S"] = PdfName.Get("GoTo"),
            ["D"] = dest
        });

        var newEntries = new Dictionary<string, PdfObject>(catalogDict.Entries)
        {
            [PdfName.OpenAction.Value] = action
        };
        existing[catalogIdx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(newEntries));

        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    private static void SetOpenActionFromModel(PdfDocumentAdapter adapter, PdfOpenAction action)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            throw new PdfException("Catalog object not found.");

        var catalogDict = existing[catalogIdx].Value as PdfDictionary ?? throw new PdfException("Catalog is not a dictionary.");

        PdfObject openAction = action switch
        {
            PdfOpenAction.GoToAction g => BuildGoToAction(adapter.Core, g.PageNumber),
            PdfOpenAction.UriAction u => BuildUriAction(u.UriString),
            PdfOpenAction.NamedAction n => BuildNamedAction(n.ActionName),
            _ => throw new ArgumentException($"Unknown PdfOpenAction type: {action.GetType().Name}")
        };

        var newEntries = new Dictionary<string, PdfObject>(catalogDict.Entries)
        {
            [PdfName.OpenAction.Value] = openAction
        };
        existing[catalogIdx] = new PdfIndirectObject(
            catalogRef.ObjectNumber,
            0,
            new PdfDictionary(newEntries));
        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    private static PdfDictionary BuildGoToAction(PdfDocumentCore core, int pageNumber)
    {
        if (pageNumber > core.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber),
                $"Page number {pageNumber} exceeds document page count {core.PageCount}.");
        }

        var pageRef = FindPageRef(core, pageNumber);
        var dest = new PdfArray([
            pageRef,
            PdfName.Get("XYZ"),
            PdfNull.Instance, PdfNull.Instance, PdfNull.Instance
        ]);
        return new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("Action"),
            ["S"] = PdfName.Get("GoTo"),
            ["D"] = dest
        });
    }

    private static PdfDictionary BuildUriAction(string uri) =>
        new(new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("Action"),
            ["S"] = PdfName.Get("URI"),
            ["URI"] = PdfString.FromLatin1(uri)
        });

    private static PdfDictionary BuildNamedAction(string name) =>
        new(new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("Action"),
            ["S"] = PdfName.Get("Named"),
            ["N"] = PdfName.Get(name)
        });

    private static PdfIndirectReference FindPageRef(PdfDocumentCore core, int pageNumber)
    {
        var pageDict = core.GetPage(pageNumber);

        foreach (var obj in core.CollectObjects().Where(obj => ReferenceEquals(obj.Value, pageDict)))
            return new PdfIndirectReference(obj.ObjectNumber, obj.Generation);

        throw new PdfException($"Could not find indirect reference for page {pageNumber}.");
    }

    private static void RemovePdfaCompliance(PdfDocumentAdapter adapter)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            return;

        if (existing[catalogIdx].Value is not PdfDictionary catalogDict)
            return;

        // Remove /OutputIntents from catalog.
        var entries = new Dictionary<string, PdfObject>(catalogDict.Entries);
        entries.Remove("OutputIntents");

        // Strip pdfaid properties from XMP if present.
        if (entries.TryGetValue("Metadata", out var metaObj))
        {
            var metaStream = metaObj switch
            {
                PdfStream s => s,
                PdfIndirectReference r =>
                    adapter.Core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
                _ => null
            };
            if (metaStream is not null)
            {
                var xmp = StreamFilters.Decode(metaStream).Span.FromUtf8Span();
                var cleaned = StripXmpNamespace(xmp, "pdfaid");
                var cleanedBytes = cleaned.ToUtf8Span();
                var newStreamDict = new PdfDictionary(
                    new Dictionary<string, PdfObject>(metaStream.Dictionary.Entries)
                    {
                        ["Length"] = new PdfInteger(cleanedBytes.Length)
                    });
                var newStream = new PdfStream(newStreamDict, cleanedBytes.ToArray());

                if (metaObj is PdfIndirectReference metaRef)
                {
                    var metaIdx = existing.FindIndex(o => o.ObjectNumber == metaRef.ObjectNumber);

                    if (metaIdx >= 0)
                        existing[metaIdx] = new PdfIndirectObject(metaRef.ObjectNumber, 0, newStream);
                }
                else
                    entries["Metadata"] = newStream;
            }
        }

        existing[catalogIdx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(entries));
        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    private static void RemovePdfUaCompliance(PdfDocumentAdapter adapter)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            return;

        if (existing[catalogIdx].Value is not PdfDictionary catalogDict)
            return;

        // Remove /MarkInfo from catalog.
        var entries = new Dictionary<string, PdfObject>(catalogDict.Entries);
        entries.Remove(PdfName.MarkInfo.Value);

        // Strip pdfuaid properties from XMP if present.
        if (entries.TryGetValue("Metadata", out var metaObj))
        {
            var metaStream = metaObj switch
            {
                PdfStream s => s,
                PdfIndirectReference r =>
                    adapter.Core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
                _ => null
            };
            if (metaStream is not null)
            {
                var xmp = StreamFilters.Decode(metaStream).Span.FromUtf8Span();
                var cleaned = StripXmpNamespace(xmp, "pdfuaid");
                var cleanedBytes = cleaned.ToUtf8Span();
                var newStreamDict = new PdfDictionary(
                    new Dictionary<string, PdfObject>(metaStream.Dictionary.Entries)
                    {
                        ["Length"] = new PdfInteger(cleanedBytes.Length)
                    });
                var newStream = new PdfStream(newStreamDict, cleanedBytes.ToArray());

                if (metaObj is PdfIndirectReference metaRef)
                {
                    var metaIdx = existing.FindIndex(o => o.ObjectNumber == metaRef.ObjectNumber);
                    if (metaIdx >= 0)
                        existing[metaIdx] = new PdfIndirectObject(metaRef.ObjectNumber, 0, newStream);
                }
                else
                    entries["Metadata"] = newStream;
            }
        }

        existing[catalogIdx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(entries));
        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    /// <summary>
    ///     Removes all XML elements and attributes that belong to
    ///     <paramref name="nsPrefix" /> from an XMP string.
    ///     Uses simple string manipulation to avoid a full XML parse/rewrite cycle.
    /// </summary>
    private static string StripXmpNamespace(string xmp, string nsPrefix)
    {
        // Remove xmlns:nsPrefix="..." declarations.
        var cleaned = Regex.Replace(
            xmp,
            $"""
             \s+xmlns:{nsPrefix}="[^"]*"
             """,
            string.Empty);

        // Remove <nsPrefix:xxx>...</nsPrefix:xxx> elements.
        cleaned = Regex.Replace(
            cleaned,
            $"<{nsPrefix}:[^>]*/?>.*?</{nsPrefix}:[^>]+>",
            string.Empty,
            RegexOptions.Singleline);

        // Remove self-closing <nsPrefix:xxx ... /> elements.
        cleaned = Regex.Replace(
            cleaned,
            $@"<{nsPrefix}:[^/]*/\s*>",
            string.Empty);

        return cleaned;
    }

    private static void ReplaceFont(
        PdfDocumentAdapter adapter,
        string fontName,
        byte[] newFontBytes
    )
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var metrics = TrueTypeMetrics.Read(newFontBytes) ?? TrueTypeMetrics.HelveticaFallback;
        var changed = false;
        var normalised = NormalizeBaseFont(fontName);
        var maxObj = existing.Max(static o => o.ObjectNumber);

        for (var i = 0; i < existing.Count; i++)
        {
            var obj = existing[i];
            var dict = obj.Value as PdfDictionary;
            if (dict is null) continue;
            if (dict.GetName("Type") != "Font") continue;

            var baseFont = dict.GetName(PdfName.BaseFont.Value);
            if (baseFont is null) continue;
            if (!string.Equals(NormalizeBaseFont(baseFont), normalised, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(baseFont, fontName, StringComparison.OrdinalIgnoreCase))
                continue;

            // Build new /FontFile2 stream.
            var fontFileObjNum = ++maxObj;
            var fontFileDict = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Length.Value] = new PdfInteger(newFontBytes.Length),
                ["Length1"] = new PdfInteger(newFontBytes.Length)
            });
            existing.Add(new PdfIndirectObject(
                fontFileObjNum,
                0,
                new PdfStream(fontFileDict, newFontBytes)));

            // Build new /FontDescriptor.
            var descObjNum = ++maxObj;
            var descEntries = new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Get("FontDescriptor"),
                ["FontName"] = PdfName.Get(baseFont),
                ["Flags"] = new PdfInteger(32),
                ["FontBBox"] = new PdfArray([
                    new PdfInteger(metrics.XMin), new PdfInteger(metrics.YMin),
                    new PdfInteger(metrics.XMax), new PdfInteger(metrics.YMax)
                ]),
                ["ItalicAngle"] = new PdfInteger(0),
                ["Ascent"] = new PdfInteger(metrics.Ascent),
                ["Descent"] = new PdfInteger(metrics.Descent),
                ["CapHeight"] = new PdfInteger(metrics.CapHeight),
                ["StemV"] = new PdfInteger(metrics.StemV),
                ["FontFile2"] = new PdfIndirectReference(fontFileObjNum, 0)
            };
            existing.Add(new PdfIndirectObject(
                descObjNum,
                0,
                new PdfDictionary(descEntries)));

            // Update the font dictionary with the new descriptor.
            var updatedEntries = new Dictionary<string, PdfObject>(dict.Entries)
            {
                ["FontDescriptor"] = new PdfIndirectReference(descObjNum, 0)
            };
            existing[i] = new PdfIndirectObject(
                obj.ObjectNumber,
                obj.Generation,
                new PdfDictionary(updatedEntries));
            changed = true;
        }

        if (changed)
            MutationHelper.SerializeAndReplace(adapter, existing);
    }

    private static void SubsetFonts(PdfDocumentAdapter adapter)
    {
        var existing = adapter.Core.CollectObjects().ToList();

        // Step 1: collect used glyph IDs per FontFile object number across all pages.
        // Key = /FontFile2 stream object number, Value = set of used glyph IDs.
        var usedGlyphs = new Dictionary<int, HashSet<int>>();
        CollectUsedGlyphs(adapter, existing, usedGlyphs);
        if (usedGlyphs.Count == 0) return;

        // Step 2: subset each embedded font stream.
        var changed = false;
        for (var i = 0; i < existing.Count; i++)
        {
            var obj = existing[i];
            if (!usedGlyphs.TryGetValue(obj.ObjectNumber, out var glyphs)) continue;
            if (obj.Value is not PdfStream fontStream) continue;

            var originalBytes = fontStream.Data.ToArray();
            if (originalBytes.Length == 0) continue;

            var subsetBytes = TrueTypeSubsetter.Subset(originalBytes, glyphs);
            if (subsetBytes.Length >= originalBytes.Length) continue; // no savings

            // Rebuild the font stream with updated length.
            var newDict = new PdfDictionary(new Dictionary<string, PdfObject>(
                fontStream.Dictionary.Entries)
            {
                [PdfName.Length.Value] = new PdfInteger(subsetBytes.Length),
                ["Length1"] = new PdfInteger(subsetBytes.Length)
            });
            existing[i] = new PdfIndirectObject(
                obj.ObjectNumber,
                obj.Generation,
                new PdfStream(newDict, subsetBytes));
            changed = true;
        }

        if (changed)
            MutationHelper.SerializeAndReplace(adapter, existing);
    }

    // Walks all pages' content streams and collects glyph IDs for each embedded font.
    // usedGlyphs: key = FontFile2 stream object number, value = set of glyph IDs used.
    private static void CollectUsedGlyphs(
        PdfDocumentAdapter adapter,
        List<PdfIndirectObject> objects,
        IDictionary<int, HashSet<int>> usedGlyphs
    )
    {
        // Build a map from FontDescriptor object number → FontFile2 object number.
        var descToFontFile = new Dictionary<int, int>();
        // Build a map from Font dict object number → FontFile2 object number.
        var fontToFontFile = new Dictionary<int, int>();

        foreach (var obj in objects)
        {
            if (obj.Value is not PdfDictionary dict)
                continue;

            var type = dict.GetName("Type");
            switch (type)
            {
                case "FontDescriptor":
                {
                    if (dict[PdfName.Get("FontFile2")] is PdfIndirectReference ff2)
                        descToFontFile[obj.ObjectNumber] = ff2.ObjectNumber;
                    break;
                }
                case "Font":
                {
                    // Link Font → FontDescriptor → FontFile2.
                    if (dict[PdfName.Get("FontDescriptor")] is not PdfIndirectReference fdRef)
                        continue;
                    if (!descToFontFile.TryGetValue(fdRef.ObjectNumber, out var ffNum))
                        continue;

                    fontToFontFile[obj.ObjectNumber] = ffNum;
                    break;
                }
            }
        }

        if (fontToFontFile.Count == 0) return;

        // Walk each page's content operators to collect glyph IDs.
        for (var p = 1; p <= adapter.Core.PageCount; p++)
        {
            var pageDict = adapter.Core.GetPage(p);
            var pageAdapter = new PdfPageAdapter(pageDict, p, adapter.Core);
            var ops = pageAdapter.GetContentOperators();
            var compFonts = pageAdapter.GetCompositeFonts(); // resource name → composite info

            // Walk the font resources to find object numbers for the resource names.
            var resources = pageDict[PdfName.Resources];
            var resDict = resources is PdfIndirectReference rr
                ? adapter.Core.ResolveIndirect(rr.ObjectNumber).Value as PdfDictionary
                : resources as PdfDictionary;
            var fontResDict = resDict?[PdfName.Get("Font")] as PdfDictionary
                              ?? (resDict?[PdfName.Get("Font")] is PdfIndirectReference fr
                                  ? adapter.Core.ResolveIndirect(fr.ObjectNumber).Value as PdfDictionary
                                  : null);
            if (fontResDict is null) continue;

            // Map resource name → FontFile2 object number.
            var resNameToFontFile = new Dictionary<string, int>();
            foreach (var (resName, fontObj) in fontResDict.Entries)
            {
                var fontObjNum = fontObj is PdfIndirectReference fontRef
                    ? fontRef.ObjectNumber
                    : -1;
                if (fontObjNum > 0 && fontToFontFile.TryGetValue(fontObjNum, out var ffNum))
                    resNameToFontFile[resName] = ffNum;
            }

            if (resNameToFontFile.Count == 0) continue;

            // ReSharper disable once GrammarMistakeInComment
            // Walk operators: Tf sets current font, Tj/TJ/'/" show strings.
            var currentFontRes = string.Empty;
            foreach (var op in ops)
            {
                switch (op.Name)
                {
                    case "Tf" when op.Operands.Count >= 1:
                        currentFontRes = (op.Operands[0] as PdfName)?.Value ?? string.Empty;
                    break;
                    case "Tj" when op.Operands.Count >= 1:
                    {
                        if (!resNameToFontFile.TryGetValue(currentFontRes, out var ff))
                            break;

                        if (!usedGlyphs.TryGetValue(ff, out var gs))
                            usedGlyphs[ff] = gs = [];

                        CollectGlyphsFromString(op.Operands[0], currentFontRes, compFonts, gs);
                        break;
                    }
                    case "TJ" when op.Operands is [PdfArray arr, ..]:
                    {
                        if (!resNameToFontFile.TryGetValue(currentFontRes, out var ff))
                            break;

                        if (!usedGlyphs.TryGetValue(ff, out var gs))
                            usedGlyphs[ff] = gs = [];

                        foreach (var elem in arr.Elements)
                            CollectGlyphsFromString(elem, currentFontRes, compFonts, gs);
                        break;
                    }
                }
            }
        }
    }

    // Extracts glyph IDs from a PdfString operand (simple or composite font).
    private static void CollectGlyphsFromString(
        PdfObject obj,
        string fontResName,
        IReadOnlyDictionary<string, CompositeFontInfo> compFonts,
        ISet<int> result
    )
    {
        if (obj is not PdfString ps)
            return;

        var bytes = ps.GetBinaryBytes();

        if (compFonts.TryGetValue(fontResName, out var cfi) && cfi.IdentityEncoding)
        {
            // Type0/CID: 2-byte CID pairs → glyph IDs via CIDToGIDMap.
            var span = bytes.Span;
            for (var i = 0; i + 1 < span.Length; i += 2)
            {
                var cid = (span[i] << 8) | span[i + 1];
                var gid = cfi.IdentityCidToGid
                    ? cid
                    : cfi.CidToGid is not null && cid < cfi.CidToGid.Count
                        ? cfi.CidToGid[cid]
                        : cid;
                result.Add(gid);
            }
        }
        else
        {
            // Simple font: each byte is a character code = approximate glyph ID.
            foreach (var b in bytes.Span)
                result.Add(b);
        }
    }
}
