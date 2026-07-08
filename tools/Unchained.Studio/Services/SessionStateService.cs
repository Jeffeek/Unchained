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

    internal PdfSessionState? Pdf => _pdf;
    internal PptxSessionState? Pptx => _pptx;
    internal XlsxSessionState? Xlsx => _xlsx;

    public async ValueTask DisposeAsync()
    {
        await CloseSessionAsync(ref _pdf).ConfigureAwait(false);
        await CloseSessionAsync(ref _pptx).ConfigureAwait(false);
        await CloseSessionAsync(ref _xlsx).ConfigureAwait(false);
    }

    private static Task CloseSessionAsync<T>(ref T? state)
        where T : IAsyncDisposable
    {
        var old = Interlocked.Exchange(ref state, default!);
        return old is not null ? old.DisposeAsync().AsTask() : Task.CompletedTask;
    }

    internal async Task LoadPdfAsync(
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

    public Task ClosePdfAsync() => CloseSessionAsync(ref _pdf);

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

    public Task ClosePptxAsync() => CloseSessionAsync(ref _pptx);

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

    public async Task LoadCsvAsync(
        byte[] bytes,
        string fileName,
        CancellationToken ct = default
    )
    {
        if (Interlocked.Exchange(ref _loading, 1) == 1)
            throw new InvalidOperationException("A document load is already in progress.");

        try
        {
            using var inStream = new MemoryStream(bytes);
            using var imported = await xlsxProcessor.LoadFromCsvAsync(inStream, cancellationToken: ct).ConfigureAwait(false);
            using var outStream = new MemoryStream();
            await xlsxProcessor.SaveAsync(imported, outStream, cancellationToken: ct).ConfigureAwait(false);

            var name = Path.ChangeExtension(fileName, ".xlsx");
            var newState = await XlsxSessionState.CreateAsync(xlsxProcessor, outStream.ToArray(), name, ct).ConfigureAwait(false);
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

    public Task CloseXlsxAsync() => CloseSessionAsync(ref _xlsx);
}
