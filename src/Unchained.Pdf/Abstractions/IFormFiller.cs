namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Fills and flattens AcroForm fields in a PDF document.
/// </summary>
public interface IFormFiller
{
    /// <summary>
    /// Sets the value of each field named in <paramref name="values"/>.
    /// The document is mutated in-place.
    /// </summary>
    /// <param name="document">The document containing the AcroForm. Must not be disposed.</param>
    /// <param name="values">
    /// Map of fully-qualified field name (dot-separated) to the new value string.
    /// Fields not present in the document are silently ignored.
    /// </param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task FillAsync(
        IPdfDocument document,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default
    );

    /// <summary>
    /// Merges field appearance streams into page content and removes the <c>/AcroForm</c>
    /// entry. Only <c>Tx</c> (text) fields with a normal appearance stream are flattened
    /// in this release; other field types are left as widget annotations.
    /// The document is mutated in-place.
    /// </summary>
    Task FlattenAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );
}
