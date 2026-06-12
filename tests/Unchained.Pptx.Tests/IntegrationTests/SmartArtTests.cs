using Shouldly;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
/// SmartArt (diagram) support (M-F): the diagram is surfaced as a <see cref="SmartArtShape"/>
/// with a readable/editable node-text model, and all backing diagram parts round-trip losslessly.
/// </summary>
public sealed class SmartArtTests : PptxTestBase
{
    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", name);

    private static async Task<byte[]> SampleBytesAsync(string name)
    {
        var path = SamplePath(name);
        Assert.SkipUnless(File.Exists(path), $"sample missing: {name}");
        return await File.ReadAllBytesAsync(path);
    }

    [Fact]
    public async Task Load_SurfacesSmartArtShape()
    {
        var bytes = await SampleBytesAsync("shp-shapes.pptx");
        var doc = await Processor.LoadAsync(bytes);

        var smartArt = doc.Slides.SelectMany(static s => s.Shapes).OfType<SmartArtShape>().FirstOrDefault();
        smartArt.ShouldNotBeNull("shp-shapes.pptx contains a SmartArt diagram");

        // Relationship references to the four diagram parts were read from <dgm:relIds>.
        smartArt.DataRelationshipId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Load_ReadsNodeText()
    {
        var bytes = await SampleBytesAsync("shp-shapes.pptx");
        var doc = await Processor.LoadAsync(bytes);

        var smartArt = doc.Slides.SelectMany(static s => s.Shapes).OfType<SmartArtShape>().First();

        smartArt.Nodes.ShouldNotBeEmpty("the diagram has text nodes");
        // The sample's diagram contains a node reading "Smart Art" (with leading space in source).
        smartArt.GetAllText().ShouldContain("Smart Art");
    }

    [Fact]
    public async Task RoundTrip_PreservesAllDiagramParts()
    {
        var bytes = await SampleBytesAsync("shp-shapes.pptx");
        var doc = await Processor.LoadAsync(bytes);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        var saved = ms.ToArray();

        // Every diagram part present in the source must survive the round-trip.
        foreach (var partName in new[]
                 {
                     "ppt/diagrams/data1.xml",
                     "ppt/diagrams/layout1.xml",
                     "ppt/diagrams/quickStyle1.xml",
                     "ppt/diagrams/colors1.xml",
                     "ppt/diagrams/drawing1.xml",
                 })
        {
            PartExists(saved, partName).ShouldBeTrue($"{partName} must survive round-trip");
        }
    }

    [Fact]
    public async Task RoundTrip_ReloadsNodeText()
    {
        var bytes = await SampleBytesAsync("shp-shapes.pptx");
        var doc = await Processor.LoadAsync(bytes);
        var original = doc.Slides.SelectMany(static s => s.Shapes).OfType<SmartArtShape>().First().GetAllText();

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var reloadedText = reloaded.Slides.SelectMany(static s => s.Shapes)
            .OfType<SmartArtShape>().First().GetAllText();

        reloadedText.ShouldBe(original, "node text must survive a round-trip");
    }

    [Fact]
    public async Task EditNodeText_ReflectedAfterReload()
    {
        var bytes = await SampleBytesAsync("shp-shapes.pptx");
        var doc = await Processor.LoadAsync(bytes);

        var smartArt = doc.Slides.SelectMany(static s => s.Shapes).OfType<SmartArtShape>().First();
        var target = FindNodeWithText(smartArt.Nodes, "Smart Art");
        target.ShouldNotBeNull();
        target.Text = "Edited Node";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var reloadedText = reloaded.Slides.SelectMany(static s => s.Shapes)
            .OfType<SmartArtShape>().First().GetAllText();

        reloadedText.ShouldContain("Edited Node");
        reloadedText.ShouldNotContain("Smart Art");
    }

    private static SmartArtNode? FindNodeWithText(IEnumerable<SmartArtNode> nodes, string contains)
    {
        foreach (var node in nodes)
        {
            if (node.Text.Contains(contains)) return node;
            var hit = FindNodeWithText(node.Children, contains);
            if (hit != null) return hit;
        }
        return null;
    }

    private static bool PartExists(byte[] pptx, string partName)
    {
        using var ms = new MemoryStream(pptx);
        using var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        return archive.GetEntry(partName) != null;
    }
}
