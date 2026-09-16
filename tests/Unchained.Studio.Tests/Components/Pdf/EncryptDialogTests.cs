using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Unchained.Pdf.Models;
using Unchained.Studio.Components.Pdf;
using Unchained.Studio.Models;

namespace Unchained.Studio.Tests.Components.Pdf;

public sealed class EncryptDialogTests : MudTestContext
{
    private async Task<IDialogReference> ShowAsync(IRenderedComponent<MudDialogProvider> provider)
    {
        var service = Services.GetRequiredService<IDialogService>();
        IDialogReference reference = null!;
        await provider.InvokeAsync(async () => reference = await service.ShowAsync<EncryptDialog>("Encrypt"));
        return reference;
    }

    [Fact]
    public async Task InitialState_HasDefaultValues()
    {
        var provider = Render<MudDialogProvider>();
        await ShowAsync(provider);

        var userInput = provider.FindAll("input")[0];
        var ownerInput = provider.FindAll("input")[1];

        userInput.GetAttribute("value").ShouldBe(string.Empty);
        ownerInput.GetAttribute("value").ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Confirm_WithPasswords_ReturnsEncryptResult()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider);

        var inputs = provider.FindAll("input");
        await inputs[0].ChangeAsync("user123");
        await inputs[1].ChangeAsync("owner456");

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Encrypt"));
        await confirmButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeFalse();

        var data = result.Data as EncryptResult;
        data.ShouldNotBeNull();
        data.UserPassword.ShouldBe("user123");
        data.OwnerPassword.ShouldBe("owner456");
        data.Algorithm.ShouldBe(PdfEncryptionAlgorithm.Aes256);
    }

    [Fact]
    public async Task Confirm_WithEmptyPasswords_ReturnsRemovalResult()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider);

        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Remove"));
        await confirmButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeFalse();

        var data = result.Data as EncryptResult;
        data.ShouldNotBeNull();
        data.UserPassword.ShouldBe(string.Empty);
        data.OwnerPassword.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ButtonText_ChangesBasedOnPasswordState()
    {
        var provider = Render<MudDialogProvider>();
        await ShowAsync(provider);

        // Empty passwords → "Remove encryption"
        var button = provider.FindAll("button").First(static b => b.TextContent.Contains("Remove encryption"));
        button.ShouldNotBeNull();

        // Add password → "Encrypt"
        await provider.FindAll("input")[0].ChangeAsync("password");
        var encryptButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Encrypt") && !b.TextContent.Contains("Remove"));
        encryptButton.ShouldNotBeNull();
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

    [Fact]
    public async Task PasswordVisibilityToggle_TogglesInputType()
    {
        var provider = Render<MudDialogProvider>();
        await ShowAsync(provider);

        var userInput = provider.FindAll("input")[0];
        userInput.GetAttribute("type").ShouldBe("password");

        // Click visibility toggle (icon button in adornment)
        var toggleButtons = provider.FindAll("button")
            .Where(static b => b.GetAttribute("aria-label")?.Contains("toggle password visibility", StringComparison.OrdinalIgnoreCase) == true ||
                               (b.QuerySelector("svg") != null && b.TextContent == string.Empty)
            )
            .ToList();

        if (toggleButtons.Count > 0)
        {
            await toggleButtons[0].ClickAsync();
            userInput = provider.FindAll("input")[0];
            userInput.GetAttribute("type").ShouldBe("text");
        }
    }

    [Fact]
    public async Task AlgorithmSelector_DefaultsToAes256()
    {
        var provider = Render<MudDialogProvider>();
        var dialog = await ShowAsync(provider);

        await provider.FindAll("input")[0].ChangeAsync("test");
        var confirmButton = provider.FindAll("button").First(static b => b.TextContent.Contains("Encrypt"));
        await confirmButton.ClickAsync();

        var result = await dialog.Result;
        result.ShouldNotBeNull();
        var data = result.Data as EncryptResult;
        data.ShouldNotBeNull();
        data.Algorithm.ShouldBe(PdfEncryptionAlgorithm.Aes256);
    }
}
