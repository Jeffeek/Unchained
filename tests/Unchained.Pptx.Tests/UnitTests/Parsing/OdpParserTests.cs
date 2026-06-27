using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Pptx.Core;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

public sealed class OdpParserTests
{
    private const string Mime = "application/vnd.oasis.opendocument.presentation";

    private const string NsDecls =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
        "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
        "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
        "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
        "xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" " +
        "xmlns:xlink=\"http://www.w3.org/1999/xlink\"";

    private static byte[] BuildOdp(
        string contentXml,
        string? stylesXml = null,
        string? metaXml = null,
        (string Name, byte[] Bytes)? media = null,
        string mimetype = Mime
    )
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            Write(zip, "mimetype", Encoding.ASCII.GetBytes(mimetype));
            Write(zip, "content.xml", Encoding.UTF8.GetBytes(contentXml));
            if (stylesXml != null) Write(zip, "styles.xml", Encoding.UTF8.GetBytes(stylesXml));
            if (metaXml != null) Write(zip, "meta.xml", Encoding.UTF8.GetBytes(metaXml));
            if (media is { } m) Write(zip, m.Name, m.Bytes);
        }

        return ms.ToArray();

        static void Write(ZipArchive zip, string name, byte[] bytes)
        {
            var entry = zip.CreateEntry(name);
            using var s = entry.Open();
            s.Write(bytes, 0, bytes.Length);
        }
    }

    private static string Content(string pagesInner) =>
        $"<office:document-content {NsDecls}><office:body><office:presentation>{pagesInner}</office:presentation></office:body></office:document-content>";

    [Fact]
    public void IsOdp_ValidPackage_ReturnsTrue()
    {
        var odp = BuildOdp(Content(string.Empty));
        OdpParser.IsOdp(odp).ShouldBeTrue();
    }

    [Fact]
    public void IsOdp_NoMimetype_ReturnsFalse()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var e = zip.CreateEntry("content.xml");
            using var s = e.Open();
            s.Write("<x/>"u8);
        }

        OdpParser.IsOdp(ms.ToArray()).ShouldBeFalse();
    }

    [Fact]
    public void IsOdp_NotAZip_ReturnsFalse() =>
        OdpParser.IsOdp("not a zip"u8.ToArray()).ShouldBeFalse();

    [Fact]
    public void IsOdp_WrongMimetype_ReturnsFalse()
    {
        var odp = BuildOdp(Content(string.Empty), mimetype: "application/zip");
        OdpParser.IsOdp(odp).ShouldBeFalse();
    }

    [Fact]
    public void Parse_NoContent_Throws()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var e = zip.CreateEntry("mimetype");
            using var s = e.Open();
            s.Write(Encoding.ASCII.GetBytes(Mime));
        }

        Should.Throw<PptxException>(() => OdpParser.Parse(ms.ToArray()));
    }

    [Fact]
    public void Parse_PageWithTextBoxSpan_ReadsRun()
    {
        const string page = "<draw:page draw:name=\"Slide 1\">" +
                            "<draw:frame svg:x=\"1cm\" svg:y=\"2cm\" svg:width=\"5cm\" svg:height=\"3cm\">" +
                            "<draw:text-box><text:p><text:span>Hello ODP</text:span></text:p></draw:text-box>" +
                            "</draw:frame></draw:page>";
        var doc = OdpParser.Parse(BuildOdp(Content(page)));

        doc.Slides.Count.ShouldBe(1);
        doc.Slides[0].Name.ShouldBe("Slide 1");
        var shape = doc.Slides[0].Shapes.OfType<AutoShape>().Single();
        shape.TextFrame.PlainText.ShouldContain("Hello ODP");
    }

    [Fact]
    public void Parse_TextBoxWithDirectText_ReadsParagraph()
    {
        const string page = "<draw:page><draw:frame svg:width=\"5cm\" svg:height=\"3cm\">" +
                            "<draw:text-box><text:p>Bare text</text:p></draw:text-box>" +
                            "</draw:frame></draw:page>";
        var doc = OdpParser.Parse(BuildOdp(Content(page)));

        var shape = doc.Slides[0].Shapes.OfType<AutoShape>().Single();
        shape.TextFrame.PlainText.ShouldContain("Bare text");
    }

    [Fact]
    public void Parse_FrameWithoutTextBox_AddsNoShape()
    {
        const string page = "<draw:page><draw:frame svg:width=\"5cm\" svg:height=\"3cm\"/></draw:page>";
        var doc = OdpParser.Parse(BuildOdp(Content(page)));
        doc.Slides[0].Shapes.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_ImageFrame_AddsPictureShape()
    {
        // 1x1 PNG.
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
        );
        const string page = "<draw:page><draw:frame svg:x=\"0cm\" svg:y=\"0cm\" svg:width=\"2cm\" svg:height=\"2cm\">" +
                            "<draw:image xlink:href=\"Pictures/img1.png\"/></draw:frame></draw:page>";
        var doc = OdpParser.Parse(BuildOdp(Content(page), media: ("Pictures/img1.png", png)));

        var pic = doc.Slides[0].Shapes.OfType<PictureShape>().Single();
        pic.Image.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_ImageFrameMissingMedia_AddsNoShape()
    {
        const string page = "<draw:page><draw:frame svg:width=\"2cm\" svg:height=\"2cm\">" +
                            "<draw:image xlink:href=\"Pictures/missing.png\"/></draw:frame></draw:page>";
        var doc = OdpParser.Parse(BuildOdp(Content(page)));
        doc.Slides[0].Shapes.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_HiddenPage_SetsIsHidden()
    {
        const string page = "<draw:page presentation:visibility=\"hidden\"/>";
        var doc = OdpParser.Parse(BuildOdp(Content(page)));
        doc.Slides[0].IsHidden.ShouldBeTrue();
        doc.Properties.HiddenSlideCount.ShouldBe(1);
    }

    [Fact]
    public void Parse_StylesPageSize_IsRead()
    {
        const string styles =
            "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
            "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\">" +
            "<office:automatic-styles><style:page-layout>" +
            "<style:page-layout-properties fo:page-width=\"20cm\" fo:page-height=\"15cm\"/>" +
            "</style:page-layout></office:automatic-styles></office:document-styles>";
        var doc = OdpParser.Parse(BuildOdp(Content("<draw:page/>"), styles));

        doc.SlideSize.Width.Value.ShouldBeGreaterThan(0);
        doc.SlideSize.Height.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Parse_MetaProperties_AreRead()
    {
        const string meta =
            "<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" " +
            "xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\">" +
            "<office:meta><dc:title>My Deck</dc:title><meta:initial-creator>Alice</meta:initial-creator></office:meta>" +
            "</office:document-meta>";
        var doc = OdpParser.Parse(BuildOdp(Content("<draw:page/>"), metaXml: meta));

        doc.Properties.Title.ShouldBe("My Deck");
        doc.Properties.Author.ShouldBe("Alice");
    }

    [
        Theory,
        InlineData("Pictures/a.png"),
        InlineData("Pictures/a.jpg"),
        InlineData("Pictures/a.jpeg"),
        InlineData("Pictures/a.gif"),
        InlineData("Pictures/a.bmp"),
        InlineData("Pictures/a.tif"),
        InlineData("Pictures/a.tiff"),
        InlineData("Pictures/a.xyz")
    ]
    public void Parse_ImageExtensions_MapToContentType(string href)
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
        );
        var page =
            $"<draw:page><draw:frame svg:width=\"2cm\" svg:height=\"2cm\">" +
            $"<draw:image xlink:href=\"{href}\"/></draw:frame></draw:page>";
        var doc = OdpParser.Parse(BuildOdp(Content(page), media: (href, png)));

        var pic = doc.Slides[0].Shapes.OfType<PictureShape>().Single();
        pic.Image.ShouldNotBeNull();
    }

    [
        Theory,
        InlineData("10cm"),
        InlineData("100mm"),
        InlineData("4in"),
        InlineData("360pt"),
        InlineData("5"),
        InlineData(""),
        InlineData("bad")
    ]
    public void Parse_VariousLengthUnits_DoNotThrow(string width)
    {
        var page = $"<draw:page><draw:frame svg:width=\"{width}\" svg:height=\"{width}\">" +
                   "<draw:text-box><text:p><text:span>x</text:span></text:p></draw:text-box></draw:frame></draw:page>";
        var doc = OdpParser.Parse(BuildOdp(Content(page)));
        doc.Slides[0].Shapes.OfType<AutoShape>().Count().ShouldBe(1);
    }
}
