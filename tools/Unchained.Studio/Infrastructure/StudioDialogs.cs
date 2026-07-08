using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Unchained.Studio.Infrastructure;

/// <summary>
///     Typed dialog launcher. Replaces manual DialogParameters + DialogOptions construction
///     across all tabs.
///     Usage:
///     var result = await Dialogs.ShowAsync&lt;SplitDialog, IReadOnlyList&lt;PageRange&gt;&gt;(
///     "Split",
///     p =&gt; p[nameof(SplitDialog.PageCount)] = Session.Document.PageCount,
///     DialogSize.Small);
///     if (result is null || result.Canceled) return;
///     var ranges = result.Data;
/// </summary>
public interface IStudioDialogs
{
    /// <summary>Shows a dialog and returns the typed result payload.</summary>
    Task<TResult?> ShowAsync<TDialog, TResult>(
        string title,
        Action<DialogParameters>? configure = null,
        MaxWidth maxWidth = MaxWidth.Small,
        bool fullWidth = true,
        bool closeButton = true
    )
        where TDialog : ComponentBase;

    /// <summary>Shows a dialog with no return value (void callback). Result is discarded.</summary>
    Task ShowVoidAsync<TDialog>(
        string title,
        Action<DialogParameters>? configure = null,
        MaxWidth maxWidth = MaxWidth.Small,
        bool fullWidth = true,
        bool closeButton = true
    )
        where TDialog : ComponentBase;

    /// <summary>Shows a confirmation message box.</summary>
    Task<bool?> ShowMessageBoxAsync(
        string title,
        string message,
        string confirmText = "OK",
        string? cancelText = null,
        bool closeOnEscapeKey = true
    );
}
