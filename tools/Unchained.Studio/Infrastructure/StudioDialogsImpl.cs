using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Unchained.Studio.Infrastructure;

/// <inheritdoc cref="IStudioDialogs" />
public sealed class StudioDialogs(IDialogService dialogService) : IStudioDialogs
{
    public async Task<TResult?> ShowAsync<TDialog, TResult>(
        string title,
        Action<DialogParameters>? configure = null,
        MaxWidth maxWidth = MaxWidth.Small,
        bool fullWidth = true,
        bool closeButton = true
    )
        where TDialog : ComponentBase
    {
        var parameters = new DialogParameters();
        configure?.Invoke(parameters);

        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = maxWidth,
            FullWidth = fullWidth,
            CloseButton = closeButton
        };

        var dialog = await dialogService.ShowAsync<TDialog>(title, parameters, options);
        var result = await dialog.Result;
        return result is null || result.Canceled ? default : (TResult)result.Data!;
    }

    public async Task ShowVoidAsync<TDialog>(
        string title,
        Action<DialogParameters>? configure = null,
        MaxWidth maxWidth = MaxWidth.Small,
        bool fullWidth = true,
        bool closeButton = true
    )
        where TDialog : ComponentBase
    {
        var parameters = new DialogParameters();
        configure?.Invoke(parameters);

        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = maxWidth,
            FullWidth = fullWidth,
            CloseButton = closeButton
        };

        await dialogService.ShowAsync<TDialog>(title, parameters, options);
    }

    public Task<bool?> ShowMessageBoxAsync(
        string title,
        string message,
        string confirmText = "OK",
        string? cancelText = null,
        bool closeOnEscapeKey = true
    )
    {
        var options = new DialogOptions { CloseOnEscapeKey = closeOnEscapeKey };
        return dialogService.ShowMessageBoxAsync(
            title,
            message,
            confirmText,
            cancelText,
            "",
            options
        );
    }
}
