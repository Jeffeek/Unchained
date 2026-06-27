using Unchained.Ooxml.Charts;
using Unchained.Studio.Components.Xlsx;

namespace Unchained.Studio.Studio.Xlsx;

/// <summary>
///     A single chart recommendation produced by <see cref="ChartSuggestions" /> analysis.
/// </summary>
public sealed class ChartSuggestion(ChartType type, string label, string description)
{
    /// <summary>The chart type to suggest.</summary>
    public ChartType Type { get; } = type;

    /// <summary>Short label shown on the chip.</summary>
    public string Label { get; } = label;

    /// <summary>One-line explanation shown on hover.</summary>
    public string Description { get; } = description;
}
