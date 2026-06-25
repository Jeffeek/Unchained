using Unchained.Xlsx.Models.PageSetup;

namespace Unchained.Xlsx.PageSetup;

/// <summary>Print page setup for a worksheet.</summary>
public sealed class PageSetup
{
    /// <summary>The paper size code (ECMA-376; 9 = A4, 1 = Letter). 0 leaves it unset.</summary>
    public int PaperSize { get; set; }

    /// <summary>Page orientation.</summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Default;

    /// <summary>The print scale (10–400%); 0 means use fit-to-pages.</summary>
    public int Scale { get; set; }

    /// <summary>The number of pages wide to fit to; 0 = automatic.</summary>
    public int FitToWidth { get; set; }

    /// <summary>The number of pages tall to fit to; 0 = automatic.</summary>
    public int FitToHeight { get; set; }

    /// <summary>The first page number; 0 = automatic.</summary>
    public int FirstPageNumber { get; set; }

    /// <summary>Whether to print in black and white.</summary>
    public bool BlackAndWhite { get; set; }

    /// <summary>Whether to print in draft quality.</summary>
    public bool Draft { get; set; }

    /// <summary>Whether to centre the content horizontally on the page.</summary>
    public bool CenterHorizontally { get; set; }

    /// <summary>Whether to centre the content vertically on the page.</summary>
    public bool CenterVertically { get; set; }

    /// <summary>The page traversal order.</summary>
    public PrintOrder PrintOrder { get; set; } = PrintOrder.DownThenOver;
}
