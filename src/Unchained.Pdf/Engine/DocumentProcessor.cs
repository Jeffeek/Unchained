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
    public async Task<IPdfDocument> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        return await ParseAsync(bytes, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IPdfDocument> LoadAsync(string filePath, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(password);
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        return await ParseAsync(bytes, password, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IPdfDocument> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return await ParseAsync(ms.ToArray(), null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IPdfDocument> LoadAsync(Stream stream, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(password);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return await ParseAsync(ms.ToArray(), password, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<PdfAValidationResult> ValidatePdfAAsync(byte[] pdfBytes, PdfAProfile profile = PdfAProfile.PdfA1B, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        return Task.Run(() => PdfAValidator.Validate(pdfBytes, profile), cancellationToken);
    }

    /// <inheritdoc />
    public Task<PdfUAValidationResult> ValidatePdfUAAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        return Task.Run(() => PdfUAValidator.Validate(pdfBytes), cancellationToken);
    }

    /// <inheritdoc />
    public async Task ConvertToPdfAAsync(
        IPdfDocument document,
        Stream outputStream,
        PdfAProfile profile = PdfAProfile.PdfA1B,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(outputStream);

        var adapter = CastAdapter(document);
        var converted = await Task.Run(() => new PdfAConverter(profile).Convert(adapter.Core), cancellationToken).ConfigureAwait(false);
        await outputStream.WriteAsync(converted, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ConvertToPdfAAsync(
        IPdfDocument document,
        string filePath,
        PdfAProfile profile = PdfAProfile.PdfA1B,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var adapter = CastAdapter(document);
        var converted = await Task.Run(() => new PdfAConverter(profile).Convert(adapter.Core), cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(filePath, converted, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ConvertToPdfXAsync(
        IPdfDocument document,
        Stream outputStream,
        PdfXProfile profile = PdfXProfile.PdfX1A2001,
        string outputConditionIdentifier = "CGATS TR 001",
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(outputStream);

        var adapter = CastAdapter(document);
        var converted = await Task.Run(() => new PdfXConverter(profile, outputConditionIdentifier).Convert(adapter.Core), cancellationToken).ConfigureAwait(false);
        await outputStream.WriteAsync(converted, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ConvertToPdfXAsync(
        IPdfDocument document,
        string filePath,
        PdfXProfile profile = PdfXProfile.PdfX1A2001,
        string outputConditionIdentifier = "CGATS TR 001",
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var adapter = CastAdapter(document);
        var converted = await Task.Run(() => new PdfXConverter(profile, outputConditionIdentifier).Convert(adapter.Core), cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(filePath, converted, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SignAsync(
        IPdfDocument document,
        X509Certificate2 certificate,
        Stream outputStream,
        SignatureOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(outputStream);

        var adapter = CastAdapter(document);
        var signed = await Task.Run(() => PdfSigner.Sign(adapter.Core, certificate, options ?? SignatureOptions.Default), cancellationToken).ConfigureAwait(false);
        await outputStream.WriteAsync(signed, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SignAsync(
        IPdfDocument document,
        X509Certificate2 certificate,
        string filePath,
        SignatureOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var adapter = CastAdapter(document);
        var signed = await Task.Run(() => PdfSigner.Sign(adapter.Core, certificate, options ?? SignatureOptions.Default), cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(filePath, signed, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PdfSignatureInfo>> VerifySignaturesAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        return Task.Run(
            () =>
            {
                var core = PdfDocumentCore.Parse(pdfBytes);
                return PdfSignatureVerifier.Verify(pdfBytes, core);
            },
            cancellationToken
        );
    }

    /// <inheritdoc />
    public Task ChangePasswordsAsync(
        IPdfDocument document,
        string newUserPassword,
        string newOwnerPassword,
        Stream outputStream,
        PdfEncryptionAlgorithm algorithm = PdfEncryptionAlgorithm.Aes256,
        CancellationToken cancellationToken = default
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
            cancellationToken
        );
    }

    /// <inheritdoc />
    public Task ChangePasswordsAsync(
        IPdfDocument document,
        string newUserPassword,
        string newOwnerPassword,
        string filePath,
        PdfEncryptionAlgorithm algorithm = PdfEncryptionAlgorithm.Aes256,
        CancellationToken cancellationToken = default
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
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        IPdfDocument document,
        string filePath,
        SaveOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var adapter = CastAdapter(document);
        var bytes = await SerializeAsync(adapter, options, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(filePath, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        IPdfDocument document,
        Stream stream,
        SaveOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        var adapter = CastAdapter(document);
        var bytes = await SerializeAsync(adapter, options, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IPdfDocument> LoadFromTxtAsync(string text, TxtLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Task.Run(() => TxtToPdfConverter.Convert(text, options ?? TxtLoadOptions.Default), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IPdfDocument> LoadFromMarkdownAsync(string markdown, MdLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return Task.Run(() => MarkdownToPdfConverter.Convert(markdown, options ?? MdLoadOptions.Default), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IPdfDocument> LoadFromSvgAsync(string svgXml, SvgLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(svgXml);
        return Task.Run(() => SvgToPdfConverter.Convert(svgXml, options ?? SvgLoadOptions.Default), cancellationToken);
    }


    /// <inheritdoc />
    public Task SetMetadataAsync(
        IPdfDocument document,
        DocumentMetadata metadata,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(metadata);

        var adapter = CastAdapter(document);
        return Task.Run(() => MetadataMutator.SetMetadata(adapter, metadata), cancellationToken);
    }

    /// <inheritdoc />
    public Task EmbedStandardFontsAsync(
        IPdfDocument document,
        IReadOnlyDictionary<string, byte[]> fontMap,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fontMap);
        var adapter = CastAdapter(document);
        return Task.Run(() => FontMutator.EmbedStandardFonts(adapter, fontMap), cancellationToken);
    }

    /// <inheritdoc />
    public Task<string> SaveAsXmlAsync(IPdfDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = CastAdapter(document);
        return Task.Run(() => XmlDocumentConverter.SaveXml(adapter.Core, document), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IPdfDocument> LoadFromXmlAsync(string xmlContent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(xmlContent);
        return Task.Run(() => XmlDocumentConverter.LoadFromXml(xmlContent), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IPdfDocument> RepairAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            return await ParseAsync(bytes, null, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Normal parse failed — try byte-scan recovery.
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var core = await Task.Run(() => PdfDocumentCore.Repair(bytes), cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);

        var adapter = CastAdapter(document);
        return Task.Run<PdfObject?>(
            () =>
            {
                try { return adapter.Core.ResolveIndirect(objectNumber).Value; }
                catch { return null; }
            },
            cancellationToken
        );
    }

    /// <inheritdoc />
    public Task TrimCacheAsync(IPdfDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var adapter = CastAdapter(document);
        return Task.Run(() => adapter.Core.TrimCache(), cancellationToken);
    }

    /// <inheritdoc />
    public Task SetOpenActionAsync(
        IPdfDocument document,
        int pageNumber,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber));

        var adapter = CastAdapter(document);
        return Task.Run(() => OpenActionMutator.SetOpenAction(adapter, pageNumber), cancellationToken);
    }

    /// <inheritdoc />
    public Task SetOpenActionAsync(
        IPdfDocument document,
        PdfOpenAction action,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(action);

        var adapter = CastAdapter(document);
        return Task.Run(() => OpenActionMutator.SetOpenActionFromModel(adapter, action), cancellationToken);
    }

    /// <inheritdoc />
    public Task RemovePdfaComplianceAsync(IPdfDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = CastAdapter(document);
        return Task.Run(() => MetadataMutator.RemovePdfaCompliance(adapter), cancellationToken);
    }

    /// <inheritdoc />
    public Task RemovePdfUaComplianceAsync(IPdfDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var adapter = CastAdapter(document);
        return Task.Run(() => MetadataMutator.RemovePdfUaCompliance(adapter), cancellationToken);
    }

    /// <inheritdoc />
    public Task ReplaceFontAsync(
        IPdfDocument document,
        string fontName,
        byte[] newFontBytes,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fontName);
        ArgumentNullException.ThrowIfNull(newFontBytes);

        var adapter = CastAdapter(document);
        return Task.Run(() => FontMutator.ReplaceFont(adapter, fontName, newFontBytes), cancellationToken);
    }

    /// <inheritdoc />
    public Task SubsetFontsAsync(
        IPdfDocument document,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = CastAdapter(document);
        return Task.Run(() => FontMutator.SubsetFonts(adapter), cancellationToken);
    }

    // Builds SaveOptions for a password-change operation.
    // Empty passwords on both sides → remove encryption (SaveOptions.Default).
    private static SaveOptions BuildChangePasswordOptions(string userPwd, string ownerPwd, PdfEncryptionAlgorithm algorithm) =>
        userPwd.Length == 0 && ownerPwd.Length == 0
            ? SaveOptions.Default
            : // strip encryption
            new SaveOptions(
                Encryption: new EncryptionOptions(
                    userPwd,
                    ownerPwd,
                    algorithm
                )
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
                    ct
                )
                .ConfigureAwait(false);
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
            nameof(document)
        );
}
