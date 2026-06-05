namespace Unchained.Pptx.Text;

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
}
