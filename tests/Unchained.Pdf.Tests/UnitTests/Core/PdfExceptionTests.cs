using Shouldly;
using Unchained.Pdf.Core;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Core;

public sealed class PdfExceptionTests
{
    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var ex = new PdfException("bad pdf");
        ex.Message.ShouldBe("bad pdf");
        ex.ByteOffset.ShouldBeNull();
    }

    [Fact]
    public void InnerExceptionConstructor_SetsInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new PdfException("outer", inner);
        ex.InnerException.ShouldBeSameAs(inner);
        ex.ByteOffset.ShouldBeNull();
    }

    [Fact]
    public void ByteOffsetConstructor_SetsOffset()
    {
        var ex = new PdfException("bad token", 0x1F4L);
        ex.ByteOffset.ShouldBe(0x1F4L);
    }

    [Fact]
    public void ByteOffsetConstructor_FormatsOffsetAsHexInMessage()
    {
        var ex = new PdfException("bad token", 500L);
        ex.Message.ShouldContain("0x1F4");
    }

    [Fact]
    public void IsException_InheritFromException()
    {
        var ex = new PdfException("x");
        ex.ShouldBeAssignableTo<Exception>();
    }
}
