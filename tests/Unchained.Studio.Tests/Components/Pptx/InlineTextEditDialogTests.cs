using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Unchained.Studio.Components.Pptx;

namespace Unchained.Studio.Tests.Components.Pptx;

public sealed class InlineTextEditDialogTests : MudTestContext
{
    private async Task<IDialogReference> ShowAsync(
        IRenderedComponent<MudDialogProvider> provider,
        string currentText = ""
    )
    {
        var service = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters { { nameof(InlineTextEditDialog.CurrentText), currentText } };
        IDialogReference reference = null!;
        await provider.InvokeAsync(async () => reference = await service.ShowAsync<InlineTextEditDialog>("Edit", parameters));
        return reference;
    }

    [Fact]
    public async Task InitialState_LoadsCurrentText()
    {
        var provider = Render<MudDialogProvider>();
        await ShowAsync(provider, currentText: "Hello World");

        var textarea = provider.Find("textarea");
        textarea.TextContent.ShouldBe("Hello World");
    }

    [Fact]
    public async Task Confirm_ReturnsEditedText()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, currentText: "Initial");

        await provider.Find("textarea").ChangeAsync("Modified Text");

        var okButton = provider.FindAll("button").First(static b => b.TextContent.Contains("OK"));
        await okButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeFalse();
        result.Data.ShouldBe("Modified Text");
    }

    [Fact]
    public async Task Cancel_ReturnsCanceledResult()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, currentText: "Test");

        await provider.Find("textarea").ChangeAsync("Changed");

        var cancelButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Cancel"));
        await cancelButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeTrue();
    }

    [Fact]
    public async Task Confirm_WithEmptyText_Succeeds()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, currentText: "Text");

        await provider.Find("textarea").ChangeAsync(string.Empty);

        var okButton = provider.FindAll("button").First(static b => b.TextContent.Contains("OK"));
        await okButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeFalse();
        result.Data.ShouldBe(string.Empty);
    }
}
