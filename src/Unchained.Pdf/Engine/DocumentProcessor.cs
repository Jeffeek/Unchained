using System.Security.Cryptography.X509Certificates;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.Converters;
using Unchained.Pdf.Models;

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
        return Task.Run(() => MetadataMutator.SetMetadata(adapter, metadata), ct);
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
        return Task.Run(() => FontMutator.EmbedStandardFonts(adapter, fontMap), ct);
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
        return Task.Run(() => OpenActionMutator.SetOpenAction(adapter, pageNumber), ct);
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
        return Task.Run(() => OpenActionMutator.SetOpenActionFromModel(adapter, action), ct);
    }

    /// <inheritdoc />
    public Task RemovePdfaComplianceAsync(IPdfDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = CastAdapter(document);
        return Task.Run(() => MetadataMutator.RemovePdfaCompliance(adapter), ct);
    }

    /// <inheritdoc />
    public Task RemovePdfUaComplianceAsync(IPdfDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var adapter = CastAdapter(document);
        return Task.Run(() => MetadataMutator.RemovePdfUaCompliance(adapter), ct);
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
        return Task.Run(() => FontMutator.ReplaceFont(adapter, fontName, newFontBytes), ct);
    }

    /// <inheritdoc />
    public Task SubsetFontsAsync(
        IPdfDocument document,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = CastAdapter(document);
        return Task.Run(() => FontMutator.SubsetFonts(adapter), ct);
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
}
