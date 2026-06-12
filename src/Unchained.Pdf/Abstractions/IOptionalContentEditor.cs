using Unchained.Pdf.Models;

namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Reads and toggles optional content groups ("layers", ISO 32000-1 §8.11). Visibility is
///     controlled by the default configuration's <c>/OFF</c> array in <c>/OCProperties /D</c>.
/// </summary>
public interface IOptionalContentEditor
{
    /// <summary>Returns the document's layers with their default visibility.</summary>
    Task<IReadOnlyList<OptionalContentGroup>> GetLayersAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Sets the default visibility of the layer with the given OCG object number, mutating
    ///     the document in place. Adds it to (or removes it from) the <c>/D /OFF</c> array.
    /// </summary>
    /// <param name="document">Document to mutate in place.</param>
    /// <param name="ocgObjectNumber">The OCG's indirect object number (from <see cref="OptionalContentGroup.ObjectNumber" />).</param>
    /// <param name="visible">True to show the layer, false to hide it.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetLayerVisibilityAsync(
        IPdfDocument document,
        int ocgObjectNumber,
        bool visible,
        CancellationToken ct = default
    );
}
