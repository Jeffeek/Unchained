namespace Unchained.Studio.Studio.Xlsx;

/// <summary>
///     A single chart recommendation produced by <see cref="ChartSuggestions"/> analysis.
/// </summary>
public sealed class ChartSuggestion
{
    public ChartSuggestion(Ooxml.Charts.ChartType type, string label, string description)
    {
        Type = type;
        Label = label;
        Description = description;
    }

    /// <summary>The chart type to suggest.</summary>
    public Ooxml.Charts.ChartType Type { get; }

    /// <summary>Short label shown on the chip.</summary>
    public string Label { get; }

    /// <summary>One-line explanation shown on hover.</summary>
    public string Description { get; }
}
