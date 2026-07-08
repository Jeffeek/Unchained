namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Settings for Highcharts JSON serialisation.
///     Allows the caller to inject or override arbitrary properties via <see cref="AdditionalProperties" />,
///     controlling whether those values can override properties already set by the converter.
/// </summary>
public class HighchartsSettings
{
    /// <summary>
    ///     Properties to merge into the JSON output.
    ///     Keys that conflict with converter-set properties are handled according to <see cref="AllowOverrides"/>.
    /// </summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    /// <summary>
    ///     Whether additional properties may override converter-set values.
    ///     Defaults to <c>true</c> so the caller retains full control.
    /// </summary>
    public bool AllowOverrides { get; set; } = true;
}
