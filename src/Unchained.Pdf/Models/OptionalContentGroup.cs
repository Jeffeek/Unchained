namespace Unchained.Pdf.Models;

/// <summary>
/// An optional content group ("layer", ISO 32000-1 §8.11): a named group of content whose
/// visibility can be toggled. Read via <see cref="Abstractions.IPdfDocument.GetLayers"/>.
/// </summary>
/// <param name="Name">The layer's display name (<c>/Name</c>).</param>
/// <param name="ObjectNumber">The OCG's indirect object number (identifies it for toggling).</param>
/// <param name="Visible">
/// Whether the layer is on in the default configuration (<c>/D</c>): true unless the OCG
/// appears in the configuration's <c>/OFF</c> array.
/// </param>
public sealed record OptionalContentGroup(
    string Name,
    int ObjectNumber,
    bool Visible
);
