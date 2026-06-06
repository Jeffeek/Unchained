using Unchained.Pdf.Engine;

namespace Unchained.Studio.Services;

public sealed class SessionStateService(DocumentProcessor pdfProcessor, RenderingService renderingService) : IAsyncDisposable
{
    private PdfSessionState? _pdf;
    private int _loading; // 0 = idle; 1 = loading; Interlocked flag

    public PdfSessionState? Pdf => _pdf;

    public async Task LoadPdfAsync(
        byte[] bytes,
        string fileName,
        CancellationToken ct = default)
    {
        if (System.Threading.Interlocked.Exchange(ref _loading, 1) == 1)
            throw new InvalidOperationException("A document load is already in progress.");

        try
        {
            var newState = await PdfSessionState.CreateAsync(pdfProcessor, bytes, fileName, ct);
            newState.RenderCache = renderingService;
            var old = System.Threading.Interlocked.Exchange(ref _pdf, newState);
            if (old is not null)
                await old.DisposeAsync();
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _loading, 0);
        }
    }

    public async Task LoadPdfAsync(
        byte[] bytes,
        string fileName,
        string password,
        CancellationToken ct = default)
    {
        if (System.Threading.Interlocked.Exchange(ref _loading, 1) == 1)
            throw new InvalidOperationException("A document load is already in progress.");

        try
        {
            var newState = await PdfSessionState.CreateEncryptedAsync(pdfProcessor, bytes, password, fileName, ct);
            newState.RenderCache = renderingService;
            var old = System.Threading.Interlocked.Exchange(ref _pdf, newState);
            if (old is not null)
                await old.DisposeAsync();
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _loading, 0);
        }
    }

    public async Task ClosePdfAsync()
    {
        var old = System.Threading.Interlocked.Exchange(ref _pdf, null);
        if (old is not null)
            await old.DisposeAsync();
    }

    public async ValueTask DisposeAsync() => await ClosePdfAsync();
}
