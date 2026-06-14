namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Reads and writes named destinations stored in the document's
///     <c>/Names /Dests</c> name tree (ISO 32000-1 §12.3.2.3).
/// </summary>
// ReSharper disable once MemberCanBeInternal
public interface INamedDestinationEditor
{
    /// <summary>
    ///     Adds or updates a named destination, mapping <paramref name="name" /> to
    ///     the given 1-based <paramref name="pageNumber" />. The document is mutated in-place.
    /// </summary>
    Task SetDestinationAsync(
        IPdfDocument document,
        string name,
        int pageNumber,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Removes the named destination identified by <paramref name="name" /> from the
    ///     <c>/Names /Dests</c> tree. No-op if the name does not exist.
    ///     The document is mutated in-place.
    /// </summary>
    Task RemoveDestinationAsync(
        IPdfDocument document,
        string name,
        CancellationToken ct = default
    );
}
