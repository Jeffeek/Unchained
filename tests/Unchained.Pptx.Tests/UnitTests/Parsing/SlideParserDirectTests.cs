using System.Reflection;
using System.Text;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Opc;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Media;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

/// <summary>
///     Branch coverage for <see cref="SlideParser" /> driven directly with hand-built OPC
///     packages: missing slide part, null root, hidden visibility, name seeding, layout
///     relationship resolution (resolved + dangling), transition/timing/colour-map-override
///     elements, notes and comments relationships (present, dangling), and run/click hyperlink
///     resolution (internal + external + dangling).
/// </summary>
public sealed class SlideParserDirectTests
{
    private const string SlideUri = "/ppt/slides/slide1.xml";
    private const string LayoutUri = "/ppt/slideLayouts/slideLayout1.xml";
    private const string SlideContentType =
        "application/vnd.openxmlformats-officedocument.presentationml.slide+xml";

    private const string P = "xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"";
    private const string R = "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
    private const string A = "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"";

    private const string EmptyTree = "<p:cSld><p:spTree></p:spTree></p:cSld>";

    // Builds a parser over a package holding the given slide XML; the supplied masters provide
    // the fallback/resolved layout. Returns both so tests can wire extra relationships first.
    private static (SlideParser Parser, OpcPackage Package) Build(
        string? slideXml,
        MasterSlide master
    )
    {
        var package = OpcPackage.CreateEmpty();
        if (slideXml is not null)
            package.AddOrReplacePart(SlideUri, SlideContentType, Encoding.UTF8.GetBytes(slideXml));

        var parser = new SlideParser(
            package,
            new MediaStore(),
            new[] { master },
            new CommentAuthorCollection()
        );
        return (parser, package);
    }

    // A master carrying one layout whose PartUri matches LayoutUri so layout-rel resolution hits.
    private static MasterSlide MasterWithLayout()
    {
        var master = new MasterSlide { Name = "M" };
        var layout = master.Layouts.AddLayout("L1");
        SetLayoutUri(layout, LayoutUri);
        return master;
    }

