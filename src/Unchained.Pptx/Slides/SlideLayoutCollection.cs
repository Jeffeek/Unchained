namespace Unchained.Pptx.Slides;

/// <summary>
/// An ordered, mutable collection of <see cref="SlideLayout"/> objects belonging
/// to a <see cref="MasterSlide"/>.
/// </summary>
public sealed class SlideLayoutCollection : IReadOnlyList<SlideLayout>
{
    private readonly List<SlideLayout> _layouts = [];

    // ── Mutation ─────────────────────────────────────────────────────────────

    /// <summary>Adds a layout to the collection.</summary>
    internal void Add(SlideLayout layout) => _layouts.Add(layout);

    /// <summary>Removes the given layout from the collection.</summary>
    public void Remove(SlideLayout layout) => _layouts.Remove(layout);

    // ── Lookup ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the first layout whose name matches the given string (case-insensitive),
    /// or <see langword="null"/> if none is found.
    /// </summary>
    public SlideLayout? FindByName(string name) =>
        _layouts.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns the first layout with the given type, or <see langword="null"/> if none exists.
    /// </summary>
    public SlideLayout? FindByType(Models.Themes.LayoutType type) =>
        _layouts.FirstOrDefault(l => l.LayoutType == type);

    // ── IReadOnlyList<SlideLayout> ────────────────────────────────────────────

    /// <inheritdoc />
    public int Count => _layouts.Count;

    /// <inheritdoc cref="IReadOnlyList{T}.this" />
    public SlideLayout this[int index] => _layouts[index];

    /// <inheritdoc />
    public IEnumerator<SlideLayout> GetEnumerator() => _layouts.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        _layouts.GetEnumerator();
}
