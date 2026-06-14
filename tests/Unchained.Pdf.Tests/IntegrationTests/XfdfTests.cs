using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class XfdfTests : PdfTestBase
{
    private static readonly XfdfEditor Editor = new();
    private static readonly AnnotationEditor AnnotEditor = new();


    [Fact]
    public async Task ExportAnnotationsToXfdf_NoAnnotations_ReturnsEmptyXfdf()
    {
        var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var xfdf = Editor.ExportAnnotationsToXfdf(doc);
        xfdf.ShouldContain("<annots");
        xfdf.ShouldNotContain("<text");
    }

    [Fact]
    public async Task ExportAnnotationsToXfdf_WithAnnotation_ContainsRect()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAnnotation("Exported note"), TestContext.Current.CancellationToken);
        var xfdf = Editor.ExportAnnotationsToXfdf(doc);
        xfdf.ShouldContain("rect=");
        xfdf.ShouldContain("Exported note");
    }

    [Fact]
    public async Task ExportAnnotationsToXfdf_WithAnnotation_ContainsPageAttr()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAnnotation(), TestContext.Current.CancellationToken);
        var xfdf = Editor.ExportAnnotationsToXfdf(doc);
        xfdf.ShouldContain("page=\"0\"");
    }

    [Fact]
    public async Task ImportAnnotationsFromXfdf_AddsAnnotation()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        const string xfdf =
            """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><text page="0" rect="50,700,100,750"><contents>Imported note</contents></text></annots></xfdf>""";
        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations().Count.ShouldBe(1);
    }

    [Fact]
    public async Task ImportAnnotationsFromXfdf_Contents_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        const string xfdf =
            """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><text page="0" rect="50,700,100,750"><contents>Hello XFDF</contents></text></annots></xfdf>""";
        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Contents.ShouldBe("Hello XFDF");
    }

    [Fact]
    public async Task ExportThenImport_RoundTripsAnnotations()
    {
        await using var source = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        // ReSharper disable once BadListLineBreaks
        await AnnotEditor.AddAnnotationAsync(
            source,
            1,
            new Annotation(
                AnnotationSubtype.Text,
                50,
                700,
                50,
                50,
                "Round-trip note"
            ),
            TestContext.Current.CancellationToken
        );

        var xfdf = Editor.ExportAnnotationsToXfdf(source);

        await using var target = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.ImportAnnotationsFromXfdfAsync(target, xfdf, TestContext.Current.CancellationToken);
        target.Pages[1].GetAnnotations().Count.ShouldBe(1);
        target.Pages[1].GetAnnotations()[0].Contents.ShouldBe("Round-trip note");
    }

    // ── Subtype export coverage ───────────────────────────────────────────────

    [
        Theory,
        InlineData(AnnotationSubtype.Highlight, "highlight"),
        InlineData(AnnotationSubtype.Link, "link"),
        InlineData(AnnotationSubtype.FreeText, "freetext"),
        InlineData(AnnotationSubtype.Square, "square"),
        InlineData(AnnotationSubtype.Circle, "circle")
    ]
    public async Task ExportAnnotationsToXfdf_AllSubtypes_UsesCorrectElementName(AnnotationSubtype subtype, string expectedElement)
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await AnnotEditor.AddAnnotationAsync(
            doc,
            1,
            // ReSharper disable once BadListLineBreaks
            new Annotation(
                subtype,
                10,
                10,
                50,
                50,
                "test"
            ),
            TestContext.Current.CancellationToken
        );

        var xfdf = Editor.ExportAnnotationsToXfdf(doc);

        xfdf.ShouldContain($"<{expectedElement} ");
    }

    // ── Color export ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportAnnotationsToXfdf_WithColor_EmitsColorAttribute()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var color = new[] { 1f, 0f, 0f }; // pure red → #FF0000
        await AnnotEditor.AddAnnotationAsync(
            doc,
            1,
            // ReSharper disable BadListLineBreaks
            new Annotation(
                AnnotationSubtype.Text,
                10,
                10,
                50,
                50,
                "colored",
                color
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        var xfdf = Editor.ExportAnnotationsToXfdf(doc);

        xfdf.ShouldContain("color=\"#FF0000\"");
    }

    [Fact]
    public async Task ExportAnnotationsToXfdf_WithColor_RoundTripsColor()
    {
        await using var source = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var color = new[] { 0f, 0.5f, 1f };
        await AnnotEditor.AddAnnotationAsync(
            source,
            1,
            // ReSharper disable BadListLineBreaks
            new Annotation(
                AnnotationSubtype.Highlight,
                10,
                10,
                50,
                20,
                "highlighted",
                color
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        var xfdf = Editor.ExportAnnotationsToXfdf(source);

        await using var target = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.ImportAnnotationsFromXfdfAsync(target, xfdf, TestContext.Current.CancellationToken);

        var ann = target.Pages[1].GetAnnotations()[0];
        ann.Color.ShouldNotBeNull();
        ann.Color!.Length.ShouldBe(3);
    }

    // ── Multi-page export ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExportAnnotationsToXfdf_MultiPage_EmitsCorrectPageIndices()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        await AnnotEditor.AddAnnotationAsync(
            doc,
            1,
            // ReSharper disable BadListLineBreaks
            new Annotation(
                AnnotationSubtype.Text,
                10,
                10,
                50,
                50,
                "page1"
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );
        await AnnotEditor.AddAnnotationAsync(
            doc,
            3,
            // ReSharper disable BadListLineBreaks
            new Annotation(
                AnnotationSubtype.Text,
                10,
                10,
                50,
                50,
                "page3"
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        var xfdf = Editor.ExportAnnotationsToXfdf(doc);

        xfdf.ShouldContain("page=\"0\"");
        xfdf.ShouldContain("page=\"2\"");
    }

    // ── Import subtype coverage ───────────────────────────────────────────────

    [
        Theory,
        InlineData("highlight", AnnotationSubtype.Highlight),
        InlineData("link", AnnotationSubtype.Link),
        InlineData("freetext", AnnotationSubtype.FreeText),
        InlineData("square", AnnotationSubtype.Square),
        InlineData("circle", AnnotationSubtype.Circle),
        InlineData("unknown_element", AnnotationSubtype.Text)
    ]
    // default branch
    public async Task ImportAnnotationsFromXfdf_AllSubtypes_SetsCorrectSubtype(string elementName, AnnotationSubtype expectedSubtype)
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var xfdf =
            $"""<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><{elementName} page="0" rect="10,10,60,60" /></annots></xfdf>""";

        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf, TestContext.Current.CancellationToken);

        doc.Pages[1].GetAnnotations().Count.ShouldBe(1);
        doc.Pages[1].GetAnnotations()[0].Subtype.ShouldBe(expectedSubtype);
    }

    // ── Import with color ─────────────────────────────────────────────────────

    [Fact]
    public async Task ImportAnnotationsFromXfdf_WithColor_ParsedCorrectly()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        const string xfdf =
            """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><text page="0" rect="10,10,60,60" color="#FF8000" /></annots></xfdf>""";

        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf, TestContext.Current.CancellationToken);

        var ann = doc.Pages[1].GetAnnotations()[0];
        ann.Color.ShouldNotBeNull();
        ann.Color![0].ShouldBeInRange(0.99f, 1.01f); // R ≈ 1.0
        ann.Color[1].ShouldBeInRange(0.49f, 0.51f);  // G ≈ 0.5
        ann.Color[2].ShouldBeInRange(-0.01f, 0.01f); // B ≈ 0.0
    }

    // ── Import graceful-degradation / edge cases ──────────────────────────────

    [Fact]
    public async Task ImportAnnotationsFromXfdf_NoAnnotsElement_LeavesPageEmpty()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        // Valid XML but no <annots> child under root
        const string xfdf = """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"></xfdf>""";

        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf, TestContext.Current.CancellationToken);

        doc.Pages[1].GetAnnotations().Count.ShouldBe(0);
    }

    [Fact]
    public async Task ImportAnnotationsFromXfdf_MissingPageAttribute_SkipsEntry()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        // Element has no page attribute — should be skipped (int.TryParse fails)
        const string xfdf =
            """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><text rect="10,10,60,60"><contents>No page attr</contents></text></annots></xfdf>""";

        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf, TestContext.Current.CancellationToken);

        doc.Pages[1].GetAnnotations().Count.ShouldBe(0);
    }

    [Fact]
    public async Task ImportAnnotationsFromXfdf_MissingRectAttribute_SkipsEntry()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        // Element has page but no rect — ParseRect returns null → skip
        const string xfdf =
            """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><text page="0"><contents>No rect</contents></text></annots></xfdf>""";

        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf, TestContext.Current.CancellationToken);

        doc.Pages[1].GetAnnotations().Count.ShouldBe(0);
    }

    [Fact]
    public async Task ImportAnnotationsFromXfdf_InvalidRectTooFewParts_SkipsEntry()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        const string xfdf =
            """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><text page="0" rect="10,20,30" /></annots></xfdf>""";

        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf, TestContext.Current.CancellationToken);

        doc.Pages[1].GetAnnotations().Count.ShouldBe(0);
    }

    [Fact]
    public async Task ImportAnnotationsFromXfdf_NonNumericRectParts_SkipsEntry()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        const string xfdf =
            """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><text page="0" rect="a,b,c,d" /></annots></xfdf>""";

        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf, TestContext.Current.CancellationToken);

        doc.Pages[1].GetAnnotations().Count.ShouldBe(0);
    }

    [Fact]
    public async Task ImportAnnotationsFromXfdf_InvalidColorFormats_DoesNotThrow()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        // Three color strings that all fail ParseHexColor: no #, too short, bad hex digits
        const string xfdf =
            """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><text page="0" rect="10,10,60,60" color="FF0000" /><text page="0" rect="10,10,60,60" color="#FFF" /><text page="0" rect="10,10,60,60" color="#GGGGGG" /></annots></xfdf>""";

        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf, TestContext.Current.CancellationToken);

        // All three annotations should still be imported (color just stays null)
        doc.Pages[1].GetAnnotations().Count.ShouldBe(3);
        doc.Pages[1].GetAnnotations().ShouldAllBe(static a => a.Color == null);
    }

    // ── Rect coordinate conversion ────────────────────────────────────────────

    [Fact]
    public async Task ImportAnnotationsFromXfdf_RectCoordinates_ConvertedToWidthHeight()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        // rect="x1,y1,x2,y2" → Width = x2-x1, Height = y2-y1
        const string xfdf =
            """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><text page="0" rect="50,700,150,780" /></annots></xfdf>""";

        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf, TestContext.Current.CancellationToken);

        var ann = doc.Pages[1].GetAnnotations()[0];
        ann.X.ShouldBe(50f);
        ann.Y.ShouldBe(700f);
        ann.Width.ShouldBe(100f);
        ann.Height.ShouldBe(80f);
    }

    // ── Export rect format ────────────────────────────────────────────────────

    [Fact]
    public async Task ExportAnnotationsToXfdf_RectFormat_IsX1Y1X2Y2()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        // Annotation at X=50, Y=700, Width=100, Height=80 → rect should be "50,700,150,780"
        await AnnotEditor.AddAnnotationAsync(
            doc,
            1,
            new Annotation(AnnotationSubtype.Text, 50f, 700f, 100f, 80f),
            TestContext.Current.CancellationToken
        );

        var xfdf = Editor.ExportAnnotationsToXfdf(doc);

        xfdf.ShouldContain("rect=\"50,700,150,780\"");
    }
}
