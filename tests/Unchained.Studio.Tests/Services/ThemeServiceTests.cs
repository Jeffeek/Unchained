using Microsoft.JSInterop;
using Unchained.Studio.Services;

namespace Unchained.Studio.Tests.Services;

/// <summary>
///     Tests for <see cref="ThemeService" /> — dark-mode default, toggle + persistence, and loading
///     the stored preference (including graceful handling when JS interop is unavailable).
/// </summary>
public sealed class ThemeServiceTests
{
    private sealed class FakeJsRuntime : IJSRuntime
    {
        public string? StoredValue { get; init; }
        public bool ThrowOnInvoke { get; init; }
        public Dictionary<string, string?> Writes { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (ThrowOnInvoke)
                throw new InvalidOperationException("JS interop unavailable");

            switch (identifier)
            {
                case "unchainedStudio.getLocalStorage":
                    return new ValueTask<TValue>((TValue)(object?)StoredValue!);
                case "unchainedStudio.setLocalStorage":
                    Writes[(string)args![0]!] = (string?)args[1];
                break;
            }

            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    [Fact]
    public void DefaultsToDarkModeWithABuiltTheme()
    {
        var service = new ThemeService(new FakeJsRuntime());

        service.IsDarkMode.ShouldBeTrue();
        service.Theme.ShouldNotBeNull();
    }

    [Fact]
    public async Task ToggleAsync_FlipsModeRaisesChangedAndPersists()
    {
        var js = new FakeJsRuntime();
        var service = new ThemeService(js);
        var raised = false;
        service.Changed += () => raised = true;

        await service.ToggleAsync();

        service.IsDarkMode.ShouldBeFalse();
        raised.ShouldBeTrue();
        js.Writes["unchained-studio.dark-mode"].ShouldBe("false");
    }

    [
        Theory,
        InlineData("false", false),
        InlineData("true", true)
    ]
    public async Task InitializeAsync_AppliesStoredPreference(string stored, bool expectedDark)
    {
        var service = new ThemeService(new FakeJsRuntime { StoredValue = stored });
        var raised = false;
        service.Changed += () => raised = true;

        await service.InitializeAsync();

        service.IsDarkMode.ShouldBe(expectedDark);
        raised.ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_IgnoresUnrecognisedStoredValue()
    {
        var service = new ThemeService(new FakeJsRuntime { StoredValue = "garbage" });

        await service.InitializeAsync();

        service.IsDarkMode.ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_SwallowsJsInteropFailure()
    {
        var service = new ThemeService(new FakeJsRuntime { ThrowOnInvoke = true });

        await Should.NotThrowAsync(service.InitializeAsync);

        service.IsDarkMode.ShouldBeTrue();
    }
}
