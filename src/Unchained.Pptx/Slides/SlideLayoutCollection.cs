using System.Collections;
using Unchained.Pptx.Models.Themes;

namespace Unchained.Pptx.Slides;

/// <summary>
///     An ordered, mutable collection of <see cref="SlideLayout" /> objects belonging
///     to a <see cref="MasterSlide" />.
/// </summary>
public sealed class SlideLayoutCollection : IReadOnlyList<SlideLayout>
{
    private readonly List<SlideLayout> _layouts = [];

    /// <summary>The master that owns this collection. Set by <see cref="MasterSlide" />.</summary>
    internal MasterSlide? Owner { get; set; }

    // ── IReadOnlyList<SlideLayout> ────────────────────────────────────────────

    /// <inheritdoc />
    public int Count => _layouts.Count;

    /// <inheritdoc cref="IReadOnlyList{T}.this" />
    public SlideLayout this[int index] => _layouts[index];

    /// <inheritdoc />
    public IEnumerator<SlideLayout> GetEnumerator() => _layouts.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        _layouts.GetEnumerator();

    // ── Mutation ─────────────────────────────────────────────────────────────

    /// <summary>Adds a layout to the collection.</summary>
    internal void Add(SlideLayout layout) => _layouts.Add(layout);

    /// <summary>Removes the given layout from the collection.</summary>
    public void Remove(SlideLayout layout) => _layouts.Remove(layout);

    /// <summary>
    ///     Creates a new, empty slide layout, attaches it to the owning master, and returns it.
    ///     The layout starts with no placeholder shapes; add shapes via <see cref="SlideLayout.Shapes" />.
    /// </summary>
    /// <param name="name">The display name of the new layout.</param>
    /// <param name="type">The layout type. Defaults to <see cref="Models.Themes.LayoutType.Custom" />.</param>
    public SlideLayout AddLayout(string name, LayoutType type = LayoutType.Custom)
    {
        var layout = new SlideLayout
        {
            Name = name,
            LayoutType = type,
            Master = Owner!
        };
        _layouts.Add(layout);
        return layout;
    }

    /// <summary>
    ///     Creates a deep-ish copy of <paramref name="source" /> (its name, type, and shape list),
    ///     attaches it to the owning master, and returns it. The new layout gets fresh part/relationship
    ///     identity so it is written as a distinct part. Shapes are shared by reference, matching the
    ///     slide-clone semantics elsewhere in the API.
    /// </summary>
    public SlideLayout AddClone(SlideLayout source, string? newName = null)
    {
        var clone = new SlideLayout
        {
            Name = newName ?? source.Name,
            LayoutType = source.LayoutType,
            Master = Owner ?? source.Master
        };
        foreach (var shape in source.Shapes)
            clone.Shapes.AddParsed(shape);
        _layouts.Add(clone);
        return clone;
    }

    // ── Lookup ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Returns the first layout whose name matches the given string (case-insensitive),
    ///     or <see langword="null" /> if none is found.
    /// </summary>
    public SlideLayout? FindByName(string name) =>
        _layouts.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Returns the first layout with the given type, or <see langword="null" /> if none exists.
    /// </summary>
    public SlideLayout? FindByType(LayoutType type) =>
        _layouts.FirstOrDefault(l => l.LayoutType == type);
}
