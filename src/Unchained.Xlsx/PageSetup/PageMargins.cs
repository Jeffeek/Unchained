namespace Unchained.Xlsx.PageSetup;

/// <summary>The print margins of a worksheet, in inches.</summary>
public sealed class PageMargins
{
    /// <summary>Top margin in inches.</summary>
    public double Top { get; set; } = 0.75;

    /// <summary>Bottom margin in inches.</summary>
    public double Bottom { get; set; } = 0.75;

    /// <summary>Left margin in inches.</summary>
    public double Left { get; set; } = 0.7;

    /// <summary>Right margin in inches.</summary>
    public double Right { get; set; } = 0.7;

    /// <summary>Header margin in inches.</summary>
    public double Header { get; set; } = 0.3;

    /// <summary>Footer margin in inches.</summary>
    public double Footer { get; set; } = 0.3;
}
