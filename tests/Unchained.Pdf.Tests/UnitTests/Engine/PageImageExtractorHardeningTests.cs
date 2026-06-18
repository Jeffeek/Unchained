using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Hardening branch coverage for <see cref="PageImageExtractor" />: indexed images at 1/2/4
///     bits-per-component (the sub-byte <c>ReadSample</c> arms), an indexed CMYK base palette,
///     palette index clamping and out-of-range lookup, the indexed-palette rejection paths
///     (unknown base, negative hival, empty lookup), an SMask with zero dimensions, and the
///     unsupported-format grey placeholder fallback.
/// </summary>
public sealed class PageImageExtractorHardeningTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

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
        int bpc,
        byte[] data,
        PdfObject colorSpace
    )
    {
        var entries = new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("XObject"),
            ["Subtype"] = PdfName.Get("Image"),
            ["Width"] = new PdfInteger(w),
            ["Height"] = new PdfInteger(h),
            ["ColorSpace"] = colorSpace,
            ["BitsPerComponent"] = new PdfInteger(bpc),
            ["Length"] = new PdfInteger(data.Length)
        };
        return new PdfStream(new PdfDictionary(entries), data);
    }

    private static PdfArray Indexed(string baseName, int hival, byte[] lookup) =>
        new([PdfName.Get("Indexed"), PdfName.Get(baseName), new PdfInteger(hival), new PdfString(lookup)]);

    [Fact]
    public void Indexed_FourBitSamples_ReadsNibbles()
    {
        // 4-bpc indexed: one byte 0x01 → two pixels (index 0, index 1).
        var cs = Indexed("DeviceRGB", 1, [0x11, 0x22, 0x33, 0xAA, 0xBB, 0xCC]);
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(2, 1, 4, [0x01], cs)), Core())["Im1"];
        img.RgbData[0].ShouldBe((byte)0x11); // pixel 0 = palette entry 0
        img.RgbData[3].ShouldBe((byte)0xAA); // pixel 1 = palette entry 1
    }

    [Fact]
    public void Indexed_OneBitSamples_ReadsBits()
    {
        // 1-bpc indexed: 0b10000000 → pixel0=index1, pixel1=index0 ...
        var cs = Indexed("DeviceRGB", 1, [0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF]);
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(2, 1, 1, [0b10000000], cs)), Core())["Im1"];
        img.RgbData[0].ShouldBe((byte)0xFF); // index 1 → white
        img.RgbData[3].ShouldBe((byte)0x00); // index 0 → black
    }

    [Fact]
    public void Indexed_TwoBitSamples_ReadsPairs()
    {
        var cs = Indexed("DeviceGray", 3, [0, 85, 170, 255]);
        // 2-bpc: 0b00011011 → indices 0,1,2,3.
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(4, 1, 2, [0b00011011], cs)), Core())["Im1"];
        img.RgbData[0].ShouldBe((byte)0);
        img.RgbData[9].ShouldBe((byte)255); // 4th pixel (index 3)
    }

    [Fact]
    public void Indexed_CmykBase_ConvertsPaletteToRgb()
    {
        // Indexed over DeviceCMYK: palette entry 0 = 0,0,0,0 (white).
        var cs = Indexed("DeviceCMYK", 0, "\0\0\0\0"u8.ToArray());
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(1, 1, 8, [0], cs)), Core())["Im1"];
        img.RgbData[0].ShouldBe((byte)255);
    }

    [Fact]
    public void Indexed_IndexBeyondHival_IsClamped()
    {
        // hival 0 but the sample is 5 → clamped to palette entry 0.
        var cs = Indexed("DeviceRGB", 0, [10, 20, 30]);
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(1, 1, 8, [5], cs)), Core())["Im1"];
        img.RgbData[0].ShouldBe((byte)10);
    }

    [Fact]
    public void Indexed_LookupTooShortForIndex_ProducesBlack()
    {
        // hival 3 but lookup holds only 1 RGB entry; index 1 reads past the lookup → black.
        var cs = Indexed("DeviceRGB", 3, [10, 20, 30]);
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(2, 1, 8, [0, 1], cs)), Core())["Im1"];
        img.RgbData[0].ShouldBe((byte)10); // entry 0 ok
        img.RgbData[3].ShouldBe((byte)0);  // entry 1 out of range → black
    }

    [Fact]
    public void Indexed_UnknownBaseSpace_NotTreatedAsIndexed()
    {
        // Base space with 0 channels (unknown) → ReadIndexedPalette returns null; falls to the
        // normal decode path which (DeviceRGB declared, 1 byte) yields a grey placeholder.
        var cs = Indexed("Frobnicate", 1, [1, 2, 3]);
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(1, 1, 8, [0], cs)), Core())["Im1"];
        img.RgbData.Length.ShouldBe(3);
    }

    [Fact]
    public void Indexed_NegativeHival_Rejected()
    {
        var cs = new PdfArray(
            [PdfName.Get("Indexed"), PdfName.Get("DeviceRGB"), new PdfInteger(-1), new PdfString(new byte[] { 1, 2, 3 })]
        );
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(1, 1, 8, [0], cs)), Core())["Im1"];
        img.RgbData.Length.ShouldBe(3);
    }

    [Fact]
    public void Indexed_EmptyLookup_Rejected()
    {
        var cs = Indexed("DeviceRGB", 0, []);
        var img = PageImageExtractor.GetImageXObjects(PageWithImage(Image(1, 1, 8, [0], cs)), Core())["Im1"];
        img.RgbData.Length.ShouldBe(3);
    }

    [Fact]
    public void SoftMask_ZeroDimensions_NoAlpha()
    {
        var maskStream = new PdfStream(
            new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    ["Type"] = PdfName.Get("XObject"),
                    ["Subtype"] = PdfName.Get("Image"),
                    ["Width"] = new PdfInteger(0),
                    ["Height"] = new PdfInteger(0),
                    ["ColorSpace"] = PdfName.Get("DeviceGray"),
                    ["BitsPerComponent"] = new PdfInteger(8),
                    ["Length"] = new PdfInteger(0)
                }
            ),
            ReadOnlyMemory<byte>.Empty
        );
        var entries = new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("XObject"),
            ["Subtype"] = PdfName.Get("Image"),
            ["Width"] = new PdfInteger(1),
            ["Height"] = new PdfInteger(1),
            ["ColorSpace"] = PdfName.Get("DeviceGray"),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["Length"] = new PdfInteger(1),
            ["SMask"] = maskStream
        };
        var img = PageImageExtractor.GetImageXObjects(
            PageWithImage(new PdfStream(new PdfDictionary(entries), new byte[] { 200 })),
            Core()
        )["Im1"];
        img.Alpha.ShouldBeNull();
    }

    [Fact]
    public void SoftMask_DownsampledToBaseGrid()
    {
        // 2×2 base, 1×1 mask → the nearest-neighbour resample (smw != baseW) branch runs.
        var maskStream = new PdfStream(
            new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    ["Type"] = PdfName.Get("XObject"),
                    ["Subtype"] = PdfName.Get("Image"),
                    ["Width"] = new PdfInteger(1),
                    ["Height"] = new PdfInteger(1),
                    ["ColorSpace"] = PdfName.Get("DeviceGray"),
                    ["BitsPerComponent"] = new PdfInteger(8),
                    ["Length"] = new PdfInteger(1)
                }
            ),
            new byte[] { 128 }
        );
        var entries = new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("XObject"),
            ["Subtype"] = PdfName.Get("Image"),
            ["Width"] = new PdfInteger(2),
            ["Height"] = new PdfInteger(2),
            ["ColorSpace"] = PdfName.Get("DeviceGray"),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["Length"] = new PdfInteger(4),
            ["SMask"] = maskStream
        };
        var img = PageImageExtractor.GetImageXObjects(
            PageWithImage(new PdfStream(new PdfDictionary(entries), new byte[] { 10, 20, 30, 40 })),
            Core()
        )["Im1"];
        img.Alpha.ShouldNotBeNull();
        img.Alpha!.Length.ShouldBe(4);
        img.Alpha[0].ShouldBe((byte)128);
    }

    [Fact]
    public void UnsupportedColorSpace_FallsBackToGreyPlaceholder()
    {
        // 16-bpc DeviceRGB hits no decode case → default grey placeholder (128).
        var data = new byte[2 * 3 * 2]; // 2 px × 3 ch × 2 bytes
        var img = PageImageExtractor.GetImageXObjects(
            PageWithImage(Image(2, 1, 16, data, PdfName.Get("DeviceRGB"))),
            Core()
        )["Im1"];
        img.RgbData[0].ShouldBe((byte)128);
    }
}
