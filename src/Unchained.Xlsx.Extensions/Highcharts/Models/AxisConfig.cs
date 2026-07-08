namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Axis configuration (reusable for X and Y axes). Inherits the shared axis styling and
///     scaling properties from <see cref="AxisConfigBase" />.
/// </summary>
public class AxisConfig : AxisConfigBase
{
    /// <summary>Category labels for category-type axes.</summary>
    public List<string>? Categories { get; set; }

    /// <summary>Date/time label formats for datetime axes.</summary>
    public DateTimeLabelFormats? DateTimeLabelFormats { get; set; }
}
