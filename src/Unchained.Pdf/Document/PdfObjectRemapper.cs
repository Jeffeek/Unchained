using Unchained.Pdf.Core;

namespace Unchained.Pdf.Document;

/// <summary>
/// Recursively walks a <see cref="PdfObject"/> graph and adds offset/>
/// to every <see cref="PdfIndirectReference.ObjectNumber"/> it encounters.
/// Used by <see cref="Unchained.Pdf.Engine.DocumentMerger"/> to renumber objects from
/// multiple source documents into a single non-conflicting number space.
/// </summary>
internal static class PdfObjectRemapper
{
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
