namespace Unchained.Pdf.Abstractions;

/// <summary>
/// Imports and exports annotations using the XML Forms Data Format (XFDF),
/// as defined in ISO 19444-1 and referenced by ISO 32000-1 §12.7.8.
/// </summary>
public interface IXfdfEditor
{
    /// <summary>
    /// Exports all annotations from every page of <paramref name="document"/> to an
    /// XFDF XML string. Returns an empty XFDF document when no annotations exist.
    /// </summary>
    string ExportAnnotationsToXfdf(IPdfDocument document);

    /// <summary>
    /// Parses <paramref name="xfdfXml"/> and appends the contained annotations to the
    /// matching pages of <paramref name="document"/>. The document is mutated in-place.
    /// </summary>
    Task ImportAnnotationsFromXfdfAsync(
        IPdfDocument document,
        string xfdfXml,
        CancellationToken ct = default
    );
}
