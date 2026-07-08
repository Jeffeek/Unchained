namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>CSV export settings.</summary>
public class CsvExportConfig
{
    /// <summary>Preserve decimal values.</summary>
    public bool? AllowDecimals { get; set; }

    /// <summary>Date format pattern.</summary>
    public string? DateFormat { get; set; }

    /// <summary>Field delimiter character.</summary>
    public string? ItemDelimiter { get; set; }

    /// <summary>Row delimiter character.</summary>
    public string? LineDelimiter { get; set; }

    /// <summary>Number of decimal places.</summary>
    public int? ValueDecimals { get; set; }
}
