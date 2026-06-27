using System.Text;
using Shouldly;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Opc;
using Unchained.Pptx.Core;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Parsing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

/// <summary>
///     Branch coverage for <see cref="PresentationParser" /> driven with hand-built OPC packages:
///     the bad-bytes wrap, the missing-presentation-relationship throw, slide-size parsing, and the
///     embedded-font list (<c>p:embeddedFontLst</c>) variant resolution including dangling relationships.
/// </summary>
public sealed class PresentationParserDirectTests
{
    private const string PresUri = "/ppt/presentation.xml";
    private const string PresContentType =
        "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml";
    private const string FontContentType = "application/x-fontdata";

    private static OpcPackage PackageWith(string presentationXml, bool wirePresentationRel = true)
    {
        var package = OpcPackage.CreateEmpty();
        package.AddOrReplacePart(PresUri, PresContentType, Encoding.UTF8.GetBytes(presentationXml));
        if (wirePresentationRel)
            package.AddPackageRelationship("rIdPres", PmlNames.RelTypePresentation, "ppt/presentation.xml");
        return package;
    }

    [Fact]
    public void Parse_InvalidBytes_WrapsInPptxException() =>
        Should.Throw<PptxException>(static () => PresentationParser.Parse([1, 2, 3, 4]));

    [Fact]
    public void ParsePackage_NoPresentationRelationship_Throws()
    {
        var package = PackageWith("<p:presentation/>", false);
        Should.Throw<PptxException>(() => PresentationParser.ParsePackage(package));
    }

    [Fact]
    public void ParsePackage_SlideSize_IsParsed()
    {
        const string xml =
            "<p:presentation xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\">" +
            "<p:sldSz cx=\"9144000\" cy=\"6858000\"/></p:presentation>";
        var result = PresentationParser.ParsePackage(PackageWith(xml));
        result.SlideSize.Width.Value.ShouldBe(9_144_000);
        result.SlideSize.Height.Value.ShouldBe(6_858_000);
    }

    [Fact]
    public void ParsePackage_NoSlideSize_DefaultsToWidescreen()
    {
        var result = PresentationParser.ParsePackage(PackageWith("<p:presentation xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>"));
        result.SlideSize.ShouldBe(SlideSize.Widescreen);
    }

    [Fact]
    public void ParsePackage_EmbeddedFontList_ResolvesVariants()
    {
        const string r = "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
        const string p = "xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"";
        const string xml = $"<p:presentation {p} {r}>" +
                           "<p:embeddedFontLst>" +
                           "<p:embeddedFont>" +
                           "<p:font typeface=\"Custom Sans\"/>" +
                           "<p:regular r:id=\"rFontReg\"/>" +
                           "<p:bold r:id=\"rFontBold\"/>" +
                           "<p:italic r:id=\"rFontMissing\"/>" + // dangling relationship → skipped
                           "</p:embeddedFont>" +
                           "</p:embeddedFontLst>" +
                           "</p:presentation>";

        var package = PackageWith(xml);
        package.AddOrReplacePart("/ppt/fonts/font1.fntdata", FontContentType, [1, 2, 3]);
        package.AddOrReplacePart("/ppt/fonts/font2.fntdata", FontContentType, [4, 5, 6]);
        package.AddRelationship(PresUri, "rFontReg", PmlNames.RelTypeFont, "fonts/font1.fntdata");
        package.AddRelationship(PresUri, "rFontBold", PmlNames.RelTypeFont, "fonts/font2.fntdata");
        // rFontMissing intentionally has no relationship.

        var result = PresentationParser.ParsePackage(package);

        result.MediaStore.Fonts.Count.ShouldBe(2);
        result.MediaStore.Fonts.ShouldContain(static f => f.Style == EmbeddedFontStyle.Regular);
        result.MediaStore.Fonts.ShouldContain(static f => f.Style == EmbeddedFontStyle.Bold);
        result.MediaStore.Fonts.ShouldAllBe(static f => f.Typeface == "Custom Sans");
    }

    [Fact]
    public void ParsePackage_EmbeddedFontWithoutTypeface_IsSkipped()
    {
        const string p = "xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"";
        const string r = "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
        const string xml = $"<p:presentation {p} {r}>" +
                           "<p:embeddedFontLst><p:embeddedFont><p:regular r:id=\"x\"/></p:embeddedFont></p:embeddedFontLst>" +
                           "</p:presentation>";
        var result = PresentationParser.ParsePackage(PackageWith(xml));
        result.MediaStore.Fonts.ShouldBeEmpty();
    }
}
