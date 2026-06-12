using System.Collections;

namespace Unchained.Pptx.Comments;

/// <summary>
///     The registry of comment authors for a presentation.
///     All comments reference an author from this collection.
/// </summary>
public sealed class CommentAuthorCollection : IReadOnlyList<CommentAuthor>
{
    private readonly List<CommentAuthor> _authors = [];

    // ── IReadOnlyList<CommentAuthor> ─────────────────────────────────────────

    /// <inheritdoc />
    public int Count => _authors.Count;

    /// <inheritdoc cref="IReadOnlyList{T}.this" />
    public CommentAuthor this[int index] => _authors[index];

    /// <inheritdoc />
    public IEnumerator<CommentAuthor> GetEnumerator() => _authors.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        _authors.GetEnumerator();

    // ── Mutation ─────────────────────────────────────────────────────────────

    /// <summary>
    ///     Adds a new author with the given name and initials and returns the new author.
    /// </summary>
    /// <param name="name">The full display name of the author.</param>
    /// <param name="initials">
    ///     Abbreviated label for the author (1–3 characters). Defaults to the first letters
    ///     of each word in <paramref name="name" />.
    /// </param>
    public CommentAuthor Add(string name, string? initials = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var computed = initials ?? ComputeInitials(name);
        var id = _authors.Count == 0 ? 0u : (_authors[^1].Id + 1u);
        var author = new CommentAuthor(id, name, computed);
        _authors.Add(author);
        return author;
    }

    /// <summary>
    ///     Returns the author with the given ID, or <see langword="null" /> if not found.
    /// </summary>
    public CommentAuthor? FindById(uint id) =>
        _authors.FirstOrDefault(a => a.Id == id);

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>Adds a pre-parsed author (used by the parser).</summary>
    internal void AddParsed(CommentAuthor author) => _authors.Add(author);

    private static string ComputeInitials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 1
            ? name[..Math.Min(2, name.Length)].ToUpperInvariant()
            : string.Concat(words.Select(static w => char.ToUpperInvariant(w[0])));
    }
}
