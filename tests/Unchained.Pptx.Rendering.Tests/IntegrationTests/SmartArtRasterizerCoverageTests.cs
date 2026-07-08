using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Rendering.Models;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Rendering.Tests.IntegrationTests;

/// <summary>
///     Targeted branch coverage for <see cref="SlideRasterizer" />'s SmartArt layout dispatch and
///     the individual layout renderers (linear, cycle, matrix, hierarchy with descendants, pyramid).
///     Renders at a large canvas so the layout code runs past the small-canvas early returns, and
///     selects node counts/aspect ratios that route to each specific layout branch.
/// </summary>
public sealed class SmartArtRasterizerCoverageTests : PptxTestBase
{
    private static readonly RenderOptions Large = new() { WidthPx = 1280, HeightPx = 720 };

    private static SmartArtShape AddSmartArt(
        PresentationDocument doc,
        Emu width,
        Emu height
    )
    {
        var shape = new SmartArtShape { X = Emu.FromInches(0.5), Y = Emu.FromInches(0.5), Width = width, Height = height };
        doc.Slides[0].Shapes.AddParsed(shape);
        return shape;
    }

    private static Task<PptxImage> RenderLargeAsync(PresentationDocument doc) =>
        SlideRenderer.RenderAsync(doc.Slides[0], doc.SlideSize, Large);

    // ── Pyramid: count >= 7, height > width ────────────────────────────────────────

    [Fact]
    public async Task SmartArt_Pyramid_SevenTallNodes_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        // Tall + many nodes routes past matrix (count 4) and cycle (count 3-6) to the pyramid branch.
        var sa = AddSmartArt(doc, Emu.FromInches(3), Emu.FromInches(7));
        for (var i = 0; i < 7; i++) sa.Nodes.Add(new SmartArtNode { Text = $"Level {i}" });

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SmartArt_Pyramid_CapsAtSixRows_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        // More than six flat tall nodes: the pyramid renderer caps n at 6 but still iterates.
        var sa = AddSmartArt(doc, Emu.FromInches(2), Emu.FromInches(7));
        for (var i = 0; i < 10; i++) sa.Nodes.Add(new SmartArtNode { Text = $"P{i}" });

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    // ── Matrix: count 4, width >= height ────────────────────────────────────────────

    [Fact]
    public async Task SmartArt_Matrix_FourWideNodes_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var sa = AddSmartArt(doc, Emu.FromInches(8), Emu.FromInches(4));
        for (var i = 0; i < 4; i++) sa.Nodes.Add(new SmartArtNode { Text = $"Quadrant {i}" });

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    // ── Cycle: count 3-6 (not 4-wide) ───────────────────────────────────────────────

    [
        Theory,
        InlineData(3),
        InlineData(5),
        InlineData(6)
    ]
    public async Task SmartArt_Cycle_RendersForThreeToSixNodes(int count)
    {
        var doc = PptxFixtures.WithSlides(1);
        var sa = AddSmartArt(doc, Emu.FromInches(7), Emu.FromInches(7));
        for (var i = 0; i < count; i++) sa.Nodes.Add(new SmartArtNode { Text = $"Phase {i} with a long label" });

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    // ── Linear: default fallback (count 1, 2, or 7+ wide) ────────────────────────────

    [
        Theory,
        InlineData(1),
        InlineData(2),
        InlineData(8)
    ]
    public async Task SmartArt_Linear_RendersForNonLayoutCounts(int count)
    {
        var doc = PptxFixtures.WithSlides(1);
        // Wide aspect with 7+ nodes falls through matrix/cycle/pyramid guards to the linear default.
        var sa = AddSmartArt(doc, Emu.FromInches(9), Emu.FromInches(4));
        for (var i = 0; i < count; i++) sa.Nodes.Add(new SmartArtNode { Text = $"Step {i}" });

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SmartArt_Linear_ManyNodes_StopsWhenOverflowing()
    {
        var doc = PptxFixtures.WithSlides(1);
        // Lots of nodes in a short box forces the cy > y+height break inside the linear renderer.
        var sa = AddSmartArt(doc, Emu.FromInches(9), Emu.FromInches(2));
        for (var i = 0; i < 30; i++) sa.Nodes.Add(new SmartArtNode { Text = $"Row {i}" });

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    // ── Hierarchy: nodes with children + grandchildren, rendered large ───────────────

    [Fact]
    public async Task SmartArt_Hierarchy_WithChildren_RendersConnectorsAndChildBoxes()
    {
        var doc = PptxFixtures.WithSlides(1);
        var sa = AddSmartArt(doc, Emu.FromInches(11), Emu.FromInches(6));
        var root = new SmartArtNode { Text = "Root" };
        root.AddChild("Child A");
        root.AddChild("Child B");
        root.AddChild("Child C");
        sa.Nodes.Add(root);

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SmartArt_Hierarchy_MultipleRootsWithGrandchildren_Renders()
    {
        var doc = PptxFixtures.WithSlides(1);
        var sa = AddSmartArt(doc, Emu.FromInches(12), Emu.FromInches(6.5));
        for (var r = 0; r < 2; r++)
        {
            var root = new SmartArtNode { Text = $"Root {r}" };
            var child = root.AddChild($"Child {r}");
            child.AddChild($"Grandchild {r}");
            sa.Nodes.Add(root);
        }

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SmartArt_Hierarchy_DeepTreeInShortBox_HitsChildYGuard()
    {
        var doc = PptxFixtures.WithSlides(1);
        // A short box makes childY exceed the bottom, exercising the childY > y+height guard.
        var sa = AddSmartArt(doc, Emu.FromInches(11), Emu.FromInches(1.2));
        var root = new SmartArtNode { Text = "R" };
        root.AddChild("C1").AddChild("G1");
        sa.Nodes.Add(root);

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }

    // ── Empty placeholder ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SmartArt_NoUsableNodes_RendersBorderPlaceholder()
    {
        var doc = PptxFixtures.WithSlides(1);
        var sa = AddSmartArt(doc, Emu.FromInches(4), Emu.FromInches(3));
        // Whitespace-only, no children → filtered out, leaving zero roots → border placeholder.
        sa.Nodes.Add(new SmartArtNode { Text = "   " });

        var image = await RenderLargeAsync(doc);
        image.Data.Length.ShouldBeGreaterThan(0);
    }
}
