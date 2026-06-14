using System.Buffers;
using System.Text;
using System.Xml.Linq;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;
using Unchained.Pdf.Writing;
using SaveOptions = System.Xml.Linq.SaveOptions;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Converts a document to a PDF/X-conformant structure (ISO 15930): adds an
///     <c>/OutputIntents</c> array describing the target print condition with a
///     <c>/GTS_PDFX</c> subtype, a <c>GTS_PDFXVersion</c> marker, and pdfxid XMP metadata.
///     This applies the required structural markers; it does not perform colour conversion
///     (e.g. RGB→CMYK), which would require an ICC colour-management engine.
/// </summary>
internal static class PdfXConverter
{
    internal static byte[] Convert(PdfDocumentCore core, PdfXProfile profile, string outputConditionIdentifier)
    {
        if (core.IsEncrypted)
            throw new InvalidOperationException("Cannot convert an encrypted PDF to PDF/X. Decrypt first.");

        var objects = core.CollectObjects().ToList();
        var maxObj = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;

        var catalogObjNum = (core.Trailer[PdfName.Root] as PdfIndirectReference)?.ObjectNumber ?? 0;
        var catalogIdx = objects.FindIndex(o => o.ObjectNumber == catalogObjNum);
        if (catalogIdx < 0)
            throw new InvalidOperationException("Cannot locate catalog object.");

        var catalogDict = objects[catalogIdx].Value as PdfDictionary ?? throw new InvalidOperationException("Catalog is not a dictionary.");
        var catalogEntries = new Dictionary<string, PdfObject>(catalogDict.Entries);

        // ── 1. /OutputIntents with a GTS_PDFX intent ──────────────────────────
        var intentObjNum = maxObj + 1;
        var intentDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.OutputIntent,
            [PdfName.S.Value] = PdfName.GTS_PDFX,
            [PdfName.OutputConditionIdentifier.Value] = PdfString.FromLatin1(outputConditionIdentifier),
            [PdfName.Info.Value] = PdfString.FromLatin1(outputConditionIdentifier),
            [PdfName.RegistryName.Value] = PdfString.FromLatin1("http://www.color.org")
        });
        objects.Add(new PdfIndirectObject(intentObjNum, 0, intentDict));
        catalogEntries["OutputIntents"] = new PdfArray([new PdfIndirectReference(intentObjNum, 0)]);

        // ── 2. /Metadata with pdfxid XMP ──────────────────────────────────────
        var metaObjNum = maxObj + 2;
        var xmpBytes = BuildPdfXXmp(profile, catalogEntries, core);
        var metaDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.Metadata,
            [PdfName.Subtype.Value] = PdfName.XML,
            [PdfName.Length.Value] = new PdfInteger(xmpBytes.Length)
        });
        objects.Add(new PdfIndirectObject(metaObjNum, 0, new PdfStream(metaDict, xmpBytes.ToArray())));
        catalogEntries["Metadata"] = new PdfIndirectReference(metaObjNum, 0);

        // ── 3. Info dict needs GTS_PDFXVersion + a Title (PDF/X requires both) ──
        var infoRef = core.Trailer[PdfName.Info] as PdfIndirectReference;
        var infoObjNum = infoRef?.ObjectNumber ?? (maxObj + 3);
        var infoEntries = new Dictionary<string, PdfObject>();
        if (infoRef is not null)
        {
            if (core.ResolveIndirect(infoRef.ObjectNumber).Value is PdfDictionary existingInfo)
                infoEntries = new Dictionary<string, PdfObject>(existingInfo.Entries);
        }

        infoEntries["GTS_PDFXVersion"] = PdfString.FromLatin1(VersionString(profile));
        if (!infoEntries.ContainsKey("Title"))
            infoEntries["Title"] = PdfString.FromLatin1("Untitled");

        var infoIdx = objects.FindIndex(o => o.ObjectNumber == infoObjNum);
        if (infoIdx >= 0)
            objects[infoIdx] = new PdfIndirectObject(infoObjNum, 0, new PdfDictionary(infoEntries));
        else
            objects.Add(new PdfIndirectObject(infoObjNum, 0, new PdfDictionary(infoEntries)));

        // ── 4. Rebuild catalog ────────────────────────────────────────────────
        objects[catalogIdx] = new PdfIndirectObject(catalogObjNum, 0, new PdfDictionary(catalogEntries));

        // ── 5. Trailer with /Info + /ID ───────────────────────────────────────
        var maxAfter = objects.Max(static o => o.ObjectNumber);
        var trailerEntries = new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(maxAfter + 1),
            [PdfName.Root.Value] = core.Trailer[PdfName.Root]!,
            [PdfName.Info.Value] = new PdfIndirectReference(infoObjNum, 0)
        };
        if (core.Trailer[PdfName.ID.Value] is { } id)
            trailerEntries[PdfName.ID.Value] = id;
        else
        {
            var hex = new string('0', 32);
            trailerEntries[PdfName.ID.Value] = new PdfArray([
                new PdfString(Encoding.Latin1.GetBytes(hex), true),
                new PdfString(Encoding.Latin1.GetBytes(hex), true)
            ]);
        }

        var buf = new ArrayBufferWriter<byte>();
        using var writer = new PdfWriter(buf);
        writer.Write(objects, new PdfDictionary(trailerEntries));
        return buf.WrittenMemory.ToArray();
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

    private static ReadOnlySpan<byte> BuildPdfXXmp(PdfXProfile profile, IReadOnlyDictionary<string, PdfObject> catalogEntries, PdfDocumentCore core)
    {
        var existing = ReadExistingXmp(catalogEntries, core);
        var xmpDoc = (existing is not null ? TryParse(existing) : null) ?? CreateMinimalXmp();

        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace pdfxid = "http://www.npes.org/pdfx/ns/id/";
        var rdfRoot = xmpDoc.Descendants(rdf + "RDF").FirstOrDefault();

        // ReSharper disable once InvertIf
        if (rdfRoot is not null)
        {
            var desc = rdfRoot.Elements(rdf + "Description").FirstOrDefault(d => d.Attribute(rdf + "about") is not null)
                       ?? new XElement(rdf + "Description", new XAttribute(rdf + "about", ""));
            if (!rdfRoot.Elements(rdf + "Description").Contains(desc))
                rdfRoot.Add(desc);
            if (desc.Attribute(XNamespace.Xmlns + "pdfxid") is null)
                desc.Add(new XAttribute(XNamespace.Xmlns + "pdfxid", pdfxid.NamespaceName));
            SetOrAdd(desc, pdfxid + "GTS_PDFXVersion", VersionString(profile));
        }

        return xmpDoc.ToString(SaveOptions.OmitDuplicateNamespaces).ToUtf8Span();
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
            return StreamFilters.Decode(stream).Span.FromUtf8Span();
        }
        catch
        {
            return null;
        }
    }

    private static XDocument? TryParse(string xml)
    {
        try { return XDocument.Parse(xml); }
        catch { return null; }
    }

    private static XDocument CreateMinimalXmp()
    {
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace x = "adobe:ns:meta/";
        return new XDocument(
            new XProcessingInstruction("xpacket", "begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\""),
            new XElement(x + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", x.NamespaceName),
                new XElement(rdf + "RDF", new XAttribute(XNamespace.Xmlns + "rdf", rdf.NamespaceName))),
            new XProcessingInstruction("xpacket", "end=\"w\""));
    }

    private static void SetOrAdd(XContainer parent, XName name, string value)
    {
        var existing = parent.Element(name);
        if (existing is not null) existing.Value = value;
        else parent.Add(new XElement(name, value));
    }
}
