using System.Xml.Linq;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Converts a document to a PDF/X-conformant structure (ISO 15930): adds an
///     <c>/OutputIntents</c> array describing the target print condition with a
///     <c>/GTS_PDFX</c> subtype, a <c>GTS_PDFXVersion</c> marker, and pdfxid XMP metadata.
///     This applies the required structural markers; it does not perform colour conversion
///     (e.g. RGB→CMYK), which would require an ICC colour-management engine.
/// </summary>
internal sealed class PdfXConverter(PdfXProfile profile, string outputConditionIdentifier) : PdfConversionBase
{
    protected override ReadOnlySpan<byte> BuildXmp(PdfDocumentCore core, IReadOnlyDictionary<string, PdfObject> catalogEntries)
    {
        var existing = XmpDocumentHelper.ReadExistingXmp(catalogEntries, core);
        var xmpDoc = (existing is not null ? XmpDocumentHelper.TryParse(existing) : null) ?? XmpDocumentHelper.CreateMinimalXmp();

        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace pdfxid = "http://www.npes.org/pdfx/ns/id/";
        var rdfRoot = xmpDoc.Descendants(rdf + "RDF").FirstOrDefault();

        if (rdfRoot is null)
            return xmpDoc.ToString().ToUtf8Span();

        var desc = rdfRoot.Elements(rdf + "Description").FirstOrDefault(d => d.Attribute(rdf + "about") is not null)
                   ?? new XElement(rdf + "Description", new XAttribute(rdf + "about", ""));
        if (!rdfRoot.Elements(rdf + "Description").Contains(desc))
            rdfRoot.Add(desc);
        if (desc.Attribute(XNamespace.Xmlns + "pdfxid") is null)
            desc.Add(new XAttribute(XNamespace.Xmlns + "pdfxid", pdfxid.NamespaceName));
        XmpDocumentHelper.SetOrAdd(desc, pdfxid + "GTS_PDFXVersion", VersionString(profile));

        return xmpDoc.ToString().ToUtf8Span();
    }

    protected override int PreMetadataHook(List<PdfIndirectObject> objects, int maxObj)
    {
        // ── OutputIntents ──────────────────────────────────────────────────
        var intentObjNum = maxObj + 1;
        var intentDict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.OutputIntent,
                [PdfName.S.Value] = PdfName.GTS_PDFX,
                [PdfName.OutputConditionIdentifier.Value] = PdfString.FromLatin1(outputConditionIdentifier),
                [PdfName.Info.Value] = PdfString.FromLatin1(outputConditionIdentifier),
                [PdfName.RegistryName.Value] = PdfString.FromLatin1("http://www.color.org")
            }
        );
        objects.Add(new PdfIndirectObject(intentObjNum, 0, intentDict));
        // Catalog will add the reference in the base class
        return 1; // one object added
    }

    protected override void PostCatalogRebuild(List<PdfIndirectObject> objects, PdfDocumentCore core)
    {
        // Add OutputIntents reference to catalog
        var (catalogObjNum, catalogIdx) = ResolveCatalog(core, objects);
        if (catalogIdx >= 0 && objects[catalogIdx].Value is PdfDictionary cat)
        {
            var entries = new Dictionary<string, PdfObject>(cat.Entries)
            {
                ["OutputIntents"] = new PdfArray([new PdfIndirectReference(catalogObjNum + 1, 0)])
            };
            objects[catalogIdx] = new PdfIndirectObject(catalogObjNum, objects[catalogIdx].Generation, new PdfDictionary(entries));
        }

        // Info dict needs GTS_PDFXVersion + a Title (PDF/X requires both)
        var infoRef = core.Trailer[PdfName.Info] as PdfIndirectReference;
        var infoObjNum = infoRef?.ObjectNumber ?? (catalogObjNum + 3);
        var infoEntries = new Dictionary<string, PdfObject>();
        if (infoRef is not null && core.ResolveIndirect(infoRef.ObjectNumber).Value is PdfDictionary existingInfo)
            infoEntries = new Dictionary<string, PdfObject>(existingInfo.Entries);

        infoEntries["GTS_PDFXVersion"] = PdfString.FromLatin1(VersionString(profile));
        if (!infoEntries.ContainsKey(PdfName.Title.Value))
            infoEntries[PdfName.Title.Value] = PdfString.FromLatin1("Untitled");

        var infoIdx = objects.FindIndex(o => o.ObjectNumber == infoObjNum);
        if (infoIdx >= 0)
            objects[infoIdx] = new PdfIndirectObject(infoObjNum, 0, new PdfDictionary(infoEntries));
        else
            objects.Add(new PdfIndirectObject(infoObjNum, 0, new PdfDictionary(infoEntries)));
    }

    private static string VersionString(PdfXProfile p) => p switch
    {
        PdfXProfile.PdfX1A2001 => "PDF/X-1a:2001",
        PdfXProfile.PdfX1A2003 => "PDF/X-1a:2003",
        PdfXProfile.PdfX32002 => "PDF/X-3:2002",
        PdfXProfile.PdfX32003 => "PDF/X-3:2003",
        PdfXProfile.PdfX4 => "PDF/X-4",
        _ => "PDF/X-1a:2001"
    };
}
