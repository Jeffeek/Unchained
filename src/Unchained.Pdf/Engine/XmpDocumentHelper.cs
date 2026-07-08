using System.Xml.Linq;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Shared XMP-metadata helpers used by the compliance converters
///     (<see cref="PdfAConverter" /> and <see cref="PdfXConverter" />): reading the existing
///     XMP packet from the catalog, lenient parsing, building a minimal packet, and the
///     set-or-add element primitive.
/// </summary>
internal static class XmpDocumentHelper
{
    internal static string? ReadExistingXmp(IReadOnlyDictionary<string, PdfObject> catalogEntries, PdfDocumentCore core)
    {
        var metaObj = catalogEntries.GetValueOrDefault("Metadata");
        var stream = core.ResolveStream(metaObj);

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

    internal static XDocument? TryParse(string xml)
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

    internal static XDocument CreateMinimalXmp()
    {
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace x = "adobe:ns:meta/";
        return new XDocument(
            // ReSharper disable StringLiteralTypo
            new XProcessingInstruction("xpacket", "begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\""),
            // ReSharper restore StringLiteralTypo
            new XElement(
                x + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", x.NamespaceName),
                new XElement(rdf + "RDF", new XAttribute(XNamespace.Xmlns + "rdf", rdf.NamespaceName))
            ),
            new XProcessingInstruction("xpacket", "end=\"w\"")
        );
    }

    internal static void SetOrAdd(XContainer parent, XName name, string value)
    {
        var existing = parent.Element(name);

        if (existing is not null)
            existing.Value = value;
        else
            parent.Add(new XElement(name, value));
    }
}
