namespace Unchained.Pptx.Models.Text;

/// <summary>
/// Identifies the type of auto-updating field inserted into a text run.
/// Fields are automatically refreshed by the presentation viewer.
/// </summary>
public enum FieldType
{
    /// <summary>Inserts the current slide number.</summary>
    SlideNumber,
    /// <summary>Inserts the current date, formatted according to the presentation locale.</summary>
    Date,
    /// <summary>Inserts the current time.</summary>
    Time,
    /// <summary>Inserts a fixed (non-updating) date and time stamp.</summary>
    FixedDateTime,
    /// <summary>Inserts the total slide count of the presentation.</summary>
    TotalSlides
}
