namespace Unchained.Ooxml.Text;

/// <summary>
///     A single text run within a paragraph — the smallest unit of text that shares
///     a uniform set of character formatting.
/// </summary>
public sealed class Run
{
    /// <summary>The text content of this run.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    ///     The character-level formatting applied to this run.
    ///     Any property not explicitly set inherits from the paragraph or theme.
    /// </summary>
    public RunFormat Format { get; } = new();

    /// <summary>
    ///     When non-<see langword="null" />, the run is an auto-updating field
    ///     (e.g. slide number or current date) rather than static text.
    /// </summary>
    public FieldType? Field { get; set; }
}
