namespace Unchained.Ooxml.Text;

/// <summary>
/// An ordered, mutable collection of <see cref="Paragraph"/> objects within a
/// <see cref="TextFrame"/>. Implements <see cref="IReadOnlyList{T}"/> for enumeration.
/// </summary>
public sealed class ParagraphCollection : IReadOnlyList<Paragraph>
{
    private readonly List<Paragraph> _paragraphs = [];

    // ── Mutation ─────────────────────────────────────────────────────────────

    /// <summary>Appends a new empty paragraph and returns it.</summary>
    public Paragraph Add()
    {
        var paragraph = new Paragraph();
        _paragraphs.Add(paragraph);
        return paragraph;
    }

    /// <summary>Appends a new paragraph with the given text and returns it.</summary>
    /// <param name="text">Initial text for the first run of the paragraph.</param>
    public Paragraph Add(string text)
    {
        var paragraph = new Paragraph();
        paragraph.Runs.Add(text);
        _paragraphs.Add(paragraph);
        return paragraph;
    }

    /// <summary>Inserts a new empty paragraph at the given zero-based position and returns it.</summary>
    public Paragraph Insert(int index)
    {
        var paragraph = new Paragraph();
        _paragraphs.Insert(index, paragraph);
        return paragraph;
    }

    /// <summary>Removes the given paragraph from the collection.</summary>
    public void Remove(Paragraph paragraph) => _paragraphs.Remove(paragraph);

    /// <summary>Removes the paragraph at the given zero-based index.</summary>
    public void RemoveAt(int index) => _paragraphs.RemoveAt(index);

    /// <summary>Appends an already-constructed paragraph (used by the parser).</summary>
    internal void Add(Paragraph paragraph) => _paragraphs.Add(paragraph);

    /// <summary>Removes all paragraphs.</summary>
    public void Clear() => _paragraphs.Clear();

    // ── IReadOnlyList<Paragraph> ─────────────────────────────────────────────

    /// <inheritdoc />
    public int Count => _paragraphs.Count;

    /// <inheritdoc cref="IReadOnlyList{T}.this" />
    public Paragraph this[int index] => _paragraphs[index];

    /// <inheritdoc />
    public IEnumerator<Paragraph> GetEnumerator() => _paragraphs.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        _paragraphs.GetEnumerator();
}
