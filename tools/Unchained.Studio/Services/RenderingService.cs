using System.Collections.Concurrent;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Engine;

namespace Unchained.Studio.Services;

/// <summary>
///     Wraps <see cref="PdfRenderer" /> with a simple in-memory cache.
///     Registered as Scoped — one instance per Blazor circuit.
///     Uses <see cref="ConcurrentDictionary" /> instead of MemoryCache to avoid
///     the IDisposable / async-teardown race that MemoryCache introduces.
/// </summary>
public sealed class RenderingService(PdfRenderer renderer)
{
    // Soft cap: when the cache grows beyond this, clear the oldest half.
    // For a dev tool rendering at most a handful of documents this is never reached,
    // but it prevents unbounded growth during a long session.
    private const int MaxEntries = 60;
    // Key: (document identity hash, 1-based page number, dpi)
    // Value: rendered PNG bytes
    private readonly ConcurrentDictionary<(int DocId, int Page, int Dpi), byte[]> _cache = new();

    public async Task<byte[]?> RenderPdfPageAsync(
        IPdfDocument document,
        int pageNumber,
        int dpi = 96,
        CancellationToken ct = default
    )
    {
        var key = (RuntimeHelpers.GetHashCode(document), pageNumber, dpi);

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var page = document.Pages[pageNumber];
            if (page is null)
                return null;

            var options = new RenderOptions(dpi);
            var bytes = await renderer.RenderPageAsync(page, options, ct).ConfigureAwait(false);

            // Evict if the cache grew too large before writing the new entry
            if (_cache.Count >= MaxEntries)
                TrimCache();

            _cache.TryAdd(key, bytes);
            return bytes;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    ///     Removes all cached entries for the given document.
    ///     Call this after a document is mutated (Optimize, Repair, etc.) so the
    ///     new bytes are rendered fresh instead of showing the old cached pages.
    /// </summary>
    public void InvalidateDocument(IPdfDocument document)
    {
        var docId = RuntimeHelpers.GetHashCode(document);
        foreach (var key in _cache.Keys.Where(k => k.DocId == docId).ToList())
            _cache.TryRemove(key, out _);
    }

    public void ClearAll() => _cache.Clear();

    private void TrimCache()
    {
        // Remove approximately half the entries (arbitrary order is fine for a dev cache)
        var toRemove = _cache.Keys.Take(MaxEntries / 2).ToList();
        foreach (var key in toRemove)
            _cache.TryRemove(key, out _);
    }
}

// Bring System.Runtime.CompilerServices.RuntimeHelpers into scope cleanly
file static class RuntimeHelpers
{
    public static int GetHashCode(object obj) =>
        System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
