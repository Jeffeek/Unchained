using System.Text;
using Shouldly;
using Unchained.Ooxml.Opc;
using Unchained.Pptx.Models.Themes;
using Unchained.Pptx.Parsing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

/// <summary>
///     Branch coverage for <see cref="LayoutParser" />: missing part, null root, missing shape tree,
///     and every <c>ST_SlideLayoutType</c> token mapped by <c>ParseLayoutType</c>.
/// </summary>
public sealed class LayoutParserTests
{
    private const string PartUri = "/ppt/slideLayouts/slideLayout1.xml";
    private const string ContentType =
        "application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml";

    private static OpcPackage PackageWith(string xml)
    {
        var package = OpcPackage.CreateEmpty();
        package.AddOrReplacePart(PartUri, ContentType, Encoding.UTF8.GetBytes(xml));
        return package;
    }

    private static string LayoutXml(string? type, bool withShapeTree)
    {
        var typeAttr = type == null ? string.Empty : $" type=\"{type}\"";
        var inner = withShapeTree
            ? "<p:cSld><p:spTree></p:spTree></p:cSld>"
            : "<p:cSld></p:cSld>";
        return "<p:sldLayout xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" " +
               $"name=\"Layout One\"{typeAttr}>{inner}</p:sldLayout>";
    }

    [Fact]
    public void Parse_MissingPart_ReturnsStubLayout()
    {
        var parser = new LayoutParser(OpcPackage.CreateEmpty());
        var layout = parser.Parse(PartUri, "rId1");
        layout.PartUri.ShouldBe(PartUri);
        layout.RelationshipId.ShouldBe("rId1");
        layout.Shapes.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_NoShapeTree_ReturnsLayoutWithNameAndType()
    {
        var parser = new LayoutParser(PackageWith(LayoutXml("blank", false)));
        var layout = parser.Parse(PartUri, "rId3");
        layout.Name.ShouldBe("Layout One");
        layout.LayoutType.ShouldBe(LayoutType.Blank);
        layout.Shapes.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_WithShapeTree_ParsesShapes()
    {
        var parser = new LayoutParser(PackageWith(LayoutXml("title", true)));
        var layout = parser.Parse(PartUri, "rId4");
        layout.LayoutType.ShouldBe(LayoutType.Title);
        layout.RawElement.ShouldNotBeNull();
    }

    [
        Theory,
        InlineData("blank", LayoutType.Blank),
        InlineData("title", LayoutType.Title),
        InlineData("tx", LayoutType.TitleAndContent),
        InlineData("obj", LayoutType.TitleAndContent),
        InlineData("twoObj", LayoutType.TitleAndContent),
        InlineData("twoTxTwoObj", LayoutType.TitleAndTwoContent),
        InlineData("titleOnly", LayoutType.TitleOnly),
        InlineData("secHead", LayoutType.SectionHeader),
        InlineData("twoTx", LayoutType.TwoTextColumns),
        InlineData("vertTx", LayoutType.TitleAndVerticalText),
        InlineData("picTx", LayoutType.PictureWithCaption),
        InlineData("ctrTitle", LayoutType.TitleSlide),
        InlineData("somethingElse", LayoutType.Custom),
        InlineData("", LayoutType.Custom)
    ]
    public void Parse_MapsLayoutTypeTokens(string token, LayoutType expected)
    {
        var parser = new LayoutParser(PackageWith(LayoutXml(token, true)));
        parser.Parse(PartUri, "rId5").LayoutType.ShouldBe(expected);
    }
}
