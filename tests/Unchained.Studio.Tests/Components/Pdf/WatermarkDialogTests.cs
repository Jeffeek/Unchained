using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Unchained.Studio.Components.Pdf;
using Unchained.Studio.Models;

namespace Unchained.Studio.Tests.Components.Pdf;

public sealed class WatermarkDialogTests : MudTestContext
{
    private async Task<IDialogReference> ShowAsync(
        IRenderedComponent<MudDialogProvider> provider,
        int currentPage = 1
    )
    {
        var service = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters { { nameof(WatermarkDialog.CurrentPage), currentPage } };
        IDialogReference reference = null!;
        await provider.InvokeAsync(async () => reference = await service.ShowAsync<WatermarkDialog>("Watermark", parameters));
        return reference;
    }

    [Fact]
    public async Task InitialState_HasDefaults()
    {
        var provider = Render<MudDialogProvider>();
        await ShowAsync(provider);

        var textInput = provider.FindAll("input")[0];
        textInput.GetAttribute("value").ShouldBe("DRAFT");
    }

    [Fact]
    public async Task Confirm_ReturnsWatermarkResult()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, currentPage: 5);

        await provider.FindAll("input")[0].InputAsync("CONFIDENTIAL");

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Apply"));
        await confirmButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeFalse();

        var data = result.Data as WatermarkResult;
        data.ShouldNotBeNull();
        data.Stamp.Text.ShouldBe("CONFIDENTIAL");
        data.Stamp.X.ShouldBe(150);
        data.Stamp.Y.ShouldBe(400);
        data.Stamp.FontSize.ShouldBe(48);
        data.Stamp.RotationDegrees.ShouldBe(45);
        data.Stamp.GrayLevel.ShouldBe(0.75f);
        data.Stamp.IsBackground.ShouldBeFalse();
        data.AllPages.ShouldBeTrue();
    }

    [Fact]
    public async Task ConfirmButton_DisabledWhenTextEmpty()
    {
        var provider = Render<MudDialogProvider>();
        await ShowAsync(provider);

        await provider.FindAll("input")[0].InputAsync(string.Empty);

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Apply"));
        confirmButton.HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task BackgroundCheckbox_TogglesIsBackground()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider);

        var checkbox = provider.Find("input[type='checkbox']");
        await checkbox.ChangeAsync(true);

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Apply"));
        await confirmButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        var data = result.Data as WatermarkResult;
        data!.Stamp.IsBackground.ShouldBeTrue();
    }

    [Fact]
    public async Task AllPagesRadio_DefaultsToTrue()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider);

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Apply"));
        await confirmButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        var data = result.Data as WatermarkResult;
        data!.AllPages.ShouldBeTrue();
    }

    [Fact]
    public async Task CurrentPageRadio_SetsAllPagesFalse()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider, currentPage: 3);

        var radioInputs = provider.FindAll("input[type='radio']");
        await radioInputs[1].ClickAsync();

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Apply"));
        await confirmButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        var data = result.Data as WatermarkResult;
        data!.AllPages.ShouldBeFalse();
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
