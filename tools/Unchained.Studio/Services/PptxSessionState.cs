using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Unchained.Studio.Models;
using Unchained.Studio.Studio.Pptx;

namespace Unchained.Studio.Services;

/// <summary>
///     Holds the currently-loaded PPTX presentation for one Blazor circuit, along with the
///     navigable tree and the raw bytes. Mirrors <see cref="PdfSessionState" />.
/// </summary>
/// <remarks>
///     Loads and saves through the SDK (OpenXML) engine so the package is mutated in place on
///     save: every part the model does not own round-trips verbatim, producing files PowerPoint
///     opens. The generative custom writer drops unmodelled parts and yields invalid output.
/// </remarks>
public sealed class PptxSessionState : DocumentSessionBase<PresentationDocument, PresentationProcessor>
{
    // SDK engine load/save — keeps a live package on the document so SaveAsync re-emits modelled
    // content onto it and preserves everything else (OpenXmlPresentationWriter.CanSave requires it).
    internal static readonly OpenOptions OpenWithEngine = new() { UseOpenXmlEngine = true };
    internal static readonly SaveOptions SaveWithEngine = new() { UseOpenXmlEngine = true };

    private PptxSessionState(
        PresentationProcessor processor,
        PresentationDocument document,
        byte[] bytes,
        string fileName
    ) : base(fileName, bytes, document, processor) { }

    /// <summary>The playboard state for the current presentation.</summary>
    public SlidePlayboardState PlayboardState { get; } = new();

    public int CurrentSlide { get; set; } = 1;

    public static async Task<PptxSessionState> CreateAsync(
        PresentationProcessor processor,
        byte[] bytes,
        string fileName,
        CancellationToken ct = default
    )
    {
        var document = await processor.LoadAsync(bytes, OpenWithEngine, ct).ConfigureAwait(false);
        return new PptxSessionState(processor, document, bytes, fileName);
    }

    protected override async Task<byte[]> SerializeAsync(CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await Processor.SaveAsync(Document, ms, SaveWithEngine, ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    protected override TreeNode BuildTree(PresentationDocument document, string fileName) =>
        PptxTreeBuilder.Build(document, fileName);

    protected override Task<PresentationDocument> ReloadAsync(byte[] bytes, CancellationToken ct = default) =>
        Processor.LoadAsync(bytes, OpenWithEngine, ct);

    protected override ValueTask Dispose() => Document.DisposeAsync();
}
