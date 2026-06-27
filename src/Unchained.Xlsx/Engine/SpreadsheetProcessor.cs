using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Security;
using Unchained.Xlsx.Abstractions;
using Unchained.Xlsx.Core;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Parsing;
using Unchained.Xlsx.Worksheets;
using Unchained.Xlsx.Writing;

namespace Unchained.Xlsx.Engine;

/// <summary>
///     The primary entry point for all Unchained.Xlsx operations: load, create, and save
///     workbooks. Thread-safe for concurrent use.
/// </summary>
/// <remarks>
///     <para>
///         I/O-bound methods are <see langword="async" /> and dispatch CPU-bound parse/serialize work
///         to the thread pool via <see cref="Task.Run(Action)" />. A <see cref="SemaphoreSlim" /> caps
///         concurrency at <see cref="Environment.ProcessorCount" /> simultaneous operations to prevent
///         thread-pool saturation on high-throughput ASP.NET or gRPC servers.
///     </para>
///     <para>Dispose the processor when it is no longer needed to release the semaphore.</para>
/// </remarks>
public sealed class SpreadsheetProcessor : ISpreadsheetProcessor
{
    private readonly SemaphoreSlim _gate;
    private int _disposed;

    /// <summary>
    ///     Initialises a new <see cref="SpreadsheetProcessor" />.
    /// </summary>
    /// <param name="maxConcurrency">
    ///     Maximum number of workbooks that may be parsed or serialized simultaneously.
    ///     Defaults to <see cref="Environment.ProcessorCount" />.
    /// </param>
    public SpreadsheetProcessor(int? maxConcurrency = null)
    {
        var limit = maxConcurrency ?? Environment.ProcessorCount;
        if (limit < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Concurrency limit must be at least 1.");

        _gate = new SemaphoreSlim(limit, limit);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _gate.Dispose();
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    /// <summary>Loads a workbook from a file path.</summary>
    /// <exception cref="SpreadsheetEncryptedException">Thrown when the file is encrypted and no valid password is supplied.</exception>
    /// <exception cref="SpreadsheetException">Thrown for any other parse error.</exception>
    public async Task<SpreadsheetDocument> LoadAsync(
        string path,
        OpenOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return await ParseBytesAsync(bytes, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads a workbook from a stream. The stream is read to completion.</summary>
    public async Task<SpreadsheetDocument> LoadAsync(
        Stream stream,
        OpenOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return await ParseBytesAsync(ms.ToArray(), options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads a workbook from a raw byte array.</summary>
    public Task<SpreadsheetDocument> LoadAsync(
        ReadOnlyMemory<byte> data,
        OpenOptions? options = null,
        CancellationToken cancellationToken = default
    ) =>
        ParseBytesAsync(data.ToArray(), options, cancellationToken);

    // ── Create ─────────────────────────────────────────────────────────────────

    /// <summary>Creates a new blank workbook in memory (no I/O) with a single sheet named "Sheet1".</summary>
    public SpreadsheetDocument CreateBlank() => CreateBlank("Sheet1");

    /// <summary>Creates a new blank workbook in memory with a single sheet of the given name.</summary>
    public SpreadsheetDocument CreateBlank(string firstSheetName)
    {
        Worksheet.ValidateSheetName(firstSheetName);

        var document = new SpreadsheetDocument
        {
            Properties =
            {
                Created = DateTimeOffset.UtcNow,
                Modified = DateTimeOffset.UtcNow
            }
        };
        document.Sheets.Add(firstSheetName);
        return document;
    }

    // ── CSV import ──────────────────────────────────────────────────────────────

    /// <summary>Loads a CSV file into a fresh single-sheet workbook.</summary>
    public async Task<SpreadsheetDocument> LoadFromCsvAsync(
        string path,
        CsvLoadOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return ImportCsv(bytes, options);
    }

    /// <summary>Loads CSV data from a stream into a fresh single-sheet workbook.</summary>
    public async Task<SpreadsheetDocument> LoadFromCsvAsync(
        Stream stream,
        CsvLoadOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ImportCsv(ms.ToArray(), options);
    }

    private SpreadsheetDocument ImportCsv(byte[] data, CsvLoadOptions? options) =>
        Import.CsvImporter.Import(data, options ?? CsvLoadOptions.Default, this);

    // ── Save ───────────────────────────────────────────────────────────────────

    /// <summary>Saves the workbook to a file.</summary>
    public async Task SaveAsync(
        SpreadsheetDocument document,
        string path,
        XlsxSaveOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = await SerializeAsync(document, options, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Saves the workbook to a stream.</summary>
    public async Task SaveAsync(
        SpreadsheetDocument document,
        Stream stream,
        XlsxSaveOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        var bytes = await SerializeAsync(document, options, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private async Task<SpreadsheetDocument> ParseBytesAsync(
        byte[] bytes,
        OpenOptions? options,
        CancellationToken cancellationToken
    )
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Parse(bytes, options), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static SpreadsheetDocument Parse(byte[] bytes, OpenOptions? options)
    {
        // Detect and decrypt an OLE compound document (ECMA-376 Part 4 agile encryption).
        var wasEncrypted = false;
        if (AgileEncryption.IsCfb(bytes))
        {
            if (string.IsNullOrEmpty(options?.Password))
                throw new SpreadsheetEncryptedException();

            try
            {
                bytes = AgileEncryption.Decrypt(bytes, options.Password);
                wasEncrypted = true;
            }
            catch (OoXmlEncryptedException ex)
            {
                throw new SpreadsheetEncryptedException(ex.Message);
            }
        }

        OpcPackage package;
        try
        {
            package = OpcPackage.Open(bytes);
        }
        catch (Exception ex)
        {
            throw new SpreadsheetException("Failed to open the workbook package.", ex);
        }

        var document = WorkbookParser.Parse(package);
        document.WasLoadedEncrypted = wasEncrypted;
        return document;
    }

    private async Task<byte[]> SerializeAsync(
        SpreadsheetDocument document,
        XlsxSaveOptions? options,
        CancellationToken cancellationToken
    )
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opts = options ?? XlsxSaveOptions.Default;
            return await Task.Run(
                    () =>
                    {
                        var bytes = WorkbookWriter.Write(document, opts);
                        return string.IsNullOrEmpty(opts.Password)
                            ? bytes
                            : AgileEncryption.Encrypt(bytes, opts.Password);
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
