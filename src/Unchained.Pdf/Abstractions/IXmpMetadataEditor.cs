namespace Unchained.Pdf.Abstractions;

/// <summary>
///     Reads and writes the XMP metadata packet stored in the document catalog's
///     <c>/Metadata</c> stream (ISO 32000-1 §14.3).
///     XMP metadata is distinct from the <c>/Info</c> dictionary; it uses an
///     RDF/XML format defined by Adobe's Extensible Metadata Platform specification.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public interface IXmpMetadataEditor
{
    // ReSharper disable CommentTypo
    /// <summary>
    ///     Sets the document's <c>/Metadata</c> XMP stream to the XML string provided.
    ///     Replaces any existing XMP packet. The document is mutated in-place.
    /// </summary>
    /// <param name="document">The PDF document to modify.</param>
    /// <param name="xmpXml">
    ///     A valid XMP/RDF XML string, typically starting with
    ///     <c>&lt;?xpacket begin="…" id="W5M0MpCehiHzreSzNTczkc9d"?&gt;</c>.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    // ReSharper restore CommentTypo
    Task SetXmpMetadataAsync(
        IPdfDocument document,
        string xmpXml,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Removes the <c>/Metadata</c> entry from the document catalog.
    ///     The document is mutated in-place.
    /// </summary>
    Task RemoveMetadataAsync(
        IPdfDocument document,
        CancellationToken ct = default
    );
}
