namespace Unchained.Pdf.Models;

/// <summary>
///     Controls how a PDF viewer displays the document window (ISO 32000-1 §12.2).
///     Maps to the document catalog's <c>/ViewerPreferences</c> dictionary.
/// </summary>
/// <param name="HideToolbar">Hide the viewer toolbar when the document is open.</param>
/// <param name="HideMenubar">Hide the viewer menu bar.</param>
/// <param name="HideWindowUI">Hide all UI chrome except the document area.</param>
/// <param name="FitWindow">Resize the document window to fit the first page.</param>
/// <param name="CenterWindow">Centre the document window on screen when opened.</param>
/// <param name="DisplayDocTitle">Show the document title in the window title bar.</param>
/// <param name="Direction">Text reading direction; affects column order in multi-column layouts.</param>
/// <param name="Duplex">Default duplex printing mode.</param>
/// <param name="NonFullScreenPageMode">Page mode to use when exiting full-screen; ignored when not in full-screen.</param>
public sealed record ViewerPreferences(
    bool HideToolbar = false,
    bool HideMenubar = false,
    // ReSharper disable once InconsistentNaming
    bool HideWindowUI = false,
    bool FitWindow = false,
    bool CenterWindow = false,
    bool DisplayDocTitle = false,
    ReadingDirection Direction = ReadingDirection.LeftToRight,
    DuplexMode Duplex = DuplexMode.None,
    PageMode NonFullScreenPageMode = PageMode.UseNone
)
{
    /// <summary>Default viewer preferences: no UI hidden, no overrides.</summary>
    public static readonly ViewerPreferences Default = new();
}

/// <summary>Initial page layout used by the PDF viewer (ISO 32000-1 Table 28).</summary>
public enum PageLayout
{
    /// <summary>Not specified — viewer chooses.</summary>
    Default,
    /// <summary>Display one page at a time.</summary>
    SinglePage,
    /// <summary>Display pages in a single continuous scrollable column.</summary>
    OneColumn,
    /// <summary>Two-column layout, odd pages on the left.</summary>
    TwoColumnLeft,
    /// <summary>Two-column layout, odd pages on the right.</summary>
    TwoColumnRight,
    /// <summary>Two pages visible simultaneously, odd pages on the left.</summary>
    TwoPageLeft,
    /// <summary>Two pages visible simultaneously, odd pages on the right.</summary>
    TwoPageRight
}

/// <summary>Initial page mode used by the PDF viewer (ISO 32000-1 Table 28).</summary>
public enum PageMode
{
    /// <summary>Not specified — typically <see cref="UseNone" />.</summary>
    Default,
    /// <summary>No side panel open.</summary>
    UseNone,
    /// <summary>Document outline (bookmarks) panel open.</summary>
    UseOutlines,
    /// <summary>Page thumbnails panel open.</summary>
    UseThumbs,
    /// <summary>Open in full-screen mode.</summary>
    FullScreen,
    /// <summary>Optional content (layers) panel open.</summary>
    // ReSharper disable once InconsistentNaming
    UseOC,
    /// <summary>Attachments panel open.</summary>
    UseAttachments
}

/// <summary>Text reading direction for viewer layout.</summary>
public enum ReadingDirection
{
    /// <summary>Left-to-right (default for Western scripts).</summary>
    LeftToRight,
    /// <summary>Right-to-left (Arabic, Hebrew, etc.).</summary>
    RightToLeft
}

/// <summary>Default duplex printing mode.</summary>
public enum DuplexMode
{
    /// <summary>Not specified.</summary>
    None,
    /// <summary>Single-sided printing.</summary>
    Simplex,
    /// <summary>Double-sided, flip on the short edge.</summary>
    DuplexFlipShortEdge,
    /// <summary>Double-sided, flip on the long edge.</summary>
    DuplexFlipLongEdge
}
