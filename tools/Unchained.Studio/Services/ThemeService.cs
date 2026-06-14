using Microsoft.JSInterop;
using MudBlazor;

namespace Unchained.Studio.Services;

/// <summary>
///     Holds the Studio's theme state (light/dark) for one Blazor circuit and persists the user's
///     choice to <c>localStorage</c>. The MudBlazor <see cref="MudTheme" /> is shared; only the
///     dark-mode flag toggles. Call <see cref="InitializeAsync" /> once after first render to load the
///     saved preference.
/// </summary>
public sealed class ThemeService(IJSRuntime js)
{
    private const string StorageKey = "unchained-studio.dark-mode";

    /// <summary><see langword="true" /> when dark mode is active.</summary>
    public bool IsDarkMode { get; private set; }

    /// <summary>The single theme definition (light + dark palettes).</summary>
    public MudTheme Theme { get; } = BuildTheme();

    /// <summary>Raised when the theme changes so the layout can re-render.</summary>
    public event Action? Changed;

    /// <summary>Loads the persisted dark-mode preference. Safe to call once after first render.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("unchainedStudio.getLocalStorage", StorageKey).ConfigureAwait(false);
            if (stored is "true" or "false")
            {
                IsDarkMode = stored == "true";
                Changed?.Invoke();
            }
        }
        catch
        {
            // JS interop unavailable (prerender) — keep the default (light).
        }
    }

    /// <summary>Toggles dark mode and persists the new value.</summary>
    public async Task ToggleAsync()
    {
        IsDarkMode = !IsDarkMode;
        Changed?.Invoke();
        try
        {
            await js.InvokeVoidAsync(
                    "unchainedStudio.setLocalStorage",
                    StorageKey,
                    IsDarkMode ? "true" : "false"
                )
                .ConfigureAwait(false);
        }
        catch
        {
            // Persistence is best-effort.
        }
    }

    private static MudTheme BuildTheme() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#5C6BC0",
            Secondary = "#26A69A",
            AppbarBackground = "#5C6BC0",
            Background = "#fafafa",
            Surface = "#ffffff",
            DrawerBackground = "#ffffff"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#7C8AE0",
            Secondary = "#4DB6AC",
            AppbarBackground = "#1f1f2e",
            Background = "#15151f",
            Surface = "#1e1e2a",
            DrawerBackground = "#1e1e2a",
            TextPrimary = "#e6e6ee",
            TextSecondary = "#a0a0b5"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        }
    };
}