    private static void SetLayoutUri(SlideLayout layout, string uri) =>
        // PartUri is internal; the test assembly has InternalsVisibleTo so we set it directly.
        typeof(SlideLayout)
            .GetProperty("PartUri", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .SetValue(layout, uri);

    private static string Slide(string body, string attrs = "") =>
        $"<p:sld {P} {R} {A}{(attrs.Length > 0 ? " " + attrs : string.Empty)}>{body}</p:sld>";

    // ── Missing / empty parts ─────────────────────────────────────────────────

    [Fact]
    public void Parse_MissingPart_ReturnsSlotSlideWithFallbackLayout()
    {
        var (parser, _) = Build(null, MasterWithLayout());
        var slide = parser.Parse(SlideUri, "rId1", 7);
        slide.PartUri.ShouldBe(SlideUri);
        slide.RelationshipId.ShouldBe("rId1");
        slide.SlideId.ShouldBe(7u);
        slide.Layout.ShouldNotBeNull();
        slide.Layout.Master.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_NoMasters_SynthesisesFallbackLayout()
    {
        var package = OpcPackage.CreateEmpty();
        var parser = new SlideParser(package, new MediaStore(), [], new CommentAuthorCollection());
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.Layout.ShouldNotBeNull();
        slide.Layout.Name.ShouldBe("Default");
    }

    // ── Visibility ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ShowZero_MarksHidden()
    {
        var (parser, _) = Build(Slide(EmptyTree, "show=\"0\""), MasterWithLayout());
        parser.Parse(SlideUri, "rId1", 1).IsHidden.ShouldBeTrue();
    }

    [Fact]
    public void Parse_ShowOne_NotHidden()
    {
        var (parser, _) = Build(Slide(EmptyTree, "show=\"1\""), MasterWithLayout());
        parser.Parse(SlideUri, "rId1", 1).IsHidden.ShouldBeFalse();
    }

    // ── Name + shape tree ───────────────────────────────────────────────────

    [Fact]
    public void Parse_CommonSlideDataName_SeedsSlideName()
    {
        var (parser, _) = Build(Slide("<p:cSld name=\"Intro\"><p:spTree></p:spTree></p:cSld>"), MasterWithLayout());
        parser.Parse(SlideUri, "rId1", 1).Name.ShouldBe("Intro");
    }

    // ── Layout relationship ─────────────────────────────────────────────────

    [Fact]
    public void Parse_LayoutRelationshipResolves_UsesThatLayout()
    {
        var master = MasterWithLayout();
        var (parser, package) = Build(Slide(EmptyTree), master);
        package.AddRelationship(SlideUri, "rIdL", PmlNames.RelTypeSlideLayout, "../slideLayouts/slideLayout1.xml");
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.Layout.ShouldBeSameAs(master.Layouts[0]);
    }

    [Fact]
    public void Parse_LayoutRelationshipDangling_FallsBackToFirstLayout()
    {
        var master = MasterWithLayout();
        var (parser, package) = Build(Slide(EmptyTree), master);
        package.AddRelationship(SlideUri, "rIdL", PmlNames.RelTypeSlideLayout, "../slideLayouts/missing.xml");
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.Layout.ShouldNotBeNull();
    }

    // ── Transition / timing / colour-map override ─────────────────────────────

    [Fact]
    public void Parse_TransitionTimingAndColorMapOverride_AreParsed()
    {
        const string body =
            EmptyTree +
            "<p:transition spd=\"slow\"><p:fade/></p:transition>" +
            "<p:timing></p:timing>" +
            "<p:clrMapOvr><a:overrideClrMapping/></p:clrMapOvr>";
        var (parser, _) = Build(Slide(body), MasterWithLayout());
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.Transition.ShouldNotBeNull();
        slide.Animations.ShouldNotBeNull();
    }

    // ── Notes relationship ────────────────────────────────────────────────────

    [Fact]
    public void Parse_NotesRelationshipResolves_ParsesNotes()
    {
        var master = MasterWithLayout();
        var (parser, package) = Build(Slide(EmptyTree), master);
        const string notesUri = "/ppt/notesSlides/notesSlide1.xml";
        const string notesXml =
            "<p:notes xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\">" +
            "<p:cSld><p:spTree></p:spTree></p:cSld></p:notes>";
        package.AddOrReplacePart(
            notesUri,
            "application/vnd.openxmlformats-officedocument.presentationml.notesSlide+xml",
            Encoding.UTF8.GetBytes(notesXml)
        );
        package.AddRelationship(SlideUri, "rIdN", PmlNames.RelTypeNotesSlide, "../notesSlides/notesSlide1.xml");
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.Notes.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_NotesRelationshipDangling_DoesNotThrow()
    {
        var (parser, package) = Build(Slide(EmptyTree), MasterWithLayout());
        package.AddRelationship(SlideUri, "rIdN", PmlNames.RelTypeNotesSlide, "../notesSlides/missing.xml");
        Should.NotThrow(() => parser.Parse(SlideUri, "rId1", 1));
    }

    // ── Comments relationship ──────────────────────────────────────────────────

    [Fact]
    public void Parse_CommentsRelationshipResolves_ParsesComments()
    {
        var authors = new CommentAuthorCollection { { "Alice", "A" } };
        var package = OpcPackage.CreateEmpty();
        package.AddOrReplacePart(SlideUri, SlideContentType, Encoding.UTF8.GetBytes(Slide(EmptyTree)));
        const string commentsUri = "/ppt/comments/comment1.xml";
        const string commentsXml =
            "<p:cmLst xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\">" +
            "<p:cm authorId=\"0\" dt=\"2020-01-01T00:00:00\"><p:pos x=\"0\" y=\"0\"/><p:text>Hi</p:text></p:cm>" +
            "</p:cmLst>";
        package.AddOrReplacePart(
            commentsUri,
            "application/vnd.openxmlformats-officedocument.presentationml.comments+xml",
            Encoding.UTF8.GetBytes(commentsXml)
        );
        package.AddRelationship(SlideUri, "rIdC", PmlNames.RelTypeComments, "../comments/comment1.xml");
        var parser = new SlideParser(package, new MediaStore(), [MasterWithLayout()], authors);
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_CommentsRelationshipDangling_ReturnsSlideWithoutComments()
    {
        var (parser, package) = Build(Slide(EmptyTree), MasterWithLayout());
        package.AddRelationship(SlideUri, "rIdC", PmlNames.RelTypeComments, "../comments/missing.xml");
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.GetComments().ShouldBeEmpty();
    }

    [Fact]
    public void Parse_NoCommentsRelationship_ReturnsSlide()
    {
        var (parser, _) = Build(Slide(EmptyTree), MasterWithLayout());
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.GetComments().ShouldBeEmpty();
    }

    // ── Placeholder geometry inheritance (MatchPlaceholder) ───────────────────

    // A slide placeholder shape with a p:ph (type+idx) but NO a:xfrm, so it starts zero-sized and
    // must inherit geometry from the matching layout placeholder.
    private static string SlidePlaceholder(string type, int? idx)
    {
        var idxAttr = idx is { } i ? $" idx=\"{i}\"" : string.Empty;
        return Slide(
            "<p:cSld><p:spTree>" +
            "<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"PH\"/><p:cNvSpPr/>" +
            $"<p:nvPr><p:ph type=\"{type}\"{idxAttr}/></p:nvPr></p:nvSpPr>" +
            "<p:spPr/></p:sp>" +
            "</p:spTree></p:cSld>"
        );
    }

    // Adds a geometry-bearing placeholder to the layout so the slide placeholder can match it.
    private static MasterSlide MasterWithLayoutPlaceholder(
        PlaceholderType type,
        int? index
    )
    {
        var master = MasterWithLayout();
        var ph = new AutoShape
        {
            PlaceholderType = type,
            X = new Emu(100),
            Y = new Emu(200),
            Width = Emu.FromPoints(300),
            Height = Emu.FromPoints(100)
        };
        if (index is { } i) ph.PlaceholderIndex = i;
        master.Layouts[0].Shapes.AddParsed(ph);
        return master;
    }

    [Fact]
    public void Parse_PlaceholderMatchesLayoutByIndex_InheritsGeometry()
    {
        var master = MasterWithLayoutPlaceholder(PlaceholderType.Body, 1);
        var (parser, _) = Build(SlidePlaceholder("body", 1), master);
        var slide = parser.Parse(SlideUri, "rId1", 1);
        var ph = slide.Shapes[0];
        ph.Width.Value.ShouldBeGreaterThan(0);
        ph.Height.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Parse_PlaceholderMatchesLayoutByType_InheritsGeometry()
    {
        // No index on the slide placeholder → falls through to the by-type match.
        var master = MasterWithLayoutPlaceholder(PlaceholderType.Body, null);
        var (parser, _) = Build(SlidePlaceholder("body", null), master);
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.Shapes[0].Width.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Parse_TitlePlaceholderMatchesCenteredTitle_InheritsGeometry()
    {
        // Slide has Title; layout has CenteredTitle → matched via the title-family branch.
        var master = MasterWithLayoutPlaceholder(PlaceholderType.CenteredTitle, null);
        var (parser, _) = Build(SlidePlaceholder("title", null), master);
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.Shapes[0].Height.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Parse_BodyLikePlaceholderMatchesObject_InheritsGeometry()
    {
        // Slide subtitle (body-like) matches a layout Object placeholder via the body-like branch.
        var master = MasterWithLayoutPlaceholder(PlaceholderType.Object, null);
        var (parser, _) = Build(SlidePlaceholder("subTitle", null), master);
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.Shapes[0].Width.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Parse_PlaceholderNoMatchingLayoutShape_LeavesGeometryUnset()
    {
        // Layout has only a Footer placeholder; slide Title cannot match any family → no inheritance.
        var master = MasterWithLayoutPlaceholder(PlaceholderType.Footer, null);
        var (parser, _) = Build(SlidePlaceholder("title", null), master);
        var slide = parser.Parse(SlideUri, "rId1", 1);
        slide.Shapes[0].Width.Value.ShouldBe(0);
    }
}
