using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class RenderOptionsTests
{
    [Fact]
    public async Task Default_Has150DpiAndPng()
    {
        await Task.CompletedTask;
        RenderOptions.Default.Dpi.ShouldBe(150);
        RenderOptions.Default.Format.ShouldBe(OutputFormat.Png);
    }

    [Fact]
    public async Task HighRes_Has300Dpi()
    {
        await Task.CompletedTask;
        RenderOptions.HighRes.Dpi.ShouldBe(300);
        RenderOptions.HighRes.Format.ShouldBe(OutputFormat.Png);
    }

    [Fact]
    public async Task CustomDpi_Stored()
    {
        await Task.CompletedTask;
        var opts = new RenderOptions(72);
        opts.Dpi.ShouldBe(72);
        opts.Format.ShouldBe(OutputFormat.Png);
    }

    [Fact]
    public async Task RecordEquality_SameDpi_Equal()
    {
        await Task.CompletedTask;
        var a = new RenderOptions(200);
        var b = new RenderOptions(200);
        a.ShouldBe(b);
    }

    [Fact]
    public async Task OutputFormat_PngIsDefined()
    {
        await Task.CompletedTask;
        Enum.IsDefined(OutputFormat.Png).ShouldBeTrue();
    }

    [Fact]
    public async Task WithExpression_CreatesModifiedCopy()
    {
        await Task.CompletedTask;
        var original = RenderOptions.Default;
        var modified = original with { Dpi = 600 };
        modified.Dpi.ShouldBe(600);
        original.Dpi.ShouldBe(150);
    }
}
