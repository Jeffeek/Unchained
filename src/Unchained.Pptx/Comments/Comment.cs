namespace Unchained.Pptx.Comments;

/// <summary>
///     A single comment anchored to a position on a slide.
/// </summary>
public sealed class Comment
{
    internal Comment(
        CommentAuthor author,
        string text,
        SlidePosition position,
        DateTimeOffset createdAt,
        uint index
    )
    {
        Author = author;
        Text = text;
        Position = position;
        CreatedAt = createdAt;
        Index = index;
    }

    /// <summary>The comment body text.</summary>
    public string Text { get; set; }

    /// <summary>The author who created this comment.</summary>
    public CommentAuthor Author { get; }

    /// <summary>The position of the comment anchor on the slide (in EMU).</summary>
    public SlidePosition Position { get; set; }

    /// <summary>The date and time when this comment was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    ///     The sequential index of this comment within the author's comments on this slide.
    ///     Used internally for the OOXML <c>idx</c> attribute.
    /// </summary>
    internal uint Index { get; }
}
