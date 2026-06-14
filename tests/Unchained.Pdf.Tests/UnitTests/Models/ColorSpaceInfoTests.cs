using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class ColorSpaceInfoTests
{
    [Fact]
    public void Device_FactorySetsKind()
    {
        ColorSpaceInfo.Device("DeviceRGB").Kind.ShouldBe("DeviceRGB");
        ColorSpaceInfo.Device("DeviceGray").Kind.ShouldBe("DeviceGray");
    }

    [Fact]
    public void DeviceRgb_ToRgb_PassesComponentsThrough()
    {
        var info = ColorSpaceInfo.Device("DeviceRGB");
        info.ToRgb([1.0, 0.0, 0.0]).ShouldBe(((byte)255, (byte)0, (byte)0));
        info.ToRgb([0.0, 1.0, 0.0]).ShouldBe(((byte)0, (byte)255, (byte)0));
    }

    [Fact]
    public void DeviceRgb_TooFewComponents_ReturnsGrey() =>
        ColorSpaceInfo.Device("DeviceRGB").ToRgb([0.5]).ShouldBe(((byte)128, (byte)128, (byte)128));

    [Fact]
    public void DeviceGray_ToRgb_ReplicatesSingleChannel()
    {
        var info = ColorSpaceInfo.Device("DeviceGray");
        info.ToRgb([0.0]).ShouldBe(((byte)0, (byte)0, (byte)0));
        info.ToRgb([1.0]).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void DeviceCmyk_AllZero_ReturnsWhite() =>
        ColorSpaceInfo.Device("DeviceCMYK").ToRgb([0, 0, 0, 0]).ShouldBe(((byte)255, (byte)255, (byte)255));

    [Fact]
    public void DeviceCmyk_BlackChannel_ReturnsBlack() =>
        ColorSpaceInfo.Device("DeviceCMYK").ToRgb([0, 0, 0, 1]).ShouldBe(((byte)0, (byte)0, (byte)0));

    [Fact]
    public void DeviceCmyk_TooFewComponents_ReturnsGrey() =>
        ColorSpaceInfo.Device("DeviceCMYK").ToRgb([0, 0, 0]).ShouldBe(((byte)128, (byte)128, (byte)128));

    [Fact]
    public void IccBased_DelegatesToAlternate()
    {
        var info = ColorSpaceInfo.IccBased("DeviceRGB");
        info.Kind.ShouldBe("ICCBased");
        info.AlternateSpace.ShouldBe("DeviceRGB");
        info.ToRgb([1.0, 0.0, 0.0]).ShouldBe(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void IccBased_CmykAlternate_ConvertsAsCmyk() =>
        ColorSpaceInfo.IccBased("DeviceCMYK").ToRgb([0, 0, 0, 0]).ShouldBe(((byte)255, (byte)255, (byte)255));

    [Fact]
    public void Separation_NullTint_ReturnsGrey()
    {
        var info = ColorSpaceInfo.Separation(null, "DeviceCMYK");
        info.Kind.ShouldBe("Separation");
        info.ToRgb([0.5]).ShouldBe(((byte)128, (byte)128, (byte)128));
    }

    [Fact]
    public void DeviceN_NullTint_ReturnsGrey()
    {
        var info = ColorSpaceInfo.DeviceN(null, "DeviceRGB");
        info.Kind.ShouldBe("DeviceN");
        info.ToRgb([0.5, 0.5]).ShouldBe(((byte)128, (byte)128, (byte)128));
    }

    [Fact]
    public void Indexed_GrayPalette_LooksUpEntry()
    {
        // Two-entry gray palette: index 0 = black, index 1 = white.
        var info = ColorSpaceInfo.Indexed([0x00, 0xFF], channels: 1, baseSpace: "DeviceGray");
        info.Kind.ShouldBe("Indexed");
        // Component 0 → index round(0*255)=0 → black.
        info.ToRgb([0.0]).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void Indexed_RgbPalette_ReturnsTriple()
    {
        // Single RGB entry at index 0.
        var info = ColorSpaceInfo.Indexed([0x10, 0x20, 0x30], channels: 3, baseSpace: "DeviceRGB");
        info.ToRgb([0.0]).ShouldBe(((byte)0x10, (byte)0x20, (byte)0x30));
    }

    [Fact]
    public void Indexed_NullLookup_ReturnsGrey()
    {
        var info = new ColorSpaceInfo { Kind = "Indexed" };
        info.ToRgb([0.0]).ShouldBe(((byte)128, (byte)128, (byte)128));
    }

    [Fact]
    public void CalGray_Factory_SetsGamma()
    {
        var info = ColorSpaceInfo.CalGrayInfo(2.2);
        info.Kind.ShouldBe("CalGray");
        info.CalGrayGamma.ShouldBe(2.2);
    }

    [Fact]
    public void CalRgb_Factory_SetsGammaAndMatrix()
    {
        var info = ColorSpaceInfo.CalRgb([1.0, 1.0, 1.0], null);
        info.Kind.ShouldBe("CalRGB");
        info.CalRgbGamma.ShouldBe([1.0, 1.0, 1.0]);
        // Identity-ish conversion should not throw and yields a valid triple.
        var (r, g, b) = info.ToRgb([1.0, 1.0, 1.0]);
        (r + g + b).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Lab_Factory_ProducesValidTriple()
    {
        var info = ColorSpaceInfo.Lab();
        info.Kind.ShouldBe("Lab");
        // L*=100 (white-ish) should not throw.
        var rgb = info.ToRgb([100, 0, 0]);
        rgb.R.ShouldBeGreaterThan((byte)0);
    }

    [Fact]
    public void Lab_TooFewComponents_ReturnsGrey() =>
        ColorSpaceInfo.Lab().ToRgb([50, 0]).ShouldBe(((byte)128, (byte)128, (byte)128));
}
