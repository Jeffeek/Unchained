namespace Unchained.Ooxml.Text;

/// <summary>
/// The text body of a shape — a container for <see cref="Paragraph"/> objects and
/// text-body-level formatting settings.
/// </summary>
public sealed class TextFrame
{
    /// <summary>The paragraphs contained in this text frame.</summary>
    public ParagraphCollection Paragraphs { get; } = new();

    /// <summary>Text body formatting (margins, anchor, direction, autofit).</summary>
    public TextFrameFormat Format { get; } = new();

    /// <summary>
    /// Gets or sets the entire text of this frame as a plain string.
    /// <para>
    /// Getter: concatenates the plain text of all paragraphs, separated by newlines.
    /// Setter: replaces all content with a single paragraph containing a single run.
    /// </para>
    /// </summary>
    public string PlainText
    {
        get => string.Join("\n", Paragraphs.Select(static p => p.PlainText));
        set
        {
            Paragraphs.Clear();
            Paragraphs.Add(value);
        }
    }

    /// <summary>
    /// Replaces every occurrence of <paramref name="oldText"/> with <paramref name="newText"/>
    /// across all paragraphs in this frame, preserving run formatting. Matches do not span
    /// paragraph boundaries.
    /// </summary>
    /// <returns>The number of occurrences replaced.</returns>
    public int ReplaceText(
        string oldText,
        string newText,
        StringComparison comparison = StringComparison.Ordinal)
    {
        var count = 0;
        foreach (var paragraph in Paragraphs)
            count += paragraph.ReplaceText(oldText, newText, comparison);
        return count;
    }

    /// <summary>
    /// Replaces this frame's content with that of <paramref name="source"/> — copying the
    /// format and taking over its paragraphs. Used when a frame is populated by a shared parser
    /// into a temporary frame and the result must land on a get-only <see cref="TextFrame"/>.
    /// </summary>
    internal void AbsorbFrom(TextFrame source)
    {
        Format.VerticalAnchor = source.Format.VerticalAnchor;
        Format.Direction = source.Format.Direction;
        Format.Autofit = source.Format.Autofit;
        Format.MarginLeft = source.Format.MarginLeft;
        Format.MarginRight = source.Format.MarginRight;
        Format.MarginTop = source.Format.MarginTop;
        Format.MarginBottom = source.Format.MarginBottom;
        Format.WrapText = source.Format.WrapText;
        Format.ColumnCount = source.Format.ColumnCount;
        Format.ColumnSpacing = source.Format.ColumnSpacing;
        Format.Warp = source.Format.Warp;

        Paragraphs.Clear();
        foreach (var paragraph in source.Paragraphs)
            Paragraphs.Add(paragraph);
    }
}
