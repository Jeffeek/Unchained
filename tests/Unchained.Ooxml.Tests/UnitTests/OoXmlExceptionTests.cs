using Shouldly;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests;

public sealed class OoXmlExceptionTests
{
    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var ex = new OoXmlException("bad ooxml");
        ex.Message.ShouldBe("bad ooxml");
        ex.InnerException.ShouldBeNull();
    }

    [Fact]
    public void InnerExceptionConstructor_SetsInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new OoXmlException("outer", inner);
        ex.Message.ShouldBe("outer");
        ex.InnerException.ShouldBeSameAs(inner);
    }
}
