using Unchained.Pdf.Core;

namespace Unchained.Pdf.Document;

/// <summary>
///     Recursively walks a <see cref="PdfObject" /> graph and adds offset/>
///     to every <see cref="PdfIndirectReference.ObjectNumber" /> it encounters.
///     Used by <see cref="Unchained.Pdf.Engine.DocumentMerger" /> to renumber objects from
///     multiple source documents into a single non-conflicting number space.
/// </summary>
internal static class PdfObjectRemapper
{
    /// <summary>
    ///     Walks <paramref name="obj" /> and replaces any <see cref="PdfIndirectReference" />
    ///     whose <c>ObjectNumber</c> appears in <paramref name="remapping" /> with a reference
    ///     to the canonical object number. Used for stream deduplication.
    /// </summary>
    internal static PdfObject RemapSelective(PdfObject obj, IReadOnlyDictionary<int, int> remapping) =>
        obj switch
        {
            PdfIndirectReference r when remapping.TryGetValue(r.ObjectNumber, out var canon) => new PdfIndirectReference(canon, r.Generation),
            PdfIndirectObject io => new PdfIndirectObject(io.ObjectNumber, io.Generation, RemapSelective(io.Value, remapping)),
            PdfArray a => new PdfArray(a.Elements.Select(e => RemapSelective(e, remapping)).ToArray()),
            PdfDictionary d => new PdfDictionary(d.Entries.ToDictionary(static kvp => kvp.Key, kvp => RemapSelective(kvp.Value, remapping))),
            PdfStream s => new PdfStream((PdfDictionary)RemapSelective(s.Dictionary, remapping), s.Data),
            _ => obj
        };

    internal static PdfObject Remap(PdfObject obj, int offset) => obj switch
    {
        PdfIndirectReference r => new PdfIndirectReference(r.ObjectNumber + offset, r.Generation),
        PdfIndirectObject io => new PdfIndirectObject(io.ObjectNumber + offset, io.Generation, Remap(io.Value, offset)),
        PdfArray a => new PdfArray(a.Elements.Select(e => Remap(e, offset)).ToArray()),
        PdfDictionary d => new PdfDictionary(d.Entries.ToDictionary(static kvp => kvp.Key, kvp => Remap(kvp.Value, offset))),
        // Stream bytes are opaque binary payload — walk only the dictionary.
        PdfStream s => new PdfStream((PdfDictionary)Remap(s.Dictionary, offset), s.Data),
        // All primitives (Boolean, Integer, Real, String, Name, Null) pass through unchanged.
        _ => obj
    };
}
