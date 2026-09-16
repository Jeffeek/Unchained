using System.IO.Packaging;
using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Covers <see cref="OpenXmlPresentationWriter" /> branches not exercised by
///     <c>SdkSaveTests</c>: transform rotation/flip patching, and replacing an already-present
///     transition or effect list on a second save.
/// </summary>
public sealed class SdkSaveGapTests
{
    private static readonly OpenOptions Sdk = new() { UseOpenXmlEngine = true };
    private static readonly SaveOptions SdkSave = new() { UseOpenXmlEngine = true };

    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", name);

    private static Task<byte[]> SampleBytesAsync()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        return File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
    }

    // The SDK reader does not re-hydrate every written attribute onto the model, so these tests
    // assert on the emitted slide XML — which is what the writer branches under test produce.
    private static IEnumerable<string> SlideXmls(byte[] pptx)
    {
        using var ms = new MemoryStream(pptx);
        using var pkg = Package.Open(ms, FileMode.Open, FileAccess.Read);
        foreach (var part in pkg.GetParts().Where(static part => part.Uri.OriginalString.StartsWith("/ppt/slides/slide", StringComparison.Ordinal)))
        {
            using var stream = part.GetStream();
            using var reader = new StreamReader(stream);
            yield return reader.ReadToEnd();
        }
    }

    [Fact]
    public async Task SdkSave_RotationAndFlips_AreWrittenToTransform()
    {
        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(await SampleBytesAsync(), Sdk, TestContext.Current.CancellationToken);

        var shape = doc.Slides.SelectMany(static s => s.Shapes).OfType<AutoShape>().First();
        shape.RotationDegrees = 45;
        shape.FlipHorizontal = true;
        shape.FlipVertical = true;

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, SdkSave, TestContext.Current.CancellationToken);

        // 45° → 45 * 60000 = 2700000 sixtieths of a degree; flips serialise as flipH/flipV="1".
        SlideXmls(ms.ToArray())
            .ShouldContain(static x => x.Contains("rot=\"2700000\"", StringComparison.Ordinal) &&
                                       x.Contains("flipH=\"1\"", StringComparison.Ordinal) &&
                                       x.Contains("flipV=\"1\"", StringComparison.Ordinal)
            );

        await doc.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_ReplacesExistingTransition_OnSecondSave()
    {
        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(await SampleBytesAsync(), Sdk, TestContext.Current.CancellationToken);
        doc.Slides[0].Transition.Effect = TransitionEffect.Fade;

        using var first = new MemoryStream();
        await processor.SaveAsync(doc, first, SdkSave, TestContext.Current.CancellationToken);
        await doc.DisposeAsync();

        // Reload: the slide now already carries a <p:transition>, so the next save replaces it.
        var reopened = await processor.LoadAsync(first.ToArray(), Sdk, cancellationToken: TestContext.Current.CancellationToken);
        reopened.Slides[0].Transition.Effect = TransitionEffect.PushLeft;

        using var second = new MemoryStream();
        await processor.SaveAsync(reopened, second, SdkSave, TestContext.Current.CancellationToken);

        // The replace path must leave exactly one transition element on the slide.
        var slide0 = SlideXmls(second.ToArray()).First();
        CountOccurrences(slide0, "transition>").ShouldBe(1); // one closing </…:transition>

        await reopened.DisposeAsync();

        return;

        static int CountOccurrences(string haystack, string needle)
        {
            var count = 0;
            for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
                count++;
            return count;
        }
    }

    [Fact]
    public async Task SdkSave_ReplacesExistingEffects_OnSecondSave()
    {
        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(await SampleBytesAsync(), Sdk, TestContext.Current.CancellationToken);

        var shape = doc.Slides.SelectMany(static s => s.Shapes).OfType<AutoShape>().First();
        shape.Name = "Shadowed marker";
        shape.Effects.OuterShadow = new OuterShadowEffect();

        using var first = new MemoryStream();
        await processor.SaveAsync(doc, first, SdkSave, TestContext.Current.CancellationToken);
        await doc.DisposeAsync();

        // Reload: the shape now has an <a:effectLst>, so re-saving replaces it in place.
        var reopened = await processor.LoadAsync(first.ToArray(), Sdk, cancellationToken: TestContext.Current.CancellationToken);
        var reshape = reopened.Slides.SelectMany(static s => s.Shapes).First(static s => s.Name == "Shadowed marker");
        reshape.Effects.OuterShadow = new OuterShadowEffect();

        using var second = new MemoryStream();
        await processor.SaveAsync(reopened, second, SdkSave, TestContext.Current.CancellationToken);

        var reloaded = await processor.LoadAsync(second.ToArray(), Sdk, cancellationToken: TestContext.Current.CancellationToken);
        reloaded.Slides.SelectMany(static s => s.Shapes)
            .First(static s => s.Name == "Shadowed marker")
            .Effects.OuterShadow.ShouldNotBeNull();

        await reopened.DisposeAsync();
        await reloaded.DisposeAsync();
    }
}
