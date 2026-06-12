using System.IO.Packaging;
using Shouldly;
using Unchained.Ooxml.Engine;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Verifies the foundation of the SDK-backed write path (M5): an Open(editable) → Save() round
///     trip through <see cref="OoxmlEngine" /> preserves every OPC part. This is why an SDK-based save
///     avoids the part-dropping the custom writer suffers — the SDK mutates the opened package in
///     place rather than rebuilding it from scratch.
/// </summary>
public sealed class OoxmlEngineRoundTripTests
{
    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", name);

    [
        Theory,
        InlineData("shp-shapes.pptx"),
        InlineData("cht-charts.pptx"),
        InlineData("prs-notes.pptx"),
        InlineData("dml-fill.pptx")
    ]
    public void OpenSaveRoundTrip_PreservesPartCount(string fileName)
    {
        var path = SamplePath(fileName);
        Assert.SkipUnless(File.Exists(path), $"sample {fileName} not copied to output");

        var bytes = File.ReadAllBytes(path);
        var before = CountParts(bytes);

        byte[] saved;
        using (var engine = OoxmlEngine.Open(bytes))
            saved = engine.Save();

        CountParts(saved).ShouldBe(before, $"{fileName}: part count preserved on SDK round-trip");
    }

    private static int CountParts(byte[] pptx)
    {
        using var ms = new MemoryStream(pptx);
        using var pkg = Package.Open(ms, FileMode.Open, FileAccess.Read);
        return pkg.GetParts().Count();
    }
}
