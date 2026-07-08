using Shouldly;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Unchained.Pptx.Tests.Shared;
using Xunit;
using SdkPresentationDocument = DocumentFormat.OpenXml.Packaging.PresentationDocument;

namespace Unchained.Pptx.Tests.UnitTests.Engine;

/// <summary>
///     Direct unit coverage for <see cref="OpenXmlPresentationWriter" />'s guard paths that the
///     SDK round-trip integration tests do not reach: <see cref="OpenXmlPresentationWriter.CanSave" />
///     for engine-present vs absent, the no-engine <see cref="OpenXmlPresentationWriter.Save" />
///     throw, and the missing-<c>SlideIdList</c> fallback in <c>OrderedSlideParts</c>. The happy path
///     (re-emit + save) is exercised by <c>SdkSaveTests</c>.
/// </summary>
public sealed class OpenXmlPresentationWriterTests
{
    [Fact]
    public void CanSave_BlankDocumentWithoutEngine_False()
    {
        using var doc = new PresentationProcessor().CreateBlank();
        OpenXmlPresentationWriter.CanSave(doc).ShouldBeFalse();
    }

    [Fact]
    public void Save_DocumentWithoutEngine_Throws()
    {
        using var doc = new PresentationProcessor().CreateBlank();
        Should.Throw<InvalidOperationException>(() => OpenXmlPresentationWriter.Save(doc));
    }

    [Fact]
    public async Task Save_WithEngine_ReturnsBytes()
    {
        var bytes = await BuildSimplePptxBytes();
        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(bytes, new OpenOptions { UseOpenXmlEngine = true });

        OpenXmlPresentationWriter.CanSave(doc).ShouldBeTrue();
        OpenXmlPresentationWriter.Save(doc).Length.ShouldBeGreaterThan(0);

        await doc.DisposeAsync();
    }

    [Fact]
    public async Task Save_WithoutSlideIdList_FallsBackToPartEnumeration()
    {
        var bytes = await BuildSimplePptxBytes();
        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(bytes, new OpenOptions { UseOpenXmlEngine = true });

        // Drop the SlideIdList from the held SDK presentation so OrderedSlideParts takes the
        // fallback "enumerate SlideParts directly" branch.
        var sdkDoc = (SdkPresentationDocument)doc.Engine!.Package;
        var presentation = sdkDoc.PresentationPart!.Presentation;
        presentation.ShouldNotBeNull();
        presentation.SlideIdList?.Remove();
        presentation.Save();

        OpenXmlPresentationWriter.Save(doc).Length.ShouldBeGreaterThan(0);

        await doc.DisposeAsync();
    }

    private static async Task<byte[]> BuildSimplePptxBytes()
    {
        await using var doc = PptxFixtures.WithSlides(1);
        using var ms = new MemoryStream();
        await new PresentationProcessor().SaveAsync(doc, ms);
        return ms.ToArray();
    }
}
