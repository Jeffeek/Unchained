using System;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Core;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Core;

public sealed class PptxExceptionTests
{
    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var ex = new PptxException("bad pptx");
        ex.Message.ShouldBe("bad pptx");
    }

    [Fact]
    public void InnerExceptionConstructor_SetsInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new PptxException("outer", inner);
        ex.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public void IsOoXmlException() =>
        new PptxException("x").ShouldBeAssignableTo<OoXmlException>();
}

public sealed class PptxEncryptedExceptionTests
{
    [Fact]
    public void DefaultConstructor_MentionsPassword()
    {
        var ex = new PptxEncryptedException();
        ex.Message.ShouldContain("password");
    }

    [Fact]
    public void MessageConstructor_SetsMessage() =>
        new PptxEncryptedException("nope").Message.ShouldBe("nope");

    [Fact]
    public void InnerExceptionConstructor_SetsInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new PptxEncryptedException("outer", inner);
        ex.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public void IsPptxException() =>
        new PptxEncryptedException().ShouldBeAssignableTo<PptxException>();
}
