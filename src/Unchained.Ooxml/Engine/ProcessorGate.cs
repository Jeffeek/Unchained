namespace Unchained.Ooxml.Engine;

/// <summary>
///     Thread-safe gate that limits concurrent CPU-bound work (parse/save/export).
///     Shared by PresentationProcessor, SpreadsheetProcessor, etc.
/// </summary>
internal sealed class ProcessorGate(int concurrency) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(concurrency, concurrency);
    private volatile bool _disposed;

    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            if (!_disposed)
                _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _semaphore.Dispose();
    }
}
