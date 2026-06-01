namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Fills and flattens AcroForm fields in a PDF document.
/// </summary>
public interface IFormFiller
{
    /// <summary>
    /// Sets the value of each field named in <paramref name="values"/>.
    /// Keys are fully-qualified field names (dot-separated). The document is mutated in-place.
    /// </summary>
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
