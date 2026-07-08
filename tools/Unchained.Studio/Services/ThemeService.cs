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
    public bool IsDarkMode { get; private set; } = true;

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
            Primary = "#4078A2",
            Secondary = "#4A9174",
            AppbarBackground = "#2D3142",
            Background = "#FAFAF8",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF"
        },
        PaletteDark = new PaletteDark
        {
            // Instrument-panel palette — charcoal surfaces, thin-rule accents
            Primary = "#5c6bc0",
            Secondary = "#78909c",
            Tertiary = "#ce93d8",
            AppbarBackground = "#16181d",
            Background = "#121418",
            Surface = "#16181d",
            DrawerBackground = "#16181d",
            TextPrimary = "#ffffff",
            TextSecondary = "rgba(255, 255, 255, 0.65)",
            TextDisabled = "rgba(255, 255, 255, 0.3)",
            Black = "#000",
            White = "#fff",
            ActionDefault = "rgba(255, 255, 255, 0.7)",
            ActionDisabled = "rgba(255, 255, 255, 0.3)",
            ActionDisabledBackground = "rgba(255, 255, 255, 0.08)",
            HoverOpacity = 0.08,
            Divider = "rgba(255, 255, 255, 0.06)",
            DividerLight = "rgba(255, 255, 255, 0.04)",
            Error = "#ef5350",
            Warning = "#ffa726",
            Info = "#42a5f5",
            Success = "#66bb6a",
            Dark = "#0e1014",
            GrayDefault = "#607d8b",
            GrayLight = "#1a1d26",
            GrayLighter = "#16181d",
            TableLines = "rgba(255, 255, 255, 0.06)",
            LinesDefault = "rgba(255, 255, 255, 0.06)",
            LinesInputs = "rgba(255, 255, 255, 0.08)",
            OverlayLight = "rgba(18, 20, 24, 0.65)",
            OverlayDark = "rgba(0, 0, 0, 0.85)",
            Skeleton = "rgba(255, 255, 255, 0.06)",
            TableStriped = "rgba(255, 255, 255, 0.02)",
            TableHover = "rgba(255, 255, 255, 0.04)"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "0px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "0.82rem",
                FontWeight = "400",
                LetterSpacing = "normal",
                LineHeight = "1.5"
            },
            H1 = new H1Typography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "1.5rem",
                FontWeight = "600",
                LetterSpacing = "0.02em"
            },
            H2 = new H2Typography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "1.25rem",
                FontWeight = "600",
                LetterSpacing = "0.02em"
            },
            H3 = new H3Typography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "1.1rem",
                FontWeight = "600",
                LetterSpacing = "0.02em"
            },
            H4 = new H4Typography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "1rem",
                FontWeight = "600",
                LetterSpacing = "0.02em"
            },
            H5 = new H5Typography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "0.92rem",
                FontWeight = "600",
                LetterSpacing = "0.02em"
            },
            H6 = new H6Typography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "0.85rem",
                FontWeight = "600",
                LetterSpacing = "0.02em"
            },
            Button = new ButtonTypography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "0.78rem",
                FontWeight = "500",
                LetterSpacing = "0.02em",
                TextTransform = "none"
            },
            Subtitle1 = new Subtitle1Typography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "0.85rem",
                FontWeight = "400",
                LetterSpacing = "0.01em"
            },
            Subtitle2 = new Subtitle2Typography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "0.8rem",
                FontWeight = "500",
                LetterSpacing = "0.02em"
            },
            Body1 = new Body1Typography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "0.82rem",
                FontWeight = "400",
                LetterSpacing = "normal"
            },
            Body2 = new Body2Typography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "0.78rem",
                FontWeight = "400",
                LetterSpacing = "normal"
            },
            Caption = new CaptionTypography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "0.7rem",
                FontWeight = "400",
                LetterSpacing = "0.03em"
            },
            Overline = new OverlineTypography
            {
                FontFamily = ["Roboto Mono", "Consolas", "monospace"],
                FontSize = "0.68rem",
                FontWeight = "600",
                LetterSpacing = "0.1em",
                TextTransform = "uppercase"
            }
        }
    };
}
