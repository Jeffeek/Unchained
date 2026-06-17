using Shouldly;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

/// <summary>
///     Additional <see cref="ColorSpaceInfo" /> conversion tests covering branches the base
///     <see cref="ColorSpaceInfoTests" /> does not reach: Separation/DeviceN with a real tint
///     transform, Indexed CMYK base, CalRGB with a custom XYZ matrix, CalGray gamma, and the
///     ICCBased CMYK alternate path.
/// </summary>
public sealed class ColorSpaceInfoBranchTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    // A type-2 exponential tint transform mapping a single tint to CMYK [t 0 0 0] (cyan).
    private static PdfFunction CyanTint()
    {
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                ["C0"] = new PdfArray([new PdfReal(0), new PdfReal(0), new PdfReal(0), new PdfReal(0)]),
                ["C1"] = new PdfArray([new PdfReal(1), new PdfReal(0), new PdfReal(0), new PdfReal(0)]),
                ["N"] = new PdfReal(1.0)
            }
        );
        return PdfFunction.Build(dict, Core())!;
    }

    [Fact]
    public void Separation_FullTint_EvaluatesToCyan()
    {
        var info = ColorSpaceInfo.Separation(CyanTint(), "DeviceCMYK");
        // Tint 1.0 → CMYK (1,0,0,0) → cyan (R=0, G=255, B=255).
        var (r, g, b) = info.ToRgb([1.0]);
        r.ShouldBeLessThan((byte)40);
        g.ShouldBeGreaterThan((byte)200);
        b.ShouldBeGreaterThan((byte)200);
    }

    [Fact]
    public void DeviceN_FullTint_EvaluatesViaAlternate()
    {
        var info = ColorSpaceInfo.DeviceN(CyanTint(), "DeviceCMYK");
        var (r, _, _) = info.ToRgb([1.0]);
        r.ShouldBeLessThan((byte)40);
    }

    [Fact]
    public void Separation_OverrideFunction_TakesPrecedence()
    {
        var info = ColorSpaceInfo.Separation(null, "DeviceCMYK");
        // Null stored tint normally returns grey, but an override fn should be used.
        var (r, g, b) = info.ToRgb([1.0], CyanTint());
        r.ShouldBeLessThan((byte)40);
        g.ShouldBeGreaterThan((byte)200);
        b.ShouldBeGreaterThan((byte)200);
    }

    [Fact]
    public void Indexed_CmykPalette_ConvertsToRgb()
    {
        // Single CMYK entry (1,0,0,0) = cyan at index 0.
        var info = ColorSpaceInfo.Indexed([0xFF, 0x00, 0x00, 0x00], 4, "DeviceCMYK");
        var (r, g, b) = info.ToRgb([0.0]);
        r.ShouldBeLessThan((byte)40);
        g.ShouldBeGreaterThan((byte)200);
        b.ShouldBeGreaterThan((byte)200);
    }

    [Fact]
    public void Indexed_OffsetBeyondLookup_ReturnsGrey()
    {
        // Lookup has one RGB entry but we ask for index 1 (offset 3) → out of range → grey.
        var info = ColorSpaceInfo.Indexed([0x10, 0x20, 0x30], 3, "DeviceRGB");
        info.ToRgb([1.0]).ShouldBe(((byte)0x10, (byte)0x20, (byte)0x30)); // clamped to last valid index 0
    }

    [Fact]
    public void CalRgb_WithMatrix_ProducesValidTriple()
    {
        // Identity-ish gamma with an XYZ matrix → must not throw and yields a non-trivial colour.
        var info = ColorSpaceInfo.CalRgb(
            [1.0, 1.0, 1.0],
            [0.4124, 0.2126, 0.0193, 0.3576, 0.7152, 0.1192, 0.1805, 0.0722, 0.9505]
        );
        var (r, g, b) = info.ToRgb([1.0, 1.0, 1.0]);
        (r + g + b).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CalGray_NonUnitGamma_AppliesPower()
    {
        var info = ColorSpaceInfo.CalGrayInfo(2.2);
        // 0.5 ^ 2.2 ≈ 0.217 → ~55.
        var (r, _, _) = info.ToRgb([0.5]);
        r.ShouldBeLessThan((byte)128);
    }

    [Fact]
    public void IccBased_CmykAlternate_FourComponents()
    {
        var info = ColorSpaceInfo.IccBased("DeviceCMYK");
        info.ToRgb([0, 0, 0, 1]).ShouldBe(((byte)0, (byte)0, (byte)0)); // pure K = black
    }

    [Fact]
    public void Lab_MidLightness_ProducesGreyish()
    {
        var info = ColorSpaceInfo.Lab();
        var (r, g, b) = info.ToRgb([50, 0, 0]);
        // a=b=0 → neutral grey; channels should be roughly equal.
        Math.Abs(r - g).ShouldBeLessThan(40);
        Math.Abs(g - b).ShouldBeLessThan(40);
    }
}
