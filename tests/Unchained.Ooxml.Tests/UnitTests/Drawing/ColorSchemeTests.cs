using Shouldly;
using Unchained.Ooxml.Drawing;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Drawing;

public sealed class ColorSchemeTests
{
    [
        Theory,
        InlineData(ThemeColorSlot.Dark1),
        InlineData(ThemeColorSlot.Light1),
        InlineData(ThemeColorSlot.Dark2),
        InlineData(ThemeColorSlot.Light2),
        InlineData(ThemeColorSlot.Accent1),
        InlineData(ThemeColorSlot.Accent2),
        InlineData(ThemeColorSlot.Accent3),
        InlineData(ThemeColorSlot.Accent4),
        InlineData(ThemeColorSlot.Accent5),
        InlineData(ThemeColorSlot.Accent6),
        InlineData(ThemeColorSlot.Hyperlink),
        InlineData(ThemeColorSlot.FollowedHyperlink)
    ]
    public void Indexer_GetAfterSet_RoundTripsEverySlot(ThemeColorSlot slot)
    {
        var color = ColorSpec.FromRgb(0x12, 0x34, 0x56);
        var scheme = new ColorScheme { [slot] = color };
        scheme[slot].ShouldBe(color);
    }

    [Fact]
    public void Indexer_MapsToNamedProperties()
    {
        var scheme = new ColorScheme
        {
            [ThemeColorSlot.Accent1] = ColorSpec.FromRgb(0xAA, 0, 0),
            [ThemeColorSlot.Hyperlink] = ColorSpec.FromRgb(0, 0xBB, 0),
            [ThemeColorSlot.FollowedHyperlink] = ColorSpec.FromRgb(0, 0, 0xCC)
        };
        scheme.Accent1.ShouldBe(ColorSpec.FromRgb(0xAA, 0, 0));
        scheme.HyperlinkColor.ShouldBe(ColorSpec.FromRgb(0, 0xBB, 0));
        scheme.FollowedHyperlinkColor.ShouldBe(ColorSpec.FromRgb(0, 0, 0xCC));
    }

    [Fact]
    public void Indexer_Get_UnknownSlot_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => _ = new ColorScheme()[(ThemeColorSlot)999]);

    [Fact]
    public void Indexer_Set_UnknownSlot_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(static () => new ColorScheme()[(ThemeColorSlot)999] = ColorSpec.FromRgb(0, 0, 0));

    [Fact]
    public void Resolve_InitialisedSlot_ReturnsArgb()
    {
        var scheme = new ColorScheme { [ThemeColorSlot.Accent2] = ColorSpec.FromRgb(0x44, 0x72, 0xC4) };
        scheme.Resolve(ThemeColorSlot.Accent2).ShouldBe(0xFF4472C4u);
    }
}
