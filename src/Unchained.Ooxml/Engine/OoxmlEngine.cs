using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

namespace Unchained.Ooxml.Engine;

/// <summary>
///     Format-neutral facade over the Open XML SDK that the shared core exposes to the
///     per-format packages (Pptx / Docx / Xlsx). It owns an <see cref="OpenXmlPackage" /> opened
///     or created from a stream, and surfaces the underlying typed package plus generic part /
///     relationship access so callers do not depend on SDK <c>Open</c> overloads directly.
/// </summary>
/// <remarks>
///     This is the Phase 1 engine seam. It is additive — the legacy custom OPC layer
///     (<c>Unchained.Ooxml.Opc</c>) still exists in parallel until format parsers are migrated
///     onto this engine. The facade reads and writes; mutations to the typed tree are flushed by
///     <see cref="Save" /> / <see cref="SaveTo" />.
/// </remarks>
public sealed class OoxmlEngine : IDisposable
{
    private readonly MemoryStream _stream;
    private bool _disposed;

    private OoxmlEngine(OpenXmlPackage package, MemoryStream stream, OoxmlFormat format)
    {
        Package = package;
        Format = format;
        _stream = stream;
    }

    /// <summary>The underlying Open XML SDK package (a presentation, document, or workbook).</summary>
    public OpenXmlPackage Package { get; }

    /// <summary>Which OOXML document family this package is.</summary>
    public OoxmlFormat Format { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Package.Dispose();
        _stream.Dispose();
    }

    // ── Open ────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Opens an existing OOXML package from raw bytes for read/write. The format is detected
    ///     from the package's main part content type.
    /// </summary>
    /// <param name="data">The raw <c>.pptx</c> / <c>.docx</c> / <c>.xlsx</c> bytes.</param>
    /// <param name="editable">When <see langword="true" /> the package is opened for modification.</param>
    public static OoxmlEngine Open(ReadOnlySpan<byte> data, bool editable = true)
    {
        // Copy into an owned, growable stream so the SDK can read (and, when editable, write).
        var stream = new MemoryStream();
        stream.Write(data);
        stream.Position = 0;

        var format = DetectFormat(stream);
        stream.Position = 0;

        OpenXmlPackage package = format switch
        {
            OoxmlFormat.Presentation => PresentationDocument.Open(stream, editable),
            OoxmlFormat.Wordprocessing => WordprocessingDocument.Open(stream, editable),
            OoxmlFormat.Spreadsheet => SpreadsheetDocument.Open(stream, editable),
            _ => throw new OoXmlException($"Unsupported OOXML format: {format}.")
        };

        return new OoxmlEngine(package, stream, format);
    }

    // ── Create ──────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a new, empty OOXML package of the given <paramref name="format" /> in memory.
    ///     The caller is responsible for adding the main part and content via the SDK tree.
    /// </summary>
    public static OoxmlEngine Create(OoxmlFormat format)
    {
        var stream = new MemoryStream();

        OpenXmlPackage package = format switch
        {
            OoxmlFormat.Presentation =>
                PresentationDocument.Create(stream, PresentationDocumentType.Presentation),
            OoxmlFormat.Wordprocessing =>
                WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document),
            OoxmlFormat.Spreadsheet =>
                SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook),
            _ => throw new OoXmlException($"Unsupported OOXML format: {format}.")
        };

        return new OoxmlEngine(package, stream, format);
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Flushes pending changes to the in-memory package and returns the current bytes.
    ///     The engine remains usable afterwards.
    /// </summary>
    public byte[] Save()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Package.FileOpenAccess == FileAccess.Read)
        {
            throw new InvalidOperationException(
                "The package was opened read-only (editable: false) and cannot be saved. " +
                "Reopen with editable: true to persist changes.");
        }

        // Package.Save() flushes the SDK's part tree to the underlying stream without closing it,
        // so the engine remains usable afterwards.
        Package.Save();
        _stream.Flush();
        return _stream.ToArray();
    }

    /// <summary>Flushes pending changes and writes the package bytes to <paramref name="destination" />.</summary>
    public void SaveTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var bytes = Save();
        destination.Write(bytes, 0, bytes.Length);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static OoxmlFormat DetectFormat(Stream stream)
    {
        // The OPC content-type map names the main part's type. Inspect [Content_Types].xml
        // rather than trusting a file extension we may not have.
        using var package = System.IO.Packaging.Package.Open(
            stream,
            FileMode.Open,
            FileAccess.Read);

        foreach (var ct in package.GetParts().Select(static part => part.ContentType))
        {
            if (ct.Contains("presentationml.presentation.main", StringComparison.OrdinalIgnoreCase)
                || ct.Contains("presentationml.slideshow.main", StringComparison.OrdinalIgnoreCase))
                return OoxmlFormat.Presentation;
            if (ct.Contains("wordprocessingml.document.main", StringComparison.OrdinalIgnoreCase))
                return OoxmlFormat.Wordprocessing;
            if (ct.Contains("spreadsheetml.sheet.main", StringComparison.OrdinalIgnoreCase))
                return OoxmlFormat.Spreadsheet;
        }

        throw new OoXmlException(
            "Could not determine OOXML format: no presentation, document, or workbook main part found.");
    }
}
