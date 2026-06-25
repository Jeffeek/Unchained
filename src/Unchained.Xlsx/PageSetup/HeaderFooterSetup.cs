namespace Unchained.Xlsx.PageSetup;

/// <summary>
///     The header/footer of a worksheet, expressed in raw Excel format codes (e.g. <c>&amp;C&amp;P</c>
///     for a centred page number). See <see cref="HeaderFooterCodes" /> for the common code constants.
/// </summary>
public sealed class HeaderFooterSetup
{
    /// <summary>Whether the first page uses a different header/footer.</summary>
    public bool DifferentFirstPage { get; set; }

    /// <summary>Whether odd and even pages use different headers/footers.</summary>
    public bool DifferentOddEven { get; set; }

    /// <summary>Whether the header/footer scales with the document.</summary>
    public bool ScaleWithDocument { get; set; } = true;

    /// <summary>Whether the header/footer aligns with the page margins.</summary>
    public bool AlignWithMargins { get; set; } = true;

    /// <summary>The odd-page (default) header.</summary>
    public string? OddHeader { get; set; }

    /// <summary>The odd-page (default) footer.</summary>
    public string? OddFooter { get; set; }

    /// <summary>The even-page header (used when <see cref="DifferentOddEven" />).</summary>
    public string? EvenHeader { get; set; }

    /// <summary>The even-page footer (used when <see cref="DifferentOddEven" />).</summary>
    public string? EvenFooter { get; set; }

    /// <summary>The first-page header (used when <see cref="DifferentFirstPage" />).</summary>
    public string? FirstPageHeader { get; set; }

    /// <summary>The first-page footer (used when <see cref="DifferentFirstPage" />).</summary>
    public string? FirstPageFooter { get; set; }
}

/// <summary>Common Excel header/footer format-code constants.</summary>
public static class HeaderFooterCodes
{
    /// <summary>Left section.</summary>
    public const string Left = "&L";

    /// <summary>Centre section.</summary>
    public const string Center = "&C";

    /// <summary>Right section.</summary>
    public const string Right = "&R";

    /// <summary>The current page number.</summary>
    public const string PageNumber = "&P";

    /// <summary>The total number of pages.</summary>
    public const string TotalPages = "&N";

    /// <summary>The current date.</summary>
    public const string Date = "&D";

    /// <summary>The current time.</summary>
    public const string Time = "&T";

    /// <summary>The workbook file name.</summary>
    public const string FileName = "&F";

    /// <summary>The sheet (tab) name.</summary>
    public const string SheetName = "&A";

    /// <summary>Toggle bold.</summary>
    public const string Bold = "&B";

    /// <summary>Toggle italic.</summary>
    public const string Italic = "&I";
}
