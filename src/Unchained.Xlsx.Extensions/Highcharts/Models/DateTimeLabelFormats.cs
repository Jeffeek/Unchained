namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Date/time label formats for datetime axes.</summary>
public class DateTimeLabelFormats
{
    /// <summary>Format for years (e.g. "%b %Y").</summary>
    public string? Year { get; set; }

    /// <summary>Format for months (e.g. "%b '%y").</summary>
    public string? Month { get; set; }

    /// <summary>Format for days (e.g. "%e. %b").</summary>
    public string? Day { get; set; }

    /// <summary>Format for hours (e.g. "%H:%M").</summary>
    public string? Hour { get; set; }

    /// <summary>Format for minutes (e.g. "%H:%M").</summary>
    public string? Minute { get; set; }
}
