using System.Collections;

namespace Unchained.Pptx.Slides;

/// <summary>
///     An ordered, mutable collection of <see cref="MasterSlide" /> objects in a presentation.
/// </summary>
public sealed class MasterSlideCollection : IReadOnlyList<MasterSlide>
{
    private readonly List<MasterSlide> _masters = [];

    // ── IReadOnlyList<MasterSlide> ────────────────────────────────────────────

    /// <inheritdoc />
    public int Count => _masters.Count;

    /// <inheritdoc cref="IReadOnlyList{T}.this" />
    public MasterSlide this[int index] => _masters[index];

    /// <inheritdoc />
    public IEnumerator<MasterSlide> GetEnumerator() => _masters.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        _masters.GetEnumerator();

    // ── Mutation ─────────────────────────────────────────────────────────────

    /// <summary>Adds a master to the collection.</summary>
    internal void Add(MasterSlide master) => _masters.Add(master);

    /// <summary>Removes the given master from the collection.</summary>
    public void Remove(MasterSlide master) => _masters.Remove(master);
}
