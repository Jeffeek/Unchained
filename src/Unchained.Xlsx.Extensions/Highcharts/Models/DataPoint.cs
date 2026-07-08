using System.Text.Json;

namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     A single data point in a chart series. Serializes as a scalar number when only <see cref="Y" /> is set, and as
///     an object when additional properties are populated.
/// </summary>
public class DataPoint
{
    /// <summary>The Y value of the data point.</summary>
    public double? Y { get; set; }

    /// <summary>Explicit color as <c>#RRGGBB</c>. Overrides series-level color.</summary>
    public string? Color { get; set; }

    /// <summary>For pie/doughnut: whether this slice is pulled out (exploded).</summary>
    public bool Sliced { get; set; }

    /// <summary>For pie/doughnut: whether this slice is selected on render.</summary>
    public bool Selected { get; set; }

    /// <summary>Display name for this data point.</summary>
    public string? Name { get; set; }

    /// <summary>X value for scatter charts.</summary>
    public double? X { get; set; }

    /// <summary>Z (size) value for bubble charts.</summary>
    public double? Size { get; set; }

    /// <summary>Returns <see langword="true" /> if this point has properties beyond <see cref="Y" />.</summary>
    public bool IsComplex => Color is not null || Sliced || Selected || Name is not null || X is not null || Size is not null;

    /// <summary>Serializes this data point to JSON, as a scalar when simple or an object when complex.</summary>
    public string ToJson() => !IsComplex && Y.HasValue
        ? JsonSerializer.Serialize(Y.Value)
        : JsonSerializer.Serialize(this, HighchartsConverter.JsonOptions);
}
