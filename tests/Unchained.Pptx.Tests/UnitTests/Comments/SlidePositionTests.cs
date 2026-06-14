using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Comments;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Comments;

public sealed class SlidePositionTests
{
    [Fact]
    public void Constructor_StoresCoordinates()
    {
        var pos = new SlidePosition(new Emu(100), new Emu(200));
        pos.X.ShouldBe(new Emu(100));
        pos.Y.ShouldBe(new Emu(200));
    }

    [Fact]
    public void Deconstruct_ReturnsCoordinates()
    {
        var pos = new SlidePosition(new Emu(3), new Emu(4));
        var (x, y) = pos;
        x.ShouldBe(new Emu(3));
        y.ShouldBe(new Emu(4));
    }
}
