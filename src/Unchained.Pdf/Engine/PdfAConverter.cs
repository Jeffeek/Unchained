using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;
using Unchained.Pdf.Writing;
using SaveOptions = System.Xml.Linq.SaveOptions;

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
internal static class PdfAConverter
{
    internal static byte[] Convert(PdfDocumentCore core, PdfAProfile profile)
    {
        if (core.IsEncrypted)
            throw new InvalidOperationException("Cannot convert an encrypted PDF to PDF/A. Decrypt first.");

        var objects = core.CollectObjects().ToList();
        var maxObj = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;

        // Track modifications to the catalog entries
        var catalogObjNum = (core.Trailer[PdfName.Root] as PdfIndirectReference)?.ObjectNumber ?? 0;
        var catalogIdx = objects.FindIndex(o => o.ObjectNumber == catalogObjNum);
        if (catalogIdx < 0)
            throw new InvalidOperationException("Cannot locate catalog object.");

        var catalogDict = objects[catalogIdx].Value as PdfDictionary ?? throw new InvalidOperationException("Catalog is not a dictionary.");

        var catalogEntries = new Dictionary<string, PdfObject>(catalogDict.Entries);

        // ── 1. Add/update /Metadata with pdfaid XMP ───────────────────────────
        var metaObjNum = maxObj + 1;
        var xmpBytes = BuildPdfAXmp(profile, catalogEntries, core);
        var metaDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("Metadata"),
            ["Subtype"] = PdfName.Get("XML"),
            ["Length"] = new PdfInteger(xmpBytes.Length)
        });
        objects.Add(new PdfIndirectObject(metaObjNum, 0, new PdfStream(metaDict, xmpBytes)));
        catalogEntries["Metadata"] = new PdfIndirectReference(metaObjNum, 0);

        // ── 2. Remove /AA from catalog (prohibited additional actions) ─────────
        catalogEntries.Remove("AA");

        // ── 3. Remove /Collection (PDF Portfolio) ─────────────────────────────
        catalogEntries.Remove("Collection");

        // ── 4. Rebuild catalog ────────────────────────────────────────────────
        objects[catalogIdx] = new PdfIndirectObject(catalogObjNum, 0, new PdfDictionary(catalogEntries));

        // ── 5. Fix annotation Print flags across all pages ────────────────────
        FixAnnotationFlags(objects, core);

        // ── 6. Build trailer with /ID (required by PDF/A) ─────────────────────
        var trailer = BuildTrailerWithId(objects, core);

        // ── 7. Serialize ──────────────────────────────────────────────────────
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new PdfWriter(buf);
        writer.Write(objects, trailer);
        return buf.WrittenMemory.ToArray();
    }

    // ── XMP ───────────────────────────────────────────────────────────────────

    private static byte[] BuildPdfAXmp(PdfAProfile profile, IReadOnlyDictionary<string, PdfObject> catalogEntries, PdfDocumentCore core)
    {
        // Try to preserve existing XMP
        var existing = ReadExistingXmp(catalogEntries, core);
        var xmpDoc = existing is not null
            ? TryParse(existing) ?? CreateMinimalXmp()
            : CreateMinimalXmp();

        SetPdfaidProperties(xmpDoc, profile);

        return Encoding.UTF8.GetBytes(xmpDoc.ToString(SaveOptions.OmitDuplicateNamespaces));
    }

    private static string? ReadExistingXmp(IReadOnlyDictionary<string, PdfObject> catalogEntries, PdfDocumentCore core)
    {
        var metaObj = catalogEntries.GetValueOrDefault("Metadata");
        var stream = metaObj switch
        {
            PdfStream s => s,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
            _ => null
        };

        if (stream is null)
            return null;

        try
        {
            return Encoding.UTF8.GetString(StreamFilters.Decode(stream).Span);
        }
        catch
        {
            return null;
        }
    }

    private static XDocument? TryParse(string xml)
    {
        try
        {
            return XDocument.Parse(xml);
        }
        catch
        {
            return null;
        }
    }

    private static XDocument CreateMinimalXmp()
    {
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace x = "adobe:ns:meta/";
        return new XDocument(
            // ReSharper disable StringLiteralTypo
            new XProcessingInstruction("xpacket", "begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\""),
            // ReSharper restore StringLiteralTypo
            new XElement(x + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", x.NamespaceName),
                new XElement(rdf + "RDF", new XAttribute(XNamespace.Xmlns + "rdf", rdf.NamespaceName))),
            new XProcessingInstruction("xpacket", "end=\"w\""));
    }

    private static void SetPdfaidProperties(XContainer xmpDoc, PdfAProfile profile)
    {
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace pdfaid = "http://www.aiim.org/pdfa/ns/id/";

        var rdfRoot = xmpDoc.Descendants(rdf + "RDF").FirstOrDefault();
        if (rdfRoot is null) return;

        // Find or create a rdf:Description that holds pdfaid properties
        var desc = rdfRoot.Elements(rdf + "Description").FirstOrDefault(d => d.Attribute(rdf + "about") is not null)
                   ?? new XElement(rdf + "Description", new XAttribute(rdf + "about", ""));

        if (!rdfRoot.Elements(rdf + "Description").Contains(desc))
            rdfRoot.Add(desc);

        // Ensure pdfaid namespace is declared
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

        SetOrAdd(desc, pdfaid + "part", part);
        SetOrAdd(desc, pdfaid + "conformance", conformance);
    }

    private static void SetOrAdd(XContainer parent, XName name, string value)
    {
        var existing = parent.Element(name);

        if (existing is not null)
            existing.Value = value;
        else
            parent.Add(new XElement(name, value));
    }

    // ── Annotation Print flag ─────────────────────────────────────────────────

    private static void FixAnnotationFlags(List<PdfIndirectObject> objects, PdfDocumentCore core)
    {
        for (var page = 1; page <= core.PageCount; page++)
        {
            var pageDict = core.GetPage(page);
            var annotsObj = pageDict[PdfName.Annots];
            var annots = annotsObj switch
            {
                PdfArray a => a,
                PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfArray,
                _ => null
            };
            if (annots is null) continue;

            foreach (var elem in annots.Elements)
            {
                if (elem is not PdfIndirectReference r)
                    continue;

                var idx = objects.FindIndex(o => o.ObjectNumber == r.ObjectNumber);
                if (idx < 0)
                    continue;

                if (objects[idx].Value is not PdfDictionary annot)
                    continue;

                var subtype = annot.GetName("Subtype") ?? string.Empty;
                if (subtype == "Widget")
                    continue; // Widget flags differ

                var flags = (int)(annot.Get<PdfInteger>("F")?.Value ?? 0);
                if ((flags & 4) != 0)
                    continue; // Print bit already set

                // Set Print bit (bit 3 = value 4)
                var updated = new Dictionary<string, PdfObject>(annot.Entries)
                {
                    ["F"] = new PdfInteger(flags | 4)
                };
                objects[idx] = new PdfIndirectObject(objects[idx].ObjectNumber, objects[idx].Generation, new PdfDictionary(updated));
            }
        }
    }

    // ── Trailer ───────────────────────────────────────────────────────────────

    private static PdfDictionary BuildTrailerWithId(IEnumerable<PdfIndirectObject> objects, PdfDocumentCore core)
    {
        var maxObj = objects.Max(static o => o.ObjectNumber);
        var entries = new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(maxObj + 1),
            [PdfName.Root.Value] = core.Trailer[PdfName.Root]!
        };

        if (core.Trailer[PdfName.Info] is { } info)
            entries[PdfName.Info.Value] = info;

        // /ID is required by PDF/A — preserve existing or generate new
        var existingId = core.Trailer.Get<PdfArray>(PdfName.Get("ID"));
        entries["ID"] = existingId
                        ?? new PdfArray([
                            new PdfString(RandomNumberGenerator.GetBytes(16), true),
                            new PdfString(RandomNumberGenerator.GetBytes(16), true)
                        ]);

        return new PdfDictionary(entries);
    }
}
