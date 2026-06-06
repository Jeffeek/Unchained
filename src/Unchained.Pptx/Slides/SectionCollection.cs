namespace Unchained.Pptx.Slides;

/// <summary>
/// An ordered, mutable collection of presentation sections.
/// Sections logically group consecutive slides under named headings visible in PowerPoint's
/// slide panel; they are stored as PowerPoint 2010+ extensions in <c>presentation.xml</c>.
/// </summary>
public sealed class SectionCollection : IReadOnlyList<PptxSection>
{
    private readonly List<PptxSection> _sections = [];

    // ── IReadOnlyList<PptxSection> ───────────────────────────────────────────

    /// <inheritdoc />
    public int Count => _sections.Count;

    /// <inheritdoc cref="IReadOnlyList{T}.this" />
    public PptxSection this[int index] => _sections[index];

    /// <inheritdoc />
    public IEnumerator<PptxSection> GetEnumerator() => _sections.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        _sections.GetEnumerator();

    // ── Mutation ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a new section with the given name and optional initial slide IDs.
    /// </summary>
    /// <param name="name">The display name shown in PowerPoint's slide panel.</param>
    /// <param name="slideIds">
    /// Slide IDs (<see cref="Slide.SlideId"/>) to assign to this section.
    /// </param>
    public PptxSection Add(string name, IEnumerable<uint>? slideIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var section = new PptxSection(name);
        if (slideIds != null)
            section.SlideIds.AddRange(slideIds);
        _sections.Add(section);
        return section;
    }

    /// <summary>Removes the given section. The slides it contained are not deleted.</summary>
    /// <exception cref="ArgumentException">Thrown when the section is not in this collection.</exception>
    public void Remove(PptxSection section)
    {
        if (!_sections.Remove(section))
            throw new ArgumentException("The section does not belong to this collection.", nameof(section));
    }

    /// <summary>Removes all sections. Slides are not affected.</summary>
    public void Clear() => _sections.Clear();

    /// <summary>Adds a pre-parsed section (used by the parser).</summary>
    internal void AddParsed(PptxSection section) => _sections.Add(section);
}
