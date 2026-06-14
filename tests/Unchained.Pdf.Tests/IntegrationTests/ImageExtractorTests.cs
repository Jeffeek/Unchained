using Shouldly;
using Unchained.Drawing.Constants;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class ImageExtractorTests : PdfTestBase
{
    private static readonly ImageExtractor Extractor = new();

    private static byte[] RedImage(int w, int h)
    {
        var rgb = new byte[w * h * 3];
        for (var i = 0; i < rgb.Length; i += 3)
        {
            rgb[i] = 255;
            rgb[i + 1] = 0;
            rgb[i + 2] = 0;
        }

        return rgb;
    }

    [Fact]
    public async Task ExtractImages_SingleImage_ReturnsOneWithCorrectDimensions()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(8, 6, RedImage(8, 6)));
        var images = await Extractor.ExtractImagesAsync(doc, TestContext.Current.CancellationToken);

        images.Count.ShouldBe(1);
        images[0].Width.ShouldBe(8);
        images[0].Height.ShouldBe(6);
        images[0].PageNumber.ShouldBe(1);
        images[0].RgbData.Length.ShouldBe(8 * 6 * 3);
    }

    [Fact]
    public async Task ExtractImages_DecodesPixelData_RedStaysRed()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(4, 4, RedImage(4, 4)));
        var images = await Extractor.ExtractImagesAsync(doc, TestContext.Current.CancellationToken);

        var rgb = images[0].RgbData;
        rgb[0].ShouldBe((byte)255); // R
        rgb[1].ShouldBe((byte)0);   // G
        rgb[2].ShouldBe((byte)0);   // B
    }

    [Fact]
    public async Task ExtractImages_ToPng_ProducesValidPngSignature()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(4, 4, RedImage(4, 4)));
        var images = await Extractor.ExtractImagesAsync(doc, TestContext.Current.CancellationToken);

        var png = images[0].ToPng();
        png.Length.ShouldBeGreaterThan(8);
        png[..8].ShouldBe(PngConstants.Signature);
    }

    [Fact]
    public async Task ExtractImages_PngRoundTripsThroughDecoder()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(5, 3, RedImage(5, 3)));
        var images = await Extractor.ExtractImagesAsync(doc, TestContext.Current.CancellationToken);
        var png = images[0].ToPng();

        // IHDR width/height are big-endian at byte offsets 16 and 20.
        var w = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        var h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        w.ShouldBe(5);
        h.ShouldBe(3);
    }

    [Fact]
    public async Task ExtractImages_NoImages_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        var images = await Extractor.ExtractImagesAsync(doc, TestContext.Current.CancellationToken);
        images.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExtractImages_ByPage_MatchesWholeDocument()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(4, 4, RedImage(4, 4)));
        var page1 = await Extractor.ExtractImagesAsync(doc, 1, TestContext.Current.CancellationToken);
        page1.Count.ShouldBe(1);
        page1[0].ResourceName.ShouldBe("Im1");
    }

    [Fact]
    public Task ExtractImages_PageOutOfRange_Throws() =>
        Should.ThrowAsync<ArgumentOutOfRangeException>(static async () =>
            {
                await using var doc = await LoadAsync(PdfFixtures.WithImageXObject(4, 4, RedImage(4, 4)));
                await Extractor.ExtractImagesAsync(doc, 99);
            }
        );
}
