using System.Buffers;
using System.Security.Cryptography;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Writing;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Shared machinery for PDF structural conversion (PDF/A, PDF/X, etc.).
///     Each subclass implements the profile-specific XMP mutation and any extra catalog entries.
/// </summary>
internal abstract class PdfConversionBase
{
    /// <summary>
    ///     Produces the XMP payload for this profile.
    /// </summary>
    protected abstract ReadOnlySpan<byte> BuildXmp(PdfDocumentCore core, IReadOnlyDictionary<string, PdfObject> catalogEntries);

    /// <summary>
    ///     Additional catalog entries to merge (can be null).
    /// </summary>
    protected virtual IReadOnlyDictionary<string, PdfObject>? ExtraCatalogEntries => null;

    /// <summary>
    ///     Adds objects that should precede metadata (e.g. OutputIntents for PDF/X).
    ///     Return the count of objects added so the caller can offset the metadata object number.
    /// </summary>
    protected virtual int PreMetadataHook(List<PdfIndirectObject> objects, int maxObj) => 0;

    /// <summary>
    ///     Finds the catalog object in the list. Returns (objNum, index) or (-1, -1).
    /// </summary>
    protected static (int objNum, int idx) ResolveCatalog(PdfDocumentCore core, List<PdfIndirectObject> objects)
    {
        var catalogObjNum = (core.Trailer[PdfName.Root] as PdfIndirectReference)?.ObjectNumber ?? 0;
        var idx = objects.FindIndex(o => o.ObjectNumber == catalogObjNum);
        return (catalogObjNum, idx);
    }

    /// <summary>
    ///     Post-catalog-rebuild hook (e.g. fix annotation flags, modify info dict).
    /// </summary>
    protected virtual void PostCatalogRebuild(List<PdfIndirectObject> objects, PdfDocumentCore core) { }

    /// <summary>
    ///     Final trailer customization (e.g. add GTS_PDFXVersion to info dict).
    /// </summary>
    protected virtual void FinalizeTrailer(Dictionary<string, PdfObject> trailerEntries, List<PdfIndirectObject> objects, PdfDocumentCore core) { }

    /// <summary>
    ///     Performs the conversion and returns the serialized byte buffer.
    /// </summary>
    internal byte[] Convert(PdfDocumentCore core)
    {
        if (core.IsEncrypted)
            throw new InvalidOperationException("Cannot convert an encrypted PDF. Decrypt first.");

        var (objects, maxObj) = MutationHelper.CollectWithMax(core);

        // ── Resolve catalog ─────────────────────────────────────────────────
        var catalogObjNum = (core.Trailer[PdfName.Root] as PdfIndirectReference)?.ObjectNumber ?? 0;
        var catalogIdx = objects.FindIndex(o => o.ObjectNumber == catalogObjNum);
        if (catalogIdx < 0)
            throw new InvalidOperationException("Cannot locate catalog object.");

        var catalogDict = objects[catalogIdx].Value as PdfDictionary
                          ?? throw new InvalidOperationException("Catalog is not a dictionary.");
        var catalogEntries = new Dictionary<string, PdfObject>(catalogDict.Entries);

        // ── Pre-metadata hook (e.g. OutputIntents for PDF/X) ──────────────────
        var preMetaAdded = PreMetadataHook(objects, maxObj);

        // ── Metadata with XMP ───────────────────────────────────────────────
        var metaObjNum = maxObj + 1 + preMetaAdded;
        var xmpBytes = BuildXmp(core, catalogEntries);
        catalogEntries[PdfName.Metadata.Value] = CreateMetadataObject(objects, metaObjNum, xmpBytes);
        // The catalog must reference the metadata object indirectly; the object itself
        // was appended to `objects` by CreateMetadataObject.

        // ── Extra catalog entries ───────────────────────────────────────────
        if (ExtraCatalogEntries is { } extraCat)
        {
            foreach (var kv in extraCat)
                catalogEntries[kv.Key] = kv.Value;
        }

        // ── Rebuild catalog ─────────────────────────────────────────────────
        var gen = objects[catalogIdx].Generation;
        objects[catalogIdx] = new PdfIndirectObject(catalogObjNum, gen, new PdfDictionary(catalogEntries));

        // ── Post-catalog hook ───────────────────────────────────────────────
        PostCatalogRebuild(objects, core);

        // ── Trailer ─────────────────────────────────────────────────────────
        var maxAfter = objects.Max(static o => o.ObjectNumber);
        var trailerEntries = new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(maxAfter + 1),
            [PdfName.Root.Value] = core.Trailer[PdfName.Root]!
        };

        // /Info
        if (core.Trailer[PdfName.Info] is { } info)
            trailerEntries[PdfName.Info.Value] = info;

        // Final trailer customization (e.g. PdfX adds GTS_PDFXVersion to info)
        FinalizeTrailer(trailerEntries, objects, core);

        // /ID — preserve existing or generate
        var existingId = core.Trailer.Get<PdfArray>(PdfName.ID);
        trailerEntries[PdfName.ID.Value] = existingId ?? GenerateId();

        var buf = new ArrayBufferWriter<byte>();
        using var writer = new PdfWriter(buf);
        writer.Write(objects, new PdfDictionary(trailerEntries));
        return buf.WrittenMemory.ToArray();

        static PdfIndirectReference CreateMetadataObject(ICollection<PdfIndirectObject> objects, int metaObjNum, ReadOnlySpan<byte> xmpBytes)
        {
            var metaDict = new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    [PdfName.Type.Value] = PdfName.Metadata,
                    [PdfName.Subtype.Value] = PdfName.XML,
                    [PdfName.Length.Value] = new PdfInteger(xmpBytes.Length)
                }
            );
            objects.Add(new PdfIndirectObject(metaObjNum, 0, new PdfStream(metaDict, xmpBytes.ToArray())));
            return new PdfIndirectReference(metaObjNum, 0);
        }

        static PdfObject GenerateId() =>
            new PdfArray(
                [
                    new PdfString(RandomNumberGenerator.GetBytes(16), true),
                    new PdfString(RandomNumberGenerator.GetBytes(16), true)
                ]
            );
    }
}
