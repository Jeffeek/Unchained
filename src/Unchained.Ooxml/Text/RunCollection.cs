using System.Collections;

namespace Unchained.Ooxml.Text;

/// <summary>
///     An ordered, mutable collection of <see cref="Run" /> objects within a <see cref="Paragraph" />.
///     Implements <see cref="IReadOnlyList{T}" /> for forward-compatible enumeration.
/// </summary>
public sealed class RunCollection : IReadOnlyList<Run>
{
    private readonly List<Run> _runs = [];

    // ── IReadOnlyList<Run> ───────────────────────────────────────────────────

    /// <inheritdoc />
    public int Count => _runs.Count;

    /// <inheritdoc cref="IReadOnlyList{T}.this" />
    public Run this[int index] => _runs[index];

    /// <inheritdoc />
    public IEnumerator<Run> GetEnumerator() => _runs.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        _runs.GetEnumerator();

    // ── Mutation ─────────────────────────────────────────────────────────────

    /// <summary>Appends a new run with the given text and returns it.</summary>
    /// <param name="text">The text content of the new run.</param>
    public Run Add(string text)
    {
        var run = new Run { Text = text };
        _runs.Add(run);
        return run;
    }

    /// <summary>
    ///     Inserts a new run at the given zero-based position and returns it.
    /// </summary>
    /// <param name="index">Zero-based insertion position.</param>
    /// <param name="text">The text content of the new run.</param>
    public Run Insert(int index, string text)
    {
        var run = new Run { Text = text };
        _runs.Insert(index, run);
        return run;
    }

    /// <summary>Removes the given run from the collection.</summary>
    public void Remove(Run run) => _runs.Remove(run);

    /// <summary>Appends an already-constructed run (used by the parser).</summary>
    internal void Add(Run run) => _runs.Add(run);

    /// <summary>Removes all runs.</summary>
    public void Clear() => _runs.Clear();
}
