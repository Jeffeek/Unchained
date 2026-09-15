using Shouldly;
using Unchained.Ooxml.Security;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Security;

/// <summary>Tests for the two <see cref="OoXmlEncryptedException" /> constructors.</summary>
public sealed class OoXmlEncryptedExceptionTests
{
    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var ex = new OoXmlEncryptedException("bad password");

        ex.Message.ShouldBe("bad password");
        ex.InnerException.ShouldBeNull();
    }

    [Fact]
    public void MessageAndInnerConstructor_SetsBoth()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new OoXmlEncryptedException("decrypt failed", inner);

        ex.Message.ShouldBe("decrypt failed");
        ex.InnerException.ShouldBeSameAs(inner);
    }
}
