using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Unit tests for <see cref="PageImageExtractor" /> that decode image XObjects to packed RGB,
///     exercising DeviceGray/DeviceCMYK channels, 1-bpc bi-level unpacking, Indexed palettes,
///     /Decode arrays, /SMask soft masks, and the grey-placeholder fallback for unsupported data.
/// </summary>
public sealed class PageImageExtractorTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    // Builds a page dict whose /Resources /XObject holds one image named Im1.
    private static PdfDictionary PageWithImage(PdfObject image)
    {
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["XObject"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["Im1"] = image })
            }
        );
        return new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );
    }

    private static PdfStream Image(
        int w,
        int h,
        string colorSpace,
        int bpc,
        byte[] data,
        params (string Key, PdfObject Value)[] extra
    )
    {
        var entries = new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("XObject"),
            ["Subtype"] = PdfName.Get("Image"),
            ["Width"] = new PdfInteger(w),
            ["Height"] = new PdfInteger(h),
            ["ColorSpace"] = PdfName.Get(colorSpace),
            ["BitsPerComponent"] = new PdfInteger(bpc),
            ["Length"] = new PdfInteger(data.Length)
        };
        foreach (var (k, v) in extra) entries[k] = v;
        return new PdfStream(new PdfDictionary(entries), data);
    }

    [Fact]
    public void DeviceRgb_DecodesToPackedRgb()
    {
        var data = new byte[] { 255, 0, 0, 0, 255, 0 }; // 2 pixels: red, green
        var result = PageImageExtractor.GetImageXObjects(PageWithImage(Image(2, 1, "DeviceRGB", 8, data)), Core());
        var img = result["Im1"];
        img.Width.ShouldBe(2);
        // Pixel 0: red = (255, 0, 0)
        img.RgbData[0].ShouldBe((byte)255);
        img.RgbData[1].ShouldBe((byte)0);
        img.RgbData[2].ShouldBe((byte)0);
        // Pixel 1: green = (0, 255, 0)
        img.RgbData[3].ShouldBe((byte)0);
        img.RgbData[4].ShouldBe((byte)255);
        img.RgbData[5].ShouldBe((byte)0);
    }

    [Fact]
    public void DeviceGray_ReplicatesChannel()
    {
        var data = new byte[] { 0, 128, 255 }; // 3 gray pixels
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(3, 1, "DeviceGray", 8, data)), Core())["Im1"];
        img.RgbData[0].ShouldBe((byte)0);
        img.RgbData[1].ShouldBe((byte)0);
        img.RgbData[3].ShouldBe((byte)128);
        img.RgbData[4].ShouldBe((byte)128);
    }

    [Fact]
    public void DeviceCmyk_ConvertsToRgb()
    {
        // One CMYK pixel: 0,0,0,0 = white.
        var data = "\0\0\0\0"u8.ToArray();
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(1, 1, "DeviceCMYK", 8, data)), Core())["Im1"];
        img.RgbData[0].ShouldBe((byte)255);
        img.RgbData[1].ShouldBe((byte)255);
        img.RgbData[2].ShouldBe((byte)255);
    }

    [Fact]
    public void DeviceGray_OneBitPerComponent_UnpacksBits()
    {
        // 8 pixels in one byte: 0b10101010 → white,black,white,black...
        var data = new byte[] { 0b10101010 };
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(8, 1, "DeviceGray", 1, data)), Core())["Im1"];
        img.RgbData[0].ShouldBe((byte)255); // bit 1 → white
        img.RgbData[3].ShouldBe((byte)0);   // bit 0 → black
    }

    [Fact]
    public void DeviceGray_OneBit_WithInvertingDecode_FlipsBlackWhite()
    {
        var decode = new PdfArray([new PdfReal(1), new PdfReal(0)]);
        var data = new byte[] { 0b10000000 }; // first pixel bit=1
        var img = PageImageExtractor.GetImageXObjects(
            PageWithImage(
                Image(
                    8,
                    1,
                    "DeviceGray",
                    1,
                    data,
                    ("Decode", decode)
                )
            ),
            Core()
        )["Im1"];
        // Inverted: bit 1 → black.
        img.RgbData[0].ShouldBe((byte)0);
    }

    [Fact]
    public void IndexedPalette_LooksUpColors()
    {
        // Indexed [/Indexed /DeviceRGB 1 <lookup>] with 2 RGB entries.
        var lookup = new PdfString(new byte[] { 0x10, 0x20, 0x30, 0xFF, 0xFF, 0xFF });
        var cs = new PdfArray(
            [PdfName.Get("Indexed"), PdfName.Get("DeviceRGB"), new PdfInteger(1), lookup]
        );
        // Two pixels: index 0, index 1.
        var data = new byte[] { 0, 1 };
        var image = Image(
            2,
            1,
            "DeviceRGB",
            8,
            data,
            ("ColorSpace", cs)
        );
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(image), Core())["Im1"];
        img.RgbData[0].ShouldBe((byte)0x10);
        img.RgbData[1].ShouldBe((byte)0x20);
        img.RgbData[3].ShouldBe((byte)0xFF);
    }

    [Fact]
    public void DecodeArray_InvertsRgbChannel()
    {
        // Decode [1 0 1 0 1 0] inverts all 3 channels: black input → white output.
        var decode = new PdfArray(
            [new PdfReal(1), new PdfReal(0), new PdfReal(1), new PdfReal(0), new PdfReal(1), new PdfReal(0)]
        );
        var data = "\0\0\0"u8.ToArray();
        var img = PageImageExtractor.GetImageXObjects(
            PageWithImage(
                Image(
                    1,
                    1,
                    "DeviceRGB",
                    8,
                    data,
                    ("Decode", decode)
                )
            ),
            Core()
        )["Im1"];
        img.RgbData[0].ShouldBe((byte)255);
    }

    [Fact]
    public void SoftMask_ProducesAlphaChannel()
    {
        // Base image: 2×1 white DeviceGray. SMask: 2×1 DeviceGray with values 0,255.
        var maskData = new byte[] { 0, 255 };
        var maskStream = new PdfStream(
            new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    ["Type"] = PdfName.Get("XObject"),
                    ["Subtype"] = PdfName.Get("Image"),
                    ["Width"] = new PdfInteger(2),
                    ["Height"] = new PdfInteger(1),
                    ["ColorSpace"] = PdfName.Get("DeviceGray"),
                    ["BitsPerComponent"] = new PdfInteger(8),
                    ["Length"] = new PdfInteger(maskData.Length)
                }
            ),
            maskData
        );
        var baseData = new byte[] { 200, 200 };
        var img = PageImageExtractor.GetImageXObjects(
            PageWithImage(
                Image(
                    2,
                    1,
                    "DeviceGray",
                    8,
                    baseData,
                    ("SMask", maskStream)
                )
            ),
            Core()
        )["Im1"];
        img.Alpha.ShouldNotBeNull();
        img.Alpha!.Length.ShouldBe(2);
        img.Alpha[0].ShouldBe((byte)0);
        img.Alpha[1].ShouldBe((byte)255);
    }

    [Fact]
    public void ZeroDimensions_SkippedFromResult()
    {
        var image = Image(0, 0, "DeviceRGB", 8, []);
        PageImageExtractor.GetImageXObjects(PageWithImage(image), Core()).ContainsKey("Im1").ShouldBeFalse();
    }

    [Fact]
    public void NonImageXObject_Ignored()
    {
        var form = new PdfStream(
            new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    ["Type"] = PdfName.Get("XObject"),
                    ["Subtype"] = PdfName.Get("Form")
                }
            ),
            ReadOnlyMemory<byte>.Empty
        );
        PageImageExtractor.GetImageXObjects(PageWithImage(form), Core()).ShouldBeEmpty();
    }

    [Fact]
    public void MismatchedDataLength_FallsBackGracefully()
    {
        // Declared DeviceRGB 2×1 (needs 6 bytes) but only 2 supplied → re-infers / placeholder.
        var img = PageImageExtractor.GetImageXObjects(
            PageWithImage(Image(2, 1, "DeviceRGB", 8, [10, 20])),
            Core()
        )["Im1"];
        img.RgbData.Length.ShouldBe(2 * 1 * 3);
    }

    [Fact]
    public void NoXObjectResources_ReturnsEmpty()
    {
        var page = new PdfDictionary(new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page });
        PageImageExtractor.GetImageXObjects(page, Core()).ShouldBeEmpty();
    }
}
