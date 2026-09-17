using Shouldly;
using System.Xml.Linq;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Parsing;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

/// <summary>
///     Tests for <see cref="DrawingParser" /> malformed-input guards — pictures and charts whose
///     blip/relationship/part references are missing are skipped rather than producing a drawing —
///     plus a positive control proving a well-formed drawing round-trips into a picture.
/// </summary>
public sealed class DrawingParserTests
{
    private const string DrawingUri = "/xl/drawings/drawing1.xml";

    // A 1×1 PNG, enough for a valid embedded image.
    private static readonly byte[] TinyPng =
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    private static XElement WsDr(XElement anchorContent) =>
        new(SmlNames.XDR + "wsDr", new XElement(SmlNames.XDR + "twoCellAnchor", anchorContent));

    private static (SpreadsheetDocument Document, OpcPart Part, DrawingCollection Drawings) Fixture()
    {
        using var processor = new SpreadsheetProcessor();
        var document = processor.CreateBlank("Sheet1");
        var part = new OpcPart(DrawingUri, "application/vnd.openxmlformats-officedocument.drawing+xml", []);
        return (document, part, []);
    }

    private static void Parse(XElement anchorContent, SpreadsheetDocument document, OpcPart part, DrawingCollection drawings) =>
        DrawingParser.Parse(document, part, DrawingUri, WsDr(anchorContent), drawings);

    [Fact]
    public void Parse_PictureWithoutBlipEmbed_IsSkipped()
    {
        var (document, part, drawings) = Fixture();

        Parse(new XElement(SmlNames.XDR + "pic"), document, part, drawings);

        drawings.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_PictureWithMissingRelationship_IsSkipped()
    {
        var (document, part, drawings) = Fixture();
        var pic = new XElement(
            SmlNames.XDR + "pic",
            new XElement(SmlNames.XDR + "blipFill", new XElement(DmlNames.Dml + "blip", new XAttribute(SmlNames.R + "embed", "rIdMissing")))
        );

        Parse(pic, document, part, drawings);

        drawings.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_ChartFrameWithoutRelId_IsSkipped()
    {
        var (document, part, drawings) = Fixture();
        var frame = new XElement(
            SmlNames.XDR + "graphicFrame",
            new XElement(DmlNames.Dml + "graphic", new XElement(DmlNames.Dml + "graphicData", new XElement(CmlNames.Chart)))
        );

        Parse(frame, document, part, drawings);

        drawings.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_ChartFrameWithMissingRelationship_IsSkipped()
    {
        var (document, part, drawings) = Fixture();
        var frame = ChartFrame("rIdNoRel");

        Parse(frame, document, part, drawings);

        drawings.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_ChartFrameWithMissingChartPart_IsSkipped()
    {
        var (document, part, drawings) = Fixture();
        part.AddRelationship(new OpcRelationship("rIdChart", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart", "chart1.xml"));

        Parse(ChartFrame("rIdChart"), document, part, drawings);

        drawings.Count.ShouldBe(0);
    }

    private static XElement ChartFrame(string relId) =>
        new(
            SmlNames.XDR + "graphicFrame",
            new XElement(
                DmlNames.Dml + "graphic",
                new XElement(DmlNames.Dml + "graphicData", new XElement(CmlNames.Chart, new XAttribute(SmlNames.R + "id", relId)))
            )
        );

    // ── Positive control ────────────────────────────────────────────────────────
    // Feeds a real, well-formed drawing part (produced by AddImage + save + reload) back through
    // the parser: proves the happy path adds a picture, so the negative tests above genuinely reach
    // their intended guards rather than tripping an earlier one on a mis-namespaced element.

    [Fact]
    public async Task Parse_WellFormedDrawingPart_AddsPicture()
    {
        using var document = await LoadDocumentWithDrawingAsync();
        var drawingPart = document.Package!.Parts.First(static p => p.ContentType == SmlNames.ContentTypeDrawing);
        var root = XDocument.Load(new MemoryStream(drawingPart.Data)).Root!;
        var drawings = new DrawingCollection();

        DrawingParser.Parse(document, drawingPart, drawingPart.Uri, root, drawings);

        drawings.Count.ShouldBe(1);
        var picture = drawings.Pictures.ShouldHaveSingleItem();
        picture.Image.ContentType.ShouldBe("image/png");
        picture.Anchor.From.ShouldBe(CellReference.FromA1("B2"));
    }

    private static async Task<SpreadsheetDocument> LoadDocumentWithDrawingAsync()
    {
        byte[] bytes;
        using (var writer = new SpreadsheetProcessor())
        {
            using var built = writer.CreateBlank("Sheet1");
            built.Sheets[0].AddImage(TinyPng, "image/png", CellReference.FromA1("B2"));
            using var ms = new MemoryStream();
            await writer.SaveAsync(built, ms, cancellationToken: TestContext.Current.CancellationToken);
            bytes = ms.ToArray();
        }

        using var reader = new SpreadsheetProcessor();
        return await reader.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
    }
}
