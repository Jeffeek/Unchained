using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;

namespace Unchained.Pdf.Tests.Helpers;

/// <summary>
/// Base for integration tests that load and save PDF documents.
/// Provides a shared <see cref="DocumentProcessor"/> instance and common helpers
/// so each test class does not repeat the same boilerplate.
/// </summary>
public abstract class PdfTestBase
{
    protected static readonly DocumentProcessor Processor = new();

    /// <summary>Loads a PDF from a raw byte array via a <see cref="MemoryStream"/>.</summary>
    protected static Task<IPdfDocument> LoadAsync(byte[] bytes) =>
        Processor.LoadAsync(new MemoryStream(bytes));

    /// <summary>Loads a PDF from an already-open stream.</summary>
    protected static Task<IPdfDocument> LoadAsync(Stream stream) =>
        Processor.LoadAsync(stream);

    /// <summary>
    /// Tries to load a PDF from a byte array. Returns <see langword="null"/> when parsing
    /// throws <see cref="Unchained.Pdf.Core.PdfException"/> — an expected, documented
    /// outcome for genuinely malformed PDFs.
    /// </summary>
    protected static async Task<IPdfDocument?> TryLoadDocAsync(byte[] bytes)
    {
        try { return await LoadAsync(bytes); }
        catch (Core.PdfException) { return null; }
    }

    /// <summary>
    /// Saves <paramref name="doc"/> to a temporary stream and reloads it, exercising
    /// the full serialize–parse round-trip.
    /// </summary>
    protected static async Task<IPdfDocument> SaveAndReloadAsync(IPdfDocument doc)
    {
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;
        return await Processor.LoadAsync(ms);
    }
}
