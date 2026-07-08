namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Animation configuration.
/// </summary>
public class AnimationConfig
{
    /// <summary>Duration in milliseconds.</summary>
    public double Duration { get; set; }

    /// <summary>Easing function name (e.g., "easeOutBack", "easeInOut").</summary>
    public string? Easing { get; set; }
}
