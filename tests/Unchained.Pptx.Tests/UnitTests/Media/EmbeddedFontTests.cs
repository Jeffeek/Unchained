using Shouldly;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Media;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Media;

public sealed class EmbeddedFontTests
{
    private static MediaStore StoreWith(params EmbeddedFont[] fonts)
    {
        var store = new MediaStore();
        foreach (var f in fonts)
            store.AddFont(f);
        return store;
    }

    private static EmbeddedFont Font(string typeface, EmbeddedFontStyle style, byte marker) =>
        new() { Typeface = typeface, Style = style, Data = new[] { marker } };

    [Fact]
    public void FindFontData_ExactStyleMatch_ReturnsThatVariant()
    {
        var store = StoreWith(
            Font("DM Sans", EmbeddedFontStyle.Regular, 1),
            Font("DM Sans", EmbeddedFontStyle.Bold, 2));

        var data = store.FindFontData("DM Sans", EmbeddedFontStyle.Bold);

        data.ShouldNotBeNull();
        data.Value.Span[0].ShouldBe((byte)2);
    }

    [Fact]
    public void FindFontData_MissingStyle_FallsBackToRegular()
    {
        var store = StoreWith(Font("DM Sans", EmbeddedFontStyle.Regular, 1));

        var data = store.FindFontData("DM Sans", EmbeddedFontStyle.BoldItalic);

        data.ShouldNotBeNull();
        data.Value.Span[0].ShouldBe((byte)1);
    }

    [Fact]
    public void FindFontData_IsCaseInsensitive()
    {
        var store = StoreWith(Font("Georgia Pro Light", EmbeddedFontStyle.Regular, 7));

        store.FindFontData("georgia pro light", EmbeddedFontStyle.Regular).ShouldNotBeNull();
    }

    [Fact]
    public void FindFontData_UnknownTypeface_ReturnsNull()
    {
        var store = StoreWith(Font("DM Sans", EmbeddedFontStyle.Regular, 1));

        store.FindFontData("Comic Sans", EmbeddedFontStyle.Regular).ShouldBeNull();
    }

    [Fact]
    public void FindFontData_NoEmbeddedFonts_ReturnsNull() =>
        new MediaStore().FindFontData("DM Sans", EmbeddedFontStyle.Regular).ShouldBeNull();
}
