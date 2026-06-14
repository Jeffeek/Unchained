using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class MdLoadOptionsTests
{
    [Fact]
    public async Task Default_HasExpectedValues()
    {
        await Task.CompletedTask;
        var opts = MdLoadOptions.Default;
        opts.BodyFontName.ShouldBe("Helvetica");
        opts.BodyFontSize.ShouldBe(11f);
        opts.CodeFontName.ShouldBe("Courier");
        opts.CodeFontSize.ShouldBe(10f);
        opts.LineSpacing.ShouldBe(1.4f);
        opts.ParagraphSpacingPt.ShouldBe(8f);
        opts.MarginPt.ShouldBe(72f);
        opts.PageWidthPt.ShouldBe(595f);
        opts.PageHeightPt.ShouldBe(842f);
    }

    [Fact]
    public async Task CustomConstructor_StoresAllValues()
    {
        await Task.CompletedTask;
        var opts = new MdLoadOptions(
            "Times-Roman",
            12f,
            "Courier-Bold",
            9f,
            1.5f,
            10f,
            36f,
            612f,
            792f
        );
        opts.BodyFontName.ShouldBe("Times-Roman");
        opts.BodyFontSize.ShouldBe(12f);
        opts.CodeFontName.ShouldBe("Courier-Bold");
        opts.CodeFontSize.ShouldBe(9f);
        opts.LineSpacing.ShouldBe(1.5f);
        opts.ParagraphSpacingPt.ShouldBe(10f);
        opts.MarginPt.ShouldBe(36f);
        opts.PageWidthPt.ShouldBe(612f);
        opts.PageHeightPt.ShouldBe(792f);
    }

    [
        Theory,
        InlineData(1, 22f),   // 11 * 2.0
        InlineData(2, 17.6f), // 11 * 1.6
        InlineData(3, 14.3f), // 11 * 1.3
        InlineData(4, 12.1f), // 11 * 1.1
        InlineData(5, 11f),   // 11 * 1.0
        InlineData(6, 9.9f)   // 11 * 0.9
    ]
    public async Task HeadingFontSize_DefaultBodyFont_ReturnsExpectedSize(int level, float expected)
    {
        await Task.CompletedTask;
        var opts = MdLoadOptions.Default;
        opts.HeadingFontSize(level).ShouldBe(expected, 0.01f);
    }

    [Fact]
    public async Task HeadingFontSize_CustomBodyFontSize_ScalesCorrectly()
    {
        await Task.CompletedTask;
        var opts = new MdLoadOptions(BodyFontSize: 10f);
        opts.HeadingFontSize(1).ShouldBe(20f, 0.01f);
        opts.HeadingFontSize(2).ShouldBe(16f, 0.01f);
        opts.HeadingFontSize(3).ShouldBe(13f, 0.01f);
    }

    [Fact]
    public async Task HeadingFontSize_LevelAbove6_UsesBodyTimes09()
    {
        await Task.CompletedTask;
        var opts = MdLoadOptions.Default;
        // level 7 hits the default branch: BodyFontSize * 0.9
        opts.HeadingFontSize(7).ShouldBe(opts.BodyFontSize * 0.9f, 0.01f);
    }

    [Fact]
    public async Task WithExpression_CreatesModifiedCopy()
    {
        await Task.CompletedTask;
        var original = MdLoadOptions.Default;
        var modified = original with { BodyFontSize = 14f };
        modified.BodyFontSize.ShouldBe(14f);
        original.BodyFontSize.ShouldBe(11f);
    }
}
