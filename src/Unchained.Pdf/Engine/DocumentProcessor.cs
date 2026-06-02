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
    private int _disposed;

    /// <summary>
    /// Creates a new <see cref="DocumentProcessor"/>.
    /// </summary>
    /// <param name="maxConcurrency">
    /// Maximum number of PDF parse operations that may run concurrently.
    /// Defaults to <see cref="Environment.ProcessorCount"/> when <see langword="null"/>.
    /// </param>
    public DocumentProcessor(int? maxConcurrency = null)
    {
        var concurrency = maxConcurrency ?? Environment.ProcessorCount;
        _gate = new SemaphoreSlim(concurrency, concurrency);
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
                () => PdfDocumentCore.Parse(bytes, password),
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

    /// <summary>
    /// Attempts to load a PDF from <paramref name="bytes"/>, falling back to a
    /// byte-scanning recovery pass if the normal parse fails due to a corrupted
    /// or missing cross-reference table.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
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
}
