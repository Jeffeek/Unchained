using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Models.Drawings;

namespace Unchained.Xlsx.Writing;

/// <summary>
///     Serializes a worksheet's drawing layer (<c>xl/drawings/drawing*.xml</c>) — the <c>xdr:wsDr</c>
///     root holding one anchor per picture or chart. Coordinates are converted from the 1-based
///     <see cref="DrawingAnchor" /> grid to the 0-based <c>xdr:col</c>/<c>xdr:row</c> the format uses.
/// </summary>
internal static class DrawingWriter
{
    private static readonly XNamespace Xdr = SmlNames.XDR;
    private static readonly XNamespace A = DmlNames.Dml;
    private static readonly XNamespace R = SmlNames.R;
    private static readonly XNamespace C = SmlNames.CH;

    public static byte[] Write(IReadOnlyList<WorksheetDrawing> drawings)
    {
        var root = new XElement(
            Xdr + "wsDr",
            new XAttribute(XNamespace.Xmlns + "xdr", Xdr.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "a", A.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", R.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "c", C.NamespaceName)
        );

        foreach (var drawing in drawings)
            root.Add(WriteAnchor(drawing));

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
    }

    private static XElement WriteAnchor(WorksheetDrawing drawing)
    {
        var anchor = drawing.Anchor;
        var element = anchor.AnchorType switch
        {
            DrawingAnchorType.TwoCell => new XElement(
                Xdr + "twoCellAnchor",
                new XAttribute("editAs", "oneCell"),
                WriteMarker("from", anchor.From.Row, anchor.From.Column, anchor.FromOffsetX.Value, anchor.FromOffsetY.Value),
                WriteMarker("to", anchor.To.Row, anchor.To.Column, anchor.ToOffsetX.Value, anchor.ToOffsetY.Value)
            ),

            DrawingAnchorType.Absolute => new XElement(
                Xdr + "absoluteAnchor",
                new XElement(Xdr + "pos", new XAttribute("x", anchor.OffsetX.Value), new XAttribute("y", anchor.OffsetY.Value)),
                new XElement(Xdr + "ext", new XAttribute("cx", anchor.Width.Value), new XAttribute("cy", anchor.Height.Value))
            ),

            _ => new XElement(
                Xdr + "oneCellAnchor",
                WriteMarker("from", anchor.From.Row, anchor.From.Column, anchor.FromOffsetX.Value, anchor.FromOffsetY.Value),
                new XElement(Xdr + "ext", new XAttribute("cx", anchor.Width.Value), new XAttribute("cy", anchor.Height.Value))
            )
        };

        element.Add(WriteContent(drawing));
        element.Add(new XElement(Xdr + "clientData"));
        return element;
    }

    private static XElement WriteMarker(string name, int row1Based, int col1Based, long offsetX, long offsetY) =>
        new(
            Xdr + name,
            new XElement(Xdr + "col", (col1Based - 1).ToString(CultureInfo.InvariantCulture)),
            new XElement(Xdr + "colOff", offsetX.ToString(CultureInfo.InvariantCulture)),
            new XElement(Xdr + "row", (row1Based - 1).ToString(CultureInfo.InvariantCulture)),
            new XElement(Xdr + "rowOff", offsetY.ToString(CultureInfo.InvariantCulture))
        );

    private static XElement WriteContent(WorksheetDrawing drawing) => drawing switch
    {
        PictureDrawing picture => WritePicture(picture),
        ChartDrawing chart => WriteChartFrame(chart),
        _ => new XElement(Xdr + "sp")
    };

    private static XElement WritePicture(WorksheetDrawing picture)
    {
        var name = string.IsNullOrEmpty(picture.Name) ? $"Picture {picture.ShapeId}" : picture.Name;

        return new XElement(
            Xdr + "pic",
            new XElement(
                Xdr + "nvPicPr",
                new XElement(
                    Xdr + "cNvPr",
                    new XAttribute("id", picture.ShapeId.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("name", name)
                ),
                new XElement(
                    Xdr + "cNvPicPr",
                    new XElement(A + "picLocks", new XAttribute("noChangeAspect", "1"))
                )
            ),
            new XElement(
                Xdr + "blipFill",
                new XElement(A + "blip", new XAttribute(R + "embed", picture.RelationshipId)),
                new XElement(A + "stretch", new XElement(A + "fillRect"))
            ),
            new XElement(
                Xdr + "spPr",
                new XElement(
                    A + "xfrm",
                    new XElement(A + "off", new XAttribute("x", "0"), new XAttribute("y", "0")),
                    new XElement(
                        A + "ext",
                        new XAttribute("cx", picture.Anchor.Width.Value),
                        new XAttribute("cy", picture.Anchor.Height.Value)
                    )
                ),
                new XElement(
                    A + "prstGeom",
                    new XAttribute("prst", "rect"),
                    new XElement(A + "avLst")
                )
            )
        );
    }

    private static XElement WriteChartFrame(WorksheetDrawing chart)
    {
        var name = string.IsNullOrEmpty(chart.Name) ? $"Chart {chart.ShapeId}" : chart.Name;

        return new XElement(
            Xdr + "graphicFrame",
            new XAttribute("macro", string.Empty),
            new XElement(
                Xdr + "nvGraphicFramePr",
                new XElement(
                    Xdr + "cNvPr",
                    new XAttribute("id", chart.ShapeId.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("name", name)
                ),
                new XElement(Xdr + "cNvGraphicFramePr")
            ),
            new XElement(
                Xdr + "xfrm",
                new XElement(A + "off", new XAttribute("x", "0"), new XAttribute("y", "0")),
                new XElement(A + "ext", new XAttribute("cx", "0"), new XAttribute("cy", "0"))
            ),
            new XElement(
                A + "graphic",
                new XElement(
                    A + "graphicData",
                    new XAttribute("uri", C.NamespaceName),
                    new XElement(
                        C + "chart",
                        new XAttribute(R + "id", chart.RelationshipId)
                    )
                )
            )
        );
    }
}
