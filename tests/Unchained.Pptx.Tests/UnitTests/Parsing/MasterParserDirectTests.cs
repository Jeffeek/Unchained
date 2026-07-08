using System.Text;
using Shouldly;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Parsing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

/// <summary>
///     Branch coverage for <see cref="MasterParser" />: missing part stub, null root, theme
///     relationship present vs absent (and dangling), shape tree present vs absent, and layout
///     relationship enumeration.
/// </summary>
public sealed class MasterParserDirectTests
{
    private const string MasterUri = "/ppt/slideMasters/slideMaster1.xml";
    private const string ThemeUri = "/ppt/theme/theme1.xml";
    private const string LayoutUri = "/ppt/slideLayouts/slideLayout1.xml";

    private const string MasterContentType =
        "application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml";
    private const string LayoutContentType =
        "application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml";

    private static OpcPackage PackageWith(
        string masterXml,
        bool withTheme = false,
        bool withDanglingTheme = false,
        bool withLayout = false
    )
    {
        var package = OpcPackage.CreateEmpty();
        package.AddOrReplacePart(MasterUri, MasterContentType, Encoding.UTF8.GetBytes(masterXml));

        if (withTheme)
        {
            package.AddOrReplacePart(ThemeUri, OoxmlContentTypes.Theme, Encoding.UTF8.GetBytes(ThemeXml()));
            package.AddRelationship(MasterUri, "rIdTheme", PmlNames.RelTypeTheme, "../theme/theme1.xml");
        }

        if (withDanglingTheme)
            package.AddRelationship(MasterUri, "rIdTheme", PmlNames.RelTypeTheme, "../theme/missing.xml");

        // ReSharper disable once InvertIf
        if (withLayout)
        {
            package.AddOrReplacePart(LayoutUri, LayoutContentType, Encoding.UTF8.GetBytes(LayoutXml()));
            package.AddRelationship(MasterUri, "rIdLayout", PmlNames.RelTypeSlideLayout, "../slideLayouts/slideLayout1.xml");
        }

        return package;
    }

    private static string MasterXml(bool withShapeTree) =>
        "<p:sldMaster xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\">" +
        (withShapeTree ? "<p:cSld><p:spTree></p:spTree></p:cSld>" : "<p:cSld></p:cSld>") +
        "</p:sldMaster>";

    private static string ThemeXml() =>
        "<a:theme xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" name=\"TestTheme\">" +
        "<a:themeElements><a:clrScheme name=\"X\"></a:clrScheme></a:themeElements></a:theme>";

    private static string LayoutXml() =>
        "<p:sldLayout xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" " +
        "name=\"L1\" type=\"title\"><p:cSld><p:spTree></p:spTree></p:cSld></p:sldLayout>";

    [Fact]
    public void Parse_MissingPart_ReturnsStubMaster()
    {
        var master = new MasterParser(OpcPackage.CreateEmpty()).Parse(MasterUri, "rId1");
        master.PartUri.ShouldBe(MasterUri);
        master.RelationshipId.ShouldBe("rId1");
        master.Layouts.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_NoThemeNoShapeTreeNoLayouts_ReturnsMinimalMaster()
    {
        var master = new MasterParser(PackageWith(MasterXml(false))).Parse(MasterUri, "rId3");
        // No theme relationship → the theme block is skipped and the default theme is left in place.
        master.Theme.ShouldNotBeNull();
        master.Shapes.ShouldBeEmpty();
        master.Layouts.ShouldBeEmpty();
        master.RawElement.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_WithTheme_PopulatesTheme()
    {
        var master = new MasterParser(PackageWith(MasterXml(true), true)).Parse(MasterUri, "rId4");
        master.Theme.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_ThemeRelationshipButMissingPart_LeavesDefaultTheme()
    {
        // The theme relationship resolves to no part, so ThemeParser is never invoked.
        var master = new MasterParser(PackageWith(MasterXml(true), withDanglingTheme: true)).Parse(MasterUri, "rId5");
        master.Theme.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_WithShapeTree_ParsesShapesContainer()
    {
        var master = new MasterParser(PackageWith(MasterXml(true))).Parse(MasterUri, "rId6");
        master.RawElement.ShouldNotBeNull();
        master.Shapes.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_WithLayout_AddsLayoutLinkedToMaster()
    {
        var master = new MasterParser(PackageWith(MasterXml(true), withLayout: true)).Parse(MasterUri, "rId7");
        master.Layouts.Count.ShouldBe(1);
        master.Layouts[0].Master.ShouldBeSameAs(master);
    }

    [Fact]
    public void Parse_FullMaster_ThemeShapesAndLayouts()
    {
        var master = new MasterParser(
            PackageWith(MasterXml(true), true, withLayout: true)
        ).Parse(MasterUri, "rId8");
        master.Theme.ShouldNotBeNull();
        master.Layouts.Count.ShouldBe(1);
    }
}
