using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Models.Themes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Drives the harder-to-reach branches of
///     <see cref="Unchained.Pptx.Parsing.OpenXmlPresentationParser" /> by cloning the valid
///     <c>minimal.pptx</c> base package and surgically overriding individual parts
///     (presentation.xml, slide master, slide layout, slides). This reaches branches a generated
///     round-trip cannot: master/layout shape trees containing tables &amp; connectors, the
///     no-SlideIdList synthetic-id path, an unknown-graphic-frame stub, a chart graphic-frame with a
///     missing relationship, the default slide size, and shapes with no <c>spPr</c>.
/// </summary>
public sealed class OpenXmlPresentationParserRawTests : PptxTestBase
{
    private const string P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private const string A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private const string SlideRels =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\"/>" +
        "</Relationships>";

    private static readonly OpenOptions Sdk = new() { UseOpenXmlEngine = true };

    private static string Decl => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

    private static string Ns =>
        $"xmlns:a=\"{A}\" xmlns:r=\"{R}\" xmlns:p=\"{P}\"";

    private static byte[] BaseBytes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", "minimal.pptx");
        Assert.SkipUnless(File.Exists(path), "minimal.pptx base sample missing");
        return File.ReadAllBytes(path);
    }

    // Clones the base package, replacing/adding the given parts (keyed by entry name, no leading
    // slash). New entries are appended; existing entries are overwritten in place.
    private static byte[] CloneWithParts(IReadOnlyDictionary<string, string> overrides)
    {
        var baseBytes = BaseBytes();
        using var output = new MemoryStream();
        using (var src = new ZipArchive(new MemoryStream(baseBytes), ZipArchiveMode.Read))
        using (var dst = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in src.Entries)
            {
                var name = entry.FullName;
                var newEntry = dst.CreateEntry(name);
                using var ws = newEntry.Open();
                if (overrides.TryGetValue(name, out var replacement))
                    ws.Write(Encoding.UTF8.GetBytes(replacement));
                else
                {
                    using var rs = entry.Open();
                    rs.CopyTo(ws);
                }

                written.Add(name);
            }

            foreach (var (name, content) in overrides)
            {
                if (written.Contains(name)) continue;

                var newEntry = dst.CreateEntry(name);
                using var ws = newEntry.Open();
                ws.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return output.ToArray();
    }

    // A master shape tree containing a table graphic-frame and a connection shape — drives the
    // ReadShapeTreeNoSlide graphic-frame (table) and connector branches (lines 243-246).
    private static string MasterWithTableAndConnector()
    {
        const string tableFrame =
            "<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"5\" name=\"T\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>" +
            "<p:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"100\" cy=\"100\"/></p:xfrm>" +
            "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/table\">" +
            "<a:tbl><a:tblPr/><a:tblGrid><a:gridCol w=\"100\"/></a:tblGrid>" +
            "<a:tr h=\"50\"><a:tc><a:txBody><a:bodyPr/><a:p/></a:txBody><a:tcPr/></a:tc></a:tr></a:tbl>" +
            "</a:graphicData></a:graphic></p:graphicFrame>";
        const string chartFrame =
            "<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"6\" name=\"C\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>" +
            "<p:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"100\" cy=\"100\"/></p:xfrm>" +
            "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"/></a:graphic></p:graphicFrame>";
        const string connector =
            "<p:cxnSp><p:nvCxnSpPr><p:cNvPr id=\"7\" name=\"X\"/><p:cNvCxnSpPr/><p:nvPr/></p:nvCxnSpPr>" +
            "<p:spPr><a:prstGeom prst=\"bentConnector3\"><a:avLst/></a:prstGeom></p:spPr></p:cxnSp>";

        return $"{Decl}<p:sldMaster {Ns}><p:cSld><p:spTree>" +
               "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
               "<p:grpSpPr/>" +
               tableFrame + chartFrame + connector +
               "</p:spTree></p:cSld>" +
               "<p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/>" +
               "<p:sldLayoutIdLst><p:sldLayoutId id=\"2147483655\" r:id=\"rId1\"/></p:sldLayoutIdLst>" +
               "</p:sldMaster>";
    }

    // ── Master/layout shape-tree branches (243-246) ───────────────────────────────────

    [Fact]
    public async Task Master_WithTableConnectorAndChartFrame_MapsShapesNoSlideContext()
    {
        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["ppt/slideMasters/slideMaster1.xml"] = MasterWithTableAndConnector()
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        var masterShapes = doc.Masters[0].Shapes;
        masterShapes.OfType<TableShape>().ShouldNotBeEmpty();
        masterShapes.OfType<ConnectorShape>().ShouldNotBeEmpty();
        // In the no-slide-context path only table graphic-frames map; the chart frame yields null.
        masterShapes.OfType<ChartShape>().ShouldBeEmpty();
        await doc.DisposeAsync();
    }

    // ── No SlideIdList → synthetic ids (319-320) ──────────────────────────────────────

    [Fact]
    public async Task Presentation_WithoutSlideIdList_UsesSyntheticIds()
    {
        // A slide part exists and is related, but presentation.xml omits <p:sldIdLst>, forcing the
        // fall-back enumeration over SlideParts with synthetic ids.
        var presentation =
            $"{Decl}<p:presentation {Ns}>" +
            "<p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst>" +
            "<p:sldSz cx=\"9144000\" cy=\"6858000\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/>" +
            "</p:presentation>";

        const string presRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster\" Target=\"slideMasters/slideMaster1.xml\"/>" +
            "<Relationship Id=\"rId5\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"theme/theme1.xml\"/>" +
            "<Relationship Id=\"rIdSlide\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" Target=\"slides/slide1.xml\"/>" +
            "</Relationships>";

        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "</p:spTree></p:cSld></p:sld>";

        const string slideRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\"/>" +
            "</Relationships>";

        var contentTypes = ContentTypesWithSlide();

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = contentTypes,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = slideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        doc.Slides.Count.ShouldBe(1);
        // Synthetic ids start at 256.
        doc.Slides[0].SlideId.ShouldBeGreaterThanOrEqualTo(256u);
        await doc.DisposeAsync();
    }

    // ── Default slide size when cx/cy missing (159-160) ───────────────────────────────

    [Fact]
    public async Task Presentation_WithoutSlideSize_FallsBackToWidescreen()
    {
        var presentation =
            $"{Decl}<p:presentation {Ns}>" +
            "<p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst>" +
            "<p:notesSz cx=\"6858000\" cy=\"9144000\"/>" +
            "</p:presentation>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["ppt/presentation.xml"] = presentation
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        doc.SlideSize.Width.Value.ShouldBe(SlideSize.Widescreen.Width.Value);
        await doc.DisposeAsync();
    }

    // ── Unknown graphic-frame → stub autoshape (487-490) ──────────────────────────────

    [Fact]
    public async Task Slide_UnknownGraphicFrame_MapsToAutoShapeStub()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"9\" name=\"OLE\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>" +
            "<p:xfrm><a:off x=\"100\" y=\"100\"/><a:ext cx=\"500\" cy=\"500\"/></p:xfrm>" +
            "<a:graphic><a:graphicData uri=\"http://example.com/unknown\"/></a:graphic></p:graphicFrame>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        var stub = doc.Slides[0].Shapes.OfType<AutoShape>().Single();
        stub.ShapeType.ShouldBe(AutoShapeType.Rectangle);
        await doc.DisposeAsync();
    }

    // ── Chart graphic-frame with no usable relationship id (656-663) ──────────────────

    [Fact]
    public async Task Slide_ChartFrameWithoutRelId_MapsChartShapeWithoutData()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        // chart graphic-data present, but no <c:chart r:id> child → ReadChart returns at the
        // empty-rId guard.
        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"8\" name=\"Chart\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>" +
            "<p:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"500\" cy=\"500\"/></p:xfrm>" +
            "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"/></a:graphic></p:graphicFrame>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        var chart = doc.Slides[0].Shapes.OfType<ChartShape>().Single();
        chart.Chart.Data.Series.Count.ShouldBe(0);
        await doc.DisposeAsync();
    }

    // ── AutoShape with no spPr (line 566 ReadFillAndLine null-guard) ──────────────────

    [Fact]
    public async Task Slide_ShapeWithoutSpPr_SkipsFillAndLine()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "<p:sp><p:nvSpPr><p:cNvPr id=\"10\" name=\"NoSpPr\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr></p:sp>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        doc.Slides[0].Shapes.OfType<AutoShape>().ShouldNotBeEmpty();
        await doc.DisposeAsync();
    }

    // ── Layout type twoTxTwoObj (259) ────────────────────────────────────────────────

    [Fact]
    public async Task Layout_TwoTxTwoObjType_MapsToTitleAndTwoContent()
    {
        var layout =
            $"{Decl}<p:sldLayout {Ns} type=\"twoTxTwoObj\" preserve=\"1\"><p:cSld name=\"TwoByTwo\"><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["ppt/slideLayouts/slideLayout1.xml"] = layout
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        doc.Masters[0].Layouts[0].LayoutType.ShouldBe(LayoutType.TitleAndTwoContent);
        await doc.DisposeAsync();
    }

    // ── SynthesizeFallback: master with no layouts + slide with no layout (270-281, 99) ──

    [Fact]
    public async Task MasterWithoutLayouts_SlideWithoutLayout_UsesSynthesizedFallback()
    {
        // Master rels: theme only (no slideLayout) → SlideLayoutParts empty → no layouts anywhere
        // → fallbackLayout is the synthesized blank. Slide has no layout relationship → it resolves
        // to that fallback.
        var master =
            $"{Decl}<p:sldMaster {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "</p:spTree></p:cSld>" +
            "<p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/>" +
            "</p:sldMaster>";

        const string masterRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"../theme/theme1.xml\"/>" +
            "</Relationships>";

        var (ct, presRels, presentation) = SingleSlideScaffold();

        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "</p:spTree></p:cSld></p:sld>";

        const string emptySlideRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"/>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slideMasters/slideMaster1.xml"] = master,
                ["ppt/slideMasters/_rels/slideMaster1.xml.rels"] = masterRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = emptySlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        doc.Slides.Count.ShouldBe(1);
        // The synthesized fallback layout is Blank and has a master.
        doc.Slides[0].Layout.ShouldNotBeNull();
        doc.Slides[0].Layout.LayoutType.ShouldBe(LayoutType.Blank);
        await doc.DisposeAsync();
    }

    // ── Chart frame r:id resolving to a non-chart part (663) ──────────────────────────

    [Fact]
    public async Task Slide_ChartFrameRelIdToNonChartPart_ReturnsWithoutData()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        // The chart's r:id points at rId1 which is the slide's layout part, not a ChartPart.
        var slide =
            $"{Decl}<p:sld {Ns} xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"8\" name=\"Chart\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>" +
            "<p:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"500\" cy=\"500\"/></p:xfrm>" +
            "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\">" +
            "<c:chart xmlns:r2=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" r2:id=\"rId1\"/>" +
            "</a:graphicData></a:graphic></p:graphicFrame>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        var chart = doc.Slides[0].Shapes.OfType<ChartShape>().Single();
        chart.Chart.Data.Series.Count.ShouldBe(0);
        chart.PartUri.ShouldBeNullOrEmpty();
        await doc.DisposeAsync();
    }

    // ── Shapes with partial / absent geometry (599-620, 442, 627, 637) ───────────────

    [Fact]
    public async Task Slide_ShapesWithPartialGeometry_MapWhatIsPresent()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        // sp1: xfrm with extents only (no offset). sp2: xfrm with offset only (no extents).
        // pic: blipFill with no embed attribute. table: grid columns but no rows; plus a frame
        // with offset only.
        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "<p:sp><p:nvSpPr><p:cNvPr id=\"11\" name=\"ExtOnly\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>" +
            "<p:spPr><a:xfrm><a:ext cx=\"500\" cy=\"500\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr></p:sp>" +
            "<p:sp><p:nvSpPr><p:cNvPr id=\"12\" name=\"OffOnly\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>" +
            "<p:spPr><a:xfrm><a:off x=\"10\" y=\"10\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr></p:sp>" +
            "<p:pic><p:nvPicPr><p:cNvPr id=\"13\" name=\"NoEmbed\"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>" +
            "<p:blipFill><a:blip/><a:stretch><a:fillRect/></a:stretch></p:blipFill>" +
            "<p:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"100\" cy=\"100\"/></a:xfrm></p:spPr></p:pic>" +
            "<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"14\" name=\"EmptyTable\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>" +
            "<p:xfrm><a:off x=\"0\" y=\"0\"/></p:xfrm>" +
            "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/table\">" +
            "<a:tbl><a:tblPr/><a:tblGrid><a:gridCol w=\"100\"/></a:tblGrid></a:tbl>" +
            "</a:graphicData></a:graphic></p:graphicFrame>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        var shapes = doc.Slides[0].Shapes;
        shapes.OfType<AutoShape>().Count().ShouldBe(2);
        shapes.OfType<PictureShape>().ShouldNotBeEmpty();
        var table = shapes.OfType<TableShape>().Single();
        table.Grid.ColumnCount.ShouldBe(1);
        table.Grid.RowCount.ShouldBe(0);
        await doc.DisposeAsync();
    }

    // ── Group with partial transform (501-525) ────────────────────────────────────────

    [Fact]
    public async Task Slide_GroupWithoutChildOffsetOrExtents_MapsOffsetAndExtentsOnly()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        // grpSpPr xfrm has off + ext but no chOff / chExt; one nested shape.
        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "<p:grpSp><p:nvGrpSpPr><p:cNvPr id=\"20\" name=\"G\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
            "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"1000\" cy=\"1000\"/></a:xfrm></p:grpSpPr>" +
            "<p:sp><p:nvSpPr><p:cNvPr id=\"21\" name=\"GC\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>" +
            "<p:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"100\" cy=\"100\"/></a:xfrm><a:prstGeom prst=\"ellipse\"><a:avLst/></a:prstGeom></p:spPr></p:sp>" +
            "</p:grpSp>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        var group = doc.Slides[0].Shapes.OfType<GroupShape>().Single();
        group.Width.Value.ShouldBeGreaterThan(0);
        group.Children.ShouldNotBeEmpty();
        await doc.DisposeAsync();
    }

    // ── Group with no group transform at all (501 absent side) ────────────────────────

    [Fact]
    public async Task Slide_GroupWithoutTransform_StillMaps()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "<p:grpSp><p:nvGrpSpPr><p:cNvPr id=\"30\" name=\"G2\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
            "<p:grpSpPr/>" +
            "</p:grpSp>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        doc.Slides[0].Shapes.OfType<GroupShape>().ShouldNotBeEmpty();
        await doc.DisposeAsync();
    }

    // ── Visible slide with explicit show="1" (350 !show.Value false side) ─────────────

    [Fact]
    public async Task Slide_ShowExplicitlyOne_IsVisible()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        var slide =
            $"{Decl}<p:sld {Ns} show=\"1\"><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        doc.Slides[0].IsHidden.ShouldBeFalse();
        await doc.DisposeAsync();
    }

    // ── Skeletal shapes: spPr present but no xfrm, nvPr without drawing props (599-620, 555) ──

    [Fact]
    public async Task Slide_ShapesWithoutTransformOrNvProps_MapWithDefaults()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        // sp: nvSpPr WITHOUT cNvPr → NonVisualDrawingProperties null → ReadCommon null-guard.
        //     spPr WITHOUT xfrm → ReadGeometry xfrm-null side.
        // cxn: connection shape WITHOUT spPr xfrm and no prstGeom → connector default branch.
        // pic: picture WITHOUT spPr (geometry skipped) and blipFill missing embed.
        // table: a cell WITHOUT txBody → ReadTableCell body-null side.
        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "<p:sp><p:nvSpPr/><p:spPr><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr></p:sp>" +
            "<p:cxnSp><p:nvCxnSpPr/><p:spPr/></p:cxnSp>" +
            "<p:pic><p:nvPicPr/></p:pic>" +
            "<p:graphicFrame><p:nvGraphicFramePr/>" +
            "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/table\">" +
            "<a:tbl><a:tblPr/><a:tblGrid><a:gridCol w=\"100\"/></a:tblGrid>" +
            "<a:tr h=\"50\"><a:tc><a:tcPr/></a:tc></a:tr></a:tbl>" +
            "</a:graphicData></a:graphic></p:graphicFrame>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        var shapes = doc.Slides[0].Shapes;
        shapes.OfType<AutoShape>().ShouldNotBeEmpty();
        shapes.OfType<ConnectorShape>().Single().ConnectorType.ShouldBe(ConnectorType.Straight);
        shapes.OfType<PictureShape>().ShouldNotBeEmpty();
        var table = shapes.OfType<TableShape>().Single();
        table.Grid.RowCount.ShouldBe(1);
        await doc.DisposeAsync();
    }

    // ── Master without spTree; layout without name/spTree (190, 211, 224) ─────────────

    [Fact]
    public async Task Master_NoShapeTree_Layout_NoNameNoShapeTree_StillMap()
    {
        var master =
            $"{Decl}<p:sldMaster {Ns}><p:cSld></p:cSld>" +
            "<p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/>" +
            "<p:sldLayoutIdLst><p:sldLayoutId id=\"2147483655\" r:id=\"rId1\"/></p:sldLayoutIdLst>" +
            "</p:sldMaster>";

        // Layout: cSld with neither a name attribute nor a spTree, and no type attribute.
        var layout =
            $"{Decl}<p:sldLayout {Ns} preserve=\"1\"><p:cSld></p:cSld>" +
            "<p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["ppt/slideMasters/slideMaster1.xml"] = master,
                ["ppt/slideLayouts/slideLayout1.xml"] = layout
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        var m = doc.Masters[0];
        m.Shapes.ShouldBeEmpty();
        m.Layouts.Count.ShouldBe(1);
        m.Layouts[0].Name.ShouldBe(string.Empty);
        m.Layouts[0].LayoutType.ShouldBe(LayoutType.Custom);
        m.Layouts[0].Shapes.ShouldBeEmpty();
        await doc.DisposeAsync();
    }

    // ── Multiple masters (loop iteration 86) + properties without dates (304,305) ────

    [Fact]
    public async Task Presentation_MinimalCoreProps_NoDates_StillMap()
    {
        // Replace core.xml with one that omits dcterms:created / dcterms:modified so the
        // Created/Modified guards take their false side.
        const string core =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
            "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" " +
            "xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
            "<dc:title>NoDates</dc:title></cp:coreProperties>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["docProps/core.xml"] = core
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        doc.Properties.Title.ShouldBe("NoDates");
        await doc.DisposeAsync();
    }

    // ── Offset/extents elements present but missing x/y/cx/cy attrs (599-620 ?? 0) ────

    [Fact]
    public async Task Slide_GeometryElementsWithoutAttributes_DefaultToZero()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        // sp: xfrm has <a:off/> and <a:ext/> with NO x/y/cx/cy → exercises the `?? 0` fallbacks.
        // graphicFrame (table): p:xfrm with <a:off/> and <a:ext/> missing attrs likewise.
        // group: grpSpPr xfrm with chOff / chExt elements missing attrs.
        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "<p:sp><p:nvSpPr><p:cNvPr id=\"40\" name=\"ZeroGeom\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>" +
            "<p:spPr><a:xfrm><a:off/><a:ext/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr></p:sp>" +
            "<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"41\" name=\"ZT\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>" +
            "<p:xfrm><a:off/><a:ext/></p:xfrm>" +
            "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/table\">" +
            "<a:tbl><a:tblPr/><a:tblGrid><a:gridCol/></a:tblGrid>" +
            "<a:tr><a:tc><a:txBody><a:bodyPr/><a:p/></a:txBody><a:tcPr/></a:tc></a:tr></a:tbl>" +
            "</a:graphicData></a:graphic></p:graphicFrame>" +
            "<p:grpSp><p:nvGrpSpPr><p:cNvPr id=\"42\" name=\"ZG\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
            "<p:grpSpPr><a:xfrm><a:off/><a:ext/><a:chOff/><a:chExt/></a:xfrm></p:grpSpPr>" +
            "</p:grpSp>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        var shapes = doc.Slides[0].Shapes;
        shapes.OfType<AutoShape>().Single().Width.Value.ShouldBe(0);
        var table = shapes.OfType<TableShape>().Single();
        table.Grid.RowCount.ShouldBe(1);
        var group = shapes.OfType<GroupShape>().Single();
        group.ChildOffsetX.Value.ShouldBe(0);
        await doc.DisposeAsync();
    }

    // ── GraphicFrame with no <a:graphic> at all → uri null → default stub (451) ───────

    [Fact]
    public async Task Slide_GraphicFrameWithoutGraphic_MapsToStub()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"50\" name=\"Bare\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>" +
            "<p:xfrm><a:off x=\"5\" y=\"5\"/><a:ext cx=\"50\" cy=\"50\"/></p:xfrm>" +
            "<a:graphic/></p:graphicFrame>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        doc.Slides[0].Shapes.OfType<AutoShape>().ShouldNotBeEmpty();
        await doc.DisposeAsync();
    }

    // ── Picture with no blipFill, and frame transform absent (442, 617) ───────────────

    [Fact]
    public async Task Slide_PictureWithoutBlipFill_MapsWithoutImage()
    {
        var (ct, presRels, presentation) = SingleSlideScaffold();

        var slide =
            $"{Decl}<p:sld {Ns}><p:cSld><p:spTree>" +
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            "<p:pic><p:nvPicPr><p:cNvPr id=\"60\" name=\"NoBlip\"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>" +
            "<p:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"10\" cy=\"10\"/></a:xfrm></p:spPr></p:pic>" +
            // table frame with NO p:xfrm at all → ReadFrameGeometry xfrm-null side.
            "<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"61\" name=\"NoXfrm\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>" +
            "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/table\">" +
            "<a:tbl><a:tblPr/><a:tblGrid><a:gridCol w=\"100\"/></a:tblGrid></a:tbl>" +
            "</a:graphicData></a:graphic></p:graphicFrame>" +
            "</p:spTree></p:cSld></p:sld>";

        var bytes = CloneWithParts(
            new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = ct,
                ["ppt/presentation.xml"] = presentation,
                ["ppt/_rels/presentation.xml.rels"] = presRels,
                ["ppt/slides/slide1.xml"] = slide,
                ["ppt/slides/_rels/slide1.xml.rels"] = SlideRels
            }
        );

        var doc = await new PresentationProcessor().LoadAsync(bytes, Sdk);
        var pic = doc.Slides[0].Shapes.OfType<PictureShape>().Single();
        pic.Image.ShouldBeNull();
        await doc.DisposeAsync();
    }

    private static string ContentTypesWithSlide()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", "minimal.pptx");
        using var src = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read);
        var entry = src.GetEntry("[Content_Types].xml")!;
        using var rs = new StreamReader(entry.Open());
        var ct = rs.ReadToEnd();
        // Add the slide1.xml override before </Types>.
        const string slideOverride =
            "<Override PartName=\"/ppt/slides/slide1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>";
        return ct.Replace("</Types>", slideOverride + "</Types>");
    }

    // Builds a content-types + presentation + presentation-rels trio for a single-slide package
    // whose presentation lists the slide in <p:sldIdLst>.
    private static (string ContentTypes, string PresRels, string Presentation) SingleSlideScaffold()
    {
        var presentation =
            $"{Decl}<p:presentation {Ns}>" +
            "<p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst>" +
            "<p:sldIdLst><p:sldId id=\"256\" r:id=\"rIdSlide\"/></p:sldIdLst>" +
            "<p:sldSz cx=\"12192000\" cy=\"6858000\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/>" +
            "</p:presentation>";

        const string presRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster\" Target=\"slideMasters/slideMaster1.xml\"/>" +
            "<Relationship Id=\"rId5\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"theme/theme1.xml\"/>" +
            "<Relationship Id=\"rIdSlide\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" Target=\"slides/slide1.xml\"/>" +
            "</Relationships>";

        return (ContentTypesWithSlide(), presRels, presentation);
    }
}
