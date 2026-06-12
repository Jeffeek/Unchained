namespace Unchained.Pptx.Comments;

/// <summary>
///     Represents a comment author registered in the presentation.
/// </summary>
public sealed class CommentAuthor
{
    internal CommentAuthor(uint id, string name, string initials)
    {
        Id = id;
        Name = name;
        Initials = initials;
    }

    /// <summary>The internal numeric identifier of this author.</summary>
    public uint Id { get; }

    /// <summary>The display name of the author.</summary>
    public string Name { get; set; }

    /// <summary>
    ///     The initials of the author, used as an abbreviated label in comment badges.
    /// </summary>
    public string Initials { get; set; }

    /// <summary>
    ///     Tracks the highest comment index assigned by this author.
    ///     Used internally to generate unique <c>idx</c> values for new comments.
    /// </summary>
    internal uint LastIndex { get; set; }
}
