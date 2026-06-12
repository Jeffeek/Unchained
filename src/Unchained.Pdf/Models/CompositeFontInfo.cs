namespace Unchained.Pdf.Models;

/// <summary>
///     Metadata describing a composite (Type0) font as used on a page (ISO 32000-1 §9.7).
///     Composite fonts encode text as multi-byte character codes that map through a CMap
///     (the <c>/Encoding</c> entry) to CIDs, and then to glyph indices via the descendant
///     CIDFont's <c>/CIDToGIDMap</c>.
/// </summary>
/// <param name="IdentityEncoding">
///     <see langword="true" /> when the font uses an Identity CMap (<c>/Identity-H</c> or
///     <c>/Identity-V</c>): each pair of bytes is a big-endian 16-bit code that equals the CID.
/// </param>
/// <param name="IdentityCidToGid">
///     <see langword="true" /> when <c>/CIDToGIDMap</c> is <c>/Identity</c> (or absent, which
///     defaults to Identity): the CID equals the glyph index. When <see langword="false" />,
///     <see cref="CidToGid" /> holds the explicit mapping.
/// </param>
/// <param name="CidToGid">
///     Explicit CID→glyph-index map parsed from a <c>/CIDToGIDMap</c> stream, or
///     <see langword="null" /> when <see cref="IdentityCidToGid" /> is <see langword="true" />.
/// </param>
/// <param name="DefaultWidth">
///     The default glyph advance width in glyph-space units (the <c>/DW</c> entry, default 1000).
/// </param>
/// <param name="Widths">
///     Per-CID glyph advance widths in glyph-space units, parsed from the <c>/W</c> array.
///     CIDs not present use <see cref="DefaultWidth" />.
/// </param>
public sealed record CompositeFontInfo(
    bool IdentityEncoding,
    bool IdentityCidToGid,
    IReadOnlyDictionary<int, int>? CidToGid,
    double DefaultWidth,
    IReadOnlyDictionary<int, double> Widths
);
