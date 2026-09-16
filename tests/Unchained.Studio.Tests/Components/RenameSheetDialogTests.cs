using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Unchained.Studio.Components.Xlsx;

namespace Unchained.Studio.Tests.Components;

/// <summary>
///     Tests for <see cref="RenameSheetDialog" /> driven through a real MudBlazor dialog provider:
///     confirming returns the edited name; cancelling returns a canceled result.
/// </summary>
public sealed class RenameSheetDialogTests : MudTestContext
{
    private async Task<IDialogReference> ShowAsync(IRenderedComponent<MudDialogProvider> provider)
    {
        var service = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters { { nameof(RenameSheetDialog.CurrentName), "Sheet1" } };

        IDialogReference reference = null!;
        await provider.InvokeAsync(async () => reference = await service.ShowAsync<RenameSheetDialog>("Rename", parameters));
        return reference;
    }

    [Fact]
    public async Task Confirm_ReturnsEditedName()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider);

        await provider.Find("input").InputAsync("Renamed");
        await provider.FindAll("button").First(static b => b.TextContent.Contains("Rename")).ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeFalse();
        result.Data.ShouldBe("Renamed");
    }

    [Fact]
    public async Task Cancel_ReturnsCanceledResult()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider);

        await provider.FindAll("button").First(static b => b.TextContent.Contains("Cancel")).ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeTrue();
    }
}
