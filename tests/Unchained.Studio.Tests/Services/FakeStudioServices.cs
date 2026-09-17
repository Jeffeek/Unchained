using Microsoft.AspNetCore.Components;
using MudBlazor;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Abstractions;
using Unchained.Studio.Infrastructure;

namespace Unchained.Studio.Tests.Services;

/// <summary>Records dialog invocations and returns pre-seeded results, without any UI.</summary>
internal sealed class FakeStudioDialogs : IStudioDialogs
{
    public List<string> VoidTitles { get; } = [];
    public int MessageBoxCount { get; private set; }

    /// <summary>Result returned by the next <see cref="ShowMessageBoxAsync" /> call.</summary>
    public bool? NextMessageBoxResult { get; set; }

    /// <summary>Result returned by the next <see cref="ShowAsync{TDialog,TResult}" /> call.</summary>
    public object? NextResult { get; set; }

    public Task<TResult?> ShowAsync<TDialog, TResult>(
        string title,
        Action<DialogParameters>? configure = null,
        MaxWidth maxWidth = MaxWidth.Small,
        bool fullWidth = true,
        bool closeButton = true
    )
        where TDialog : ComponentBase
    {
        configure?.Invoke([]);
        return Task.FromResult((TResult?)NextResult);
    }

    public Task ShowVoidAsync<TDialog>(
        string title,
        Action<DialogParameters>? configure = null,
        MaxWidth maxWidth = MaxWidth.Small,
        bool fullWidth = true,
        bool closeButton = true
    )
        where TDialog : ComponentBase
    {
        configure?.Invoke([]);
        VoidTitles.Add(title);
        return Task.CompletedTask;
    }

    public Task<bool?> ShowMessageBoxAsync(
        string title,
        string message,
        string confirmText = "OK",
        string? cancelText = null,
        bool closeOnEscapeKey = true
    )
    {
        MessageBoxCount++;
        return Task.FromResult(NextMessageBoxResult);
    }
}

/// <summary>Records feedback messages so tests can assert on them.</summary>
internal sealed class FakeUserFeedback : IUserFeedback
{
    public List<string> Infos { get; } = [];
    public List<string> Errors { get; } = [];
    public List<string> Successes { get; } = [];

    public void Info(string msg) => Infos.Add(msg);
    public void Error(string msg) => Errors.Add(msg);
    public void Success(string msg) => Successes.Add(msg);
}

/// <summary>No-op renderer; the XLSX code paths under test never rasterize.</summary>
internal sealed class FakePdfRenderer : IPdfRenderer
{
    public Task<byte[]> RenderPageAsync(IPdfPage page, RenderOptions options, CancellationToken ct = default) =>
        Task.FromResult(Array.Empty<byte>());

    public Task<IReadOnlyList<byte[]>> RenderDocumentAsync(IPdfDocument document, RenderOptions options, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<byte[]>>([]);

    public void Dispose() { }
}
