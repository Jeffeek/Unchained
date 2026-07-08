using System.Xml.Linq;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Performs structural PDF/A conformance fixes on a PDF document.
///     <para>
///         Handles the structural requirements that can be fixed programmatically:
///         pdfaid XMP metadata, /ID in trailer, removing prohibited catalog entries,
///         and setting annotation Print flags. Does NOT embed fonts, add output intents,
///         or remove transparency — those require additional resources or content rewriting.
///         Validate after conversion to see any remaining violations.
///     </para>
/// </summary>
internal sealed class PdfAConverter(PdfAProfile profile) : PdfConversionBase
{
    protected override ReadOnlySpan<byte> BuildXmp(PdfDocumentCore core, IReadOnlyDictionary<string, PdfObject> catalogEntries)
    {
        var existing = XmpDocumentHelper.ReadExistingXmp(catalogEntries, core);
        var xmpDoc = existing is not null
            ? XmpDocumentHelper.TryParse(existing) ?? XmpDocumentHelper.CreateMinimalXmp()
            : XmpDocumentHelper.CreateMinimalXmp();

        SetPdfaidProperties(xmpDoc, profile);
        return xmpDoc.ToString().ToUtf8Span();
    }

    protected override IReadOnlyDictionary<string, PdfObject>? ExtraCatalogEntries => null;

    protected override int PreMetadataHook(List<PdfIndirectObject> objects, int maxObj) => 0;

    protected override void PostCatalogRebuild(List<PdfIndirectObject> objects, PdfDocumentCore core)
    {
        // Remove prohibited catalog entries
        var (catalogObjNum, catalogIdx) = ResolveCatalog(core, objects);
        if (catalogIdx >= 0 && objects[catalogIdx].Value is PdfDictionary cat)
        {
            var entries = new Dictionary<string, PdfObject>(cat.Entries);
            entries.Remove("AA");
            entries.Remove("Collection");
            objects[catalogIdx] = new PdfIndirectObject(catalogObjNum, objects[catalogIdx].Generation, new PdfDictionary(entries));
        }

        // Fix annotation Print flags
        FixAnnotationFlags(objects, core);
    }

    private static void SetPdfaidProperties(XContainer xmpDoc, PdfAProfile profile)
    {
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace pdfaid = "http://www.aiim.org/pdfa/ns/id/";

        var rdfRoot = xmpDoc.Descendants(rdf + "RDF").FirstOrDefault();
        if (rdfRoot is null) return;

        var desc = rdfRoot.Elements(rdf + "Description").FirstOrDefault(d => d.Attribute(rdf + "about") is not null)
                   ?? new XElement(rdf + "Description", new XAttribute(rdf + "about", ""));
        if (!rdfRoot.Elements(rdf + "Description").Contains(desc))
            rdfRoot.Add(desc);
        if (desc.Attribute(XNamespace.Xmlns + "pdfaid") is null)
            desc.Add(new XAttribute(XNamespace.Xmlns + "pdfaid", pdfaid.NamespaceName));

        var (part, conformance) = profile switch
        {
            PdfAProfile.PdfA1B => ("1", "B"),
            PdfAProfile.PdfA1A => ("1", "A"),
            PdfAProfile.PdfA2B => ("2", "B"),
            PdfAProfile.PdfA2U => ("2", "U"),
            PdfAProfile.PdfA3B => ("3", "B"),
            _ => ("1", "B")
        };

        XmpDocumentHelper.SetOrAdd(desc, pdfaid + "part", part);
        XmpDocumentHelper.SetOrAdd(desc, pdfaid + "conformance", conformance);
    }

    private static void FixAnnotationFlags(List<PdfIndirectObject> objects, PdfDocumentCore core)
    {
        for (var page = 1; page <= core.PageCount; page++)
        {
            var pageDict = core.GetPage(page);
            var annots = core.ResolveAnnots(pageDict);
            if (annots is null) continue;

            foreach (var idx in annots.Elements
                         .OfType<PdfIndirectReference>()
                         .Select(r => objects.FindIndex(o => o.ObjectNumber == r.ObjectNumber))
                         .Where(static idx => idx >= 0))
            {
                if (objects[idx].Value is not PdfDictionary annot)
                    continue;

                var subtype = annot.GetName(PdfName.Subtype.Value) ?? string.Empty;
                if (subtype == PdfName.Widget.Value)
                    continue;

                var flags = (int)(annot.Get<PdfInteger>(PdfName.F.Value)?.Value ?? 0);
                if ((flags & 4) != 0)
                    continue;

                var updated = new Dictionary<string, PdfObject>(annot.Entries)
                {
                    ["F"] = new PdfInteger(flags | 4)
                };
                objects[idx] = new PdfIndirectObject(objects[idx].ObjectNumber, objects[idx].Generation, new PdfDictionary(updated));
            }
        }
    }
}
