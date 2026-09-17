using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Unchained.Studio.Components.Pdf;

namespace Unchained.Studio.Tests.Components.Pdf;

public sealed class SplitDialogTests : MudTestContext
{
    private async Task<IDialogReference> ShowAsync(
        IRenderedComponent<MudDialogProvider> provider,
        int pageCount = 10
    )
    {
        var service = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters { { nameof(SplitDialog.PageCount), pageCount } };
        IDialogReference reference = null!;
        await provider.InvokeAsync(async () => reference = await service.ShowAsync<SplitDialog>("Split", parameters));
        return reference;
    }

    [Fact]
    public async Task Confirm_WithValidRanges_ReturnsRangeList()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, pageCount: 10);

        await provider.Find("input").InputAsync("1-3, 4-6, 7-10");

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Split"));
        await confirmButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeFalse();

        var ranges = result.Data as List<(int Start, int End)>;
        ranges.ShouldNotBeNull();
        ranges.Count.ShouldBe(3);
        ranges[0].ShouldBe((1, 3));
        ranges[1].ShouldBe((4, 6));
        ranges[2].ShouldBe((7, 10));
    }

    [Fact]
    public async Task Confirm_WithSinglePageRange_ReturnsOneRange()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, pageCount: 10);

        await provider.Find("input").InputAsync("5");

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Split"));
        await confirmButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        var ranges = result.Data as List<(int Start, int End)>;
        ranges.ShouldNotBeNull();
        ranges.Count.ShouldBe(1);
        ranges[0].ShouldBe((5, 5));
    }

    [Fact]
    public async Task Confirm_WithInvalidRange_ShowsError()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, pageCount: 10);

        await provider.Find("input").InputAsync("1-50");

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Split"));
        await confirmButton.ClickAsync();

        // Dialog should not close on error
        var resultTask = dialog.Result;
        resultTask.IsCompleted.ShouldBeFalse();

        // Error message should be visible
        var markup = provider.Markup;
        markup.ShouldContain("out of bounds");
    }

    [Fact]
    public async Task Confirm_WithInvalidToken_ShowsError()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, pageCount: 10);

        await provider.Find("input").InputAsync("1-abc");

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Split"));
        await confirmButton.ClickAsync();

        // Dialog should not close on error
        var resultTask = dialog.Result;
        resultTask.IsCompleted.ShouldBeFalse();

        // Error message should be visible
        var markup = provider.Markup;
        markup.ShouldContain("Invalid range");
    }

    [Fact]
    public async Task SplitEachPageButton_GeneratesAllPages()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, pageCount: 5);

        var splitEachButton = provider.FindAll("button").First(static b => b.TextContent.Contains("One file per page"));
        await splitEachButton.ClickAsync();

        var input = provider.Find("input");
        input.GetAttribute("value").ShouldBe("1, 2, 3, 4, 5");

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Split"));
        await confirmButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        var ranges = result.Data as List<(int Start, int End)>;
        ranges.ShouldNotBeNull();
        ranges.Count.ShouldBe(5);
        ranges[0].ShouldBe((1, 1));
        ranges[4].ShouldBe((5, 5));
    }

    [Fact]
    public async Task ConfirmButton_DisabledWhenEmpty()
    {
        var provider = Render<MudDialogProvider>();
        await ShowAsync(provider);

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Split"));
        confirmButton.HasAttribute("disabled").ShouldBeTrue();
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
