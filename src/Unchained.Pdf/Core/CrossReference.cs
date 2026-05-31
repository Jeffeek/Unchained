namespace Unchained.Pdf.Core;

/// <summary>
/// Classifies an entry in the PDF cross-reference table (ISO 32000-1 §7.5.4).
/// </summary>
public enum CrossReferenceEntryType : byte
{
    /// <summary>
    /// Free entry (<c>f</c>). The object slot is available for reuse.
    /// Object 0 is always free and acts as the head of the free-object linked list.
    /// </summary>
    Free = 0,

    /// <summary>
    /// In-use entry (<c>n</c>). The object body is located at the byte offset
    /// stored in <see cref="CrossReferenceEntry.Offset"/>.
    /// </summary>
    InUse = 1,

    /// <summary>
    /// Compressed entry (type 2). The object is stored inside an object stream
    /// (ISO 32000-1 §7.5.7). <see cref="CrossReferenceEntry.Offset"/> holds
    /// the object number of the containing object stream.
    /// </summary>
    Compressed
}

/// <summary>
/// A single entry in the cross-reference table, describing the location and
/// status of one indirect object (ISO 32000-1 §7.5.4 Table 18).
/// </summary>
public readonly struct CrossReferenceEntry(long offset, int generation, CrossReferenceEntryType type)
{
    /// <summary>
    /// For <see cref="CrossReferenceEntryType.InUse"/> entries: the absolute byte offset
    /// from the beginning of the file where the object body begins.<br/>
    /// For <see cref="CrossReferenceEntryType.Compressed"/> entries: the object number of
    /// the object stream that contains this object.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public long Offset { get; } = offset;

    /// <summary>
    /// The generation number of the object. Zero in non-incrementally-updated PDFs.
    /// Increments each time an object slot is freed and then reused.
    /// </summary>
    public int Generation { get; } = generation;

    /// <summary>Whether this entry is free, in-use, or compressed.</summary>
    // ReSharper disable once MemberCanBeInternal
    public CrossReferenceEntryType Type { get; } = type;

    /// <summary>
    /// <see langword="true"/> when <see cref="Type"/> is <see cref="CrossReferenceEntryType.Free"/>.
    /// Attempting to resolve a free object is an error.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public bool IsFree => Type == CrossReferenceEntryType.Free;
}

/// <summary>
/// The fully resolved cross-reference table for a PDF document, built by
/// <see cref="Unchained.Pdf.Parsing.PdfParser"/> from either a traditional
/// <c>xref</c> section (§7.5.4) or a cross-reference stream (§7.5.8).
/// Incremental updates are merged: the most-recent definition of any object wins.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class CrossReferenceTable(IReadOnlyDictionary<int, CrossReferenceEntry> entries, long trailerOffset)
{
    /// <summary>
    /// The byte offset of the <c>xref</c> keyword (or cross-reference stream object)
    /// that was located via <c>startxref</c> at the end of the file.
    /// </summary>
    public long TrailerOffset { get; } = trailerOffset;

    /// <summary>The total number of entries in the table.</summary>
    public int Count => entries.Count;

    /// <summary>
    /// Attempts to retrieve the entry for <paramref name="objectNumber"/>.
    /// Returns <see langword="false"/> if the object number is not present in the table.
    /// </summary>
    public bool TryGetEntry(int objectNumber, out CrossReferenceEntry entry) =>
        entries.TryGetValue(objectNumber, out entry);

    /// <summary>
    /// Returns the entry for <paramref name="objectNumber"/>.
    /// </summary>
    /// <exception cref="PdfException">
    /// Thrown when <paramref name="objectNumber"/> is not present in the table.
    /// </exception>
    // ReSharper disable once MemberCanBeInternal
    public CrossReferenceEntry GetEntry(int objectNumber) =>
        entries.TryGetValue(objectNumber, out var e)
            ? e
            : throw new PdfException($"Object {objectNumber} not found in cross-reference table.");
}
