using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Drawings;

namespace Unchained.Xlsx.Parsing;

/// <summary>
///     Parses a worksheet drawing part (<c>xdr:wsDr</c>) into <see cref="WorksheetDrawing" /> models:
///     pictures (resolving the embedded image from <c>xl/media/</c>) and charts (resolving the chart
///     part). Anchors are converted from 0-based <c>xdr:col</c>/<c>xdr:row</c> to the 1-based model.
/// </summary>
internal static class DrawingParser
{
    private static readonly XNamespace Xdr = SmlNames.XDR;
    private static readonly XNamespace A = DmlNames.Dml;
    private static readonly XNamespace R = SmlNames.R;
    private static readonly XNamespace C = SmlNames.CH;

    public static void Parse(
        SpreadsheetDocument document,
        OpcPart drawingPart,
        string drawingUri,
        XElement root,
        DrawingCollection drawings
    )
    {
        foreach (var anchorEl in root.Elements())
        {
            var type = anchorEl.Name.LocalName switch
            {
                "twoCellAnchor" => DrawingAnchorType.TwoCell,
                "absoluteAnchor" => DrawingAnchorType.Absolute,
                _ => DrawingAnchorType.OneCell
            };

            var anchor = ReadAnchor(anchorEl, type);

            var pic = anchorEl.Element(Xdr + "pic");
            if (pic != null)
            {
                var picture = ReadPicture(document, drawingPart, pic, anchor);
                if (picture != null)
                    drawings.Add(picture);
                continue;
            }

            var frame = anchorEl.Element(Xdr + "graphicFrame");
            if (frame == null) continue;

            var chart = ReadChart(document, drawingPart, frame, anchor);
            if (chart != null)
                drawings.Add(chart);
        }
    }

    private static DrawingAnchor ReadAnchor(XContainer anchorEl, DrawingAnchorType type)
    {
        var anchor = new DrawingAnchor { AnchorType = type };

        var from = anchorEl.Element(Xdr + "from");
        if (from != null)
        {
            anchor.From = MarkerCell(from);
            anchor.FromOffsetX = new Emu(MarkerOffset(from, "colOff"));
            anchor.FromOffsetY = new Emu(MarkerOffset(from, "rowOff"));
        }

        var to = anchorEl.Element(Xdr + "to");
        if (to != null)
        {
            anchor.To = MarkerCell(to);
            anchor.ToOffsetX = new Emu(MarkerOffset(to, "colOff"));
            anchor.ToOffsetY = new Emu(MarkerOffset(to, "rowOff"));
        }

        var ext = anchorEl.Element(Xdr + "ext");
        if (ext != null)
        {
            anchor.Width = new Emu(LongAttr(ext, "cx"));
            anchor.Height = new Emu(LongAttr(ext, "cy"));
        }

        var pos = anchorEl.Element(Xdr + "pos");
        if (pos == null) return anchor;

        anchor.OffsetX = new Emu(LongAttr(pos, "x"));
        anchor.OffsetY = new Emu(LongAttr(pos, "y"));

        return anchor;
    }

    private static CellReference MarkerCell(XContainer marker)
    {
        var col = IntElement(marker, "col");
        var row = IntElement(marker, "row");
        // xdr markers are 0-based; the model is 1-based.
        return new CellReference(row + 1, col + 1);
    }

    private static long MarkerOffset(XContainer marker, string name) =>
        long.TryParse(marker.Element(Xdr + name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static int IntElement(XContainer parent, string name) =>
        int.TryParse(parent.Element(Xdr + name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static long LongAttr(XElement el, string name) =>
        long.TryParse((string?)el.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    // ReSharper disable once BadListLineBreaks
    private static PictureDrawing? ReadPicture(SpreadsheetDocument document, OpcPart drawingPart, XContainer pic, DrawingAnchor anchor)
    {
        var embed = (string?)pic.Element(Xdr + "blipFill")?.Element(A + "blip")?.Attribute(R + "embed");
        if (embed == null)
            return null;

        var rel = drawingPart.Relationships.FirstOrDefault(r => r.Id == embed);
        if (rel == null)
            return null;

        var mediaUri = drawingPart.ResolveUri(rel.TargetUri);
        var mediaPart = document.Package?.TryGetPart(mediaUri);
        if (mediaPart == null)
            return null;

        var image = new EmbeddedImage(mediaPart.ContentType, mediaPart.Data) { PartUri = mediaUri };
        var name = (string?)pic.Element(Xdr + "nvPicPr")?.Element(Xdr + "cNvPr")?.Attribute("name") ?? string.Empty;

        return new PictureDrawing(image)
        {
            Anchor = anchor,
            Name = name,
            RelationshipId = embed,
            MediaPartUri = mediaUri
        };
    }

    // ReSharper disable once BadListLineBreaks
    private static ChartDrawing? ReadChart(SpreadsheetDocument document, OpcPart drawingPart, XContainer frame, DrawingAnchor anchor)
    {
        var chartEl = frame.Element(A + "graphic")?.Element(A + "graphicData")?.Element(C + "chart");
        var relId = (string?)chartEl?.Attribute(R + "id");
        if (relId == null)
            return null;

        var rel = drawingPart.Relationships.FirstOrDefault(r => r.Id == relId);
        if (rel == null)
            return null;

        var chartUri = drawingPart.ResolveUri(rel.TargetUri);
        var chartPart = document.Package?.TryGetPart(chartUri);
        if (chartPart == null)
            return null;

        var name = (string?)frame.Element(Xdr + "nvGraphicFramePr")?.Element(Xdr + "cNvPr")?.Attribute("name") ?? string.Empty;

        return new ChartDrawing
        {
            Anchor = anchor,
            Name = name,
            RelationshipId = relId,
            ChartPartUri = chartUri,
            ChartPartData = chartPart.Data,
            Chart = ChartXml.Parse(OoXmlHelper.ParseXml(chartPart.Data).Root)
        };
    }
}
