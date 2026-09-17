using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Unchained.Studio.Components.Pdf;

namespace Unchained.Studio.Tests.Components.Pdf;

public sealed class RemovePagesDialogTests : MudTestContext
{
    private async Task<IDialogReference> ShowAsync(
        IRenderedComponent<MudDialogProvider> provider,
        int pageCount = 10
    )
    {
        var service = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters { { nameof(RemovePagesDialog.PageCount), pageCount } };
        IDialogReference reference = null!;
        await provider.InvokeAsync(async () => reference = await service.ShowAsync<RemovePagesDialog>("Remove", parameters));
        return reference;
    }

    [Fact]
    public async Task Confirm_WithValidPages_ReturnsPageList()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, pageCount: 10);

        await provider.Find("input").InputAsync("1,3,5-8");

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Remove"));
        await confirmButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeFalse();

        var pages = result.Data as List<int>;
        pages.ShouldNotBeNull();
        pages.ShouldContain(1);
        pages.ShouldContain(3);
        pages.ShouldContain(5);
        pages.ShouldContain(6);
        pages.ShouldContain(7);
        pages.ShouldContain(8);
    }

    [Fact]
    public async Task Confirm_RemoveAllPages_ShowsError()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, pageCount: 5);

        await provider.Find("input").InputAsync("1-5");

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Remove"));
        await confirmButton.ClickAsync();

        // Dialog should not close
        var resultTask = dialog.Result;
        resultTask.IsCompleted.ShouldBeFalse();

        // Error message
        var markup = provider.Markup;
        markup.ShouldContain("at least one must remain");
    }

    [Fact]
    public async Task Confirm_WithInvalidRange_ShowsError()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, pageCount: 10);

        await provider.Find("input").InputAsync("1-50");

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Remove"));
        await confirmButton.ClickAsync();

        // Dialog should not close
        var resultTask = dialog.Result;
        resultTask.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveButton_DisabledWhenEmpty()
    {
        var provider = Render<MudDialogProvider>();
        await ShowAsync(provider);

        var removeButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Remove"));
        removeButton.HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task Cancel_ReturnsCanceledResult()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider);

        var cancelButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Cancel"));
        await cancelButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeTrue();
    }
}
