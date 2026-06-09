using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
/// M5a: the SDK-backed load path keeps its <c>OoxmlEngine</c> open and attaches it to the
/// document (for a future in-place SDK save); the custom path and CreateBlank leave it null.
/// Disposal must release the held engine without error.
/// </summary>
public sealed class SdkEngineAttachmentTests : PptxTestBase
{
    private static byte[] BuildSimplePptx()
    {
        // Round-trip a generated doc through the custom writer to get valid bytes.
        using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(
            Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(2), "Engine attach");
        using var ms = new MemoryStream();
        new PresentationProcessor().SaveAsync(doc, ms).GetAwaiter().GetResult();
        return ms.ToArray();
    }

    [Fact]
    public async Task SdkPath_AttachesEngine_CustomPathDoesNot()
    {
        var bytes = BuildSimplePptx();

        var custom = await Processor.LoadAsync(bytes);
        var sdk = await Processor.LoadAsync(bytes, new OpenOptions { UseOpenXmlEngine = true });

        custom.Engine.ShouldBeNull("custom path must not hold an SDK engine");
        sdk.Engine.ShouldNotBeNull("SDK path must keep its engine open for in-place save");

        custom.Dispose();
        sdk.Dispose();
    }

    [Fact]
    public void CreateBlank_HasNoEngine()
    {
        using var doc = new PresentationProcessor().CreateBlank();
        doc.Engine.ShouldBeNull();
    }

    [Fact]
    public async Task DisposingSdkDocument_ReleasesEngineWithoutError()
    {
        var bytes = BuildSimplePptx();
        var sdk = await Processor.LoadAsync(bytes, new OpenOptions { UseOpenXmlEngine = true });

        // Double dispose must be safe.
        Should.NotThrow(() => sdk.Dispose());
        Should.NotThrow(() => sdk.Dispose());
    }
}
