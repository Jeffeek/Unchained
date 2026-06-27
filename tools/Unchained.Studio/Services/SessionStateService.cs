using Unchained.Pdf.Engine;
using Unchained.Pptx.Engine;
using Unchained.Xlsx.Engine;

namespace Unchained.Studio.Services;

public sealed class SessionStateService(
    DocumentProcessor pdfProcessor,
    PresentationProcessor pptxProcessor,
    SpreadsheetProcessor xlsxProcessor,
    RenderingService renderingService
) : IAsyncDisposable
{
    private int _loading; // 0 = idle; 1 = loading; Interlocked flag
    private PdfSessionState? _pdf;
    private PptxSessionState? _pptx;
    private XlsxSessionState? _xlsx;

    public PdfSessionState? Pdf => _pdf;
    public PptxSessionState? Pptx => _pptx;
    public XlsxSessionState? Xlsx => _xlsx;

    public async ValueTask DisposeAsync()
    {
        await ClosePdfAsync().ConfigureAwait(false);
        await ClosePptxAsync().ConfigureAwait(false);
        await CloseXlsxAsync().ConfigureAwait(false);
    }

    public async Task LoadPdfAsync(
        byte[] bytes,
        string fileName,
        CancellationToken ct = default
    )
    {
        if (Interlocked.Exchange(ref _loading, 1) == 1)
            throw new InvalidOperationException("A document load is already in progress.");

        try
        {
            var newState = await PdfSessionState.CreateAsync(pdfProcessor, bytes, fileName, ct).ConfigureAwait(false);
            newState.RenderCache = renderingService;
            var old = Interlocked.Exchange(ref _pdf, newState);
            if (old is not null)
                await old.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _loading, 0);
        }
    }

    public async Task LoadPdfAsync(
        byte[] bytes,
        string fileName,
        string password,
        CancellationToken ct = default
    )
    {
        if (Interlocked.Exchange(ref _loading, 1) == 1)
            throw new InvalidOperationException("A document load is already in progress.");

        try
        {
            var newState = await PdfSessionState.CreateEncryptedAsync(pdfProcessor, bytes, password, fileName, ct).ConfigureAwait(false);
            newState.RenderCache = renderingService;
            var old = Interlocked.Exchange(ref _pdf, newState);
            if (old is not null)
                await old.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _loading, 0);
        }
    }

    public async Task ClosePdfAsync()
    {
        var old = Interlocked.Exchange(ref _pdf, null);
        if (old is not null)
            await old.DisposeAsync().ConfigureAwait(false);
    }

    public async Task LoadPptxAsync(
        byte[] bytes,
        string fileName,
        CancellationToken ct = default
    )
    {
        if (Interlocked.Exchange(ref _loading, 1) == 1)
            throw new InvalidOperationException("A document load is already in progress.");

        try
        {
            var newState = await PptxSessionState.CreateAsync(pptxProcessor, bytes, fileName, ct).ConfigureAwait(false);
            var old = Interlocked.Exchange(ref _pptx, newState);
            if (old is not null)
                await old.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _loading, 0);
        }
    }

    public async Task ClosePptxAsync()
    {
        var old = Interlocked.Exchange(ref _pptx, null);
        if (old is not null)
            await old.DisposeAsync().ConfigureAwait(false);
    }

    public async Task LoadXlsxAsync(
        byte[] bytes,
        string fileName,
        CancellationToken ct = default
    )
    {
        if (Interlocked.Exchange(ref _loading, 1) == 1)
            throw new InvalidOperationException("A document load is already in progress.");

        try
        {
            var newState = await XlsxSessionState.CreateAsync(xlsxProcessor, bytes, fileName, ct).ConfigureAwait(false);
            var old = Interlocked.Exchange(ref _xlsx, newState);
            if (old is not null)
                await old.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _loading, 0);
        }
    }

    public async Task CreateBlankXlsxAsync(string fileName = "workbook.xlsx")
    {
        var newState = XlsxSessionState.CreateBlank(xlsxProcessor, fileName);
        var old = Interlocked.Exchange(ref _xlsx, newState);
        if (old is not null)
            await old.DisposeAsync().ConfigureAwait(false);
    }

    public async Task CloseXlsxAsync()
    {
        var old = Interlocked.Exchange(ref _xlsx, null);
        if (old is not null)
            await old.DisposeAsync().ConfigureAwait(false);
    }
}
