namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Time zone configuration.</summary>
public class TimeConfig
{
    /// <summary>Timezone string (e.g. "America/New_York").</summary>
    public string? Timezone { get; set; }

    /// <summary>Timezone offset in minutes from UTC.</summary>
    public int? TimezoneOffset { get; set; }
}
