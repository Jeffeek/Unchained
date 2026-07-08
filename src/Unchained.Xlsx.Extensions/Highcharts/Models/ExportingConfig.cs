namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Exporting configuration (download, CSV, PDF, PNG, SVG).</summary>
public class ExportingConfig
{
    /// <summary>Whether exporting is enabled.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Export filename (without extension).</summary>
    public string? Filename { get; set; }

    /// <summary>Export resolution scale factor.</summary>
    public double? Scale { get; set; }

    /// <summary>Override export width in pixels.</summary>
    public double? Width { get; set; }

    /// <summary>Source chart width for export.</summary>
    public double? SourceWidth { get; set; }

    /// <summary>Source chart height for export.</summary>
    public double? SourceHeight { get; set; }

    /// <summary>CSV export settings.</summary>
    public CsvExportConfig? Csv { get; set; }

    /// <summary>Export button configuration.</summary>
    public ExportingButtonsConfig? Buttons { get; set; }

    /// <summary>Maximum width for print output.</summary>
    public double? PrintMaxWidth { get; set; }
}
