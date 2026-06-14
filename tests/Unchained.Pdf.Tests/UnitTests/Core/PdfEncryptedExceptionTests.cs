using Shouldly;
using Unchained.Pdf.Core;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Core;

public sealed class PdfEncryptedExceptionTests
{
    [Fact]
    public async Task MessageConstructor_SetsMessage()
    {
        await Task.CompletedTask;
        var ex = new PdfEncryptedException("document is locked");
        ex.Message.ShouldBe("document is locked");
        ex.InnerException.ShouldBeNull();
    }

    [Fact]
    public async Task InnerExceptionConstructor_SetsMessageAndInner()
    {
        await Task.CompletedTask;
        var inner = new InvalidOperationException("crypto failure");
        var ex = new PdfEncryptedException("decrypt failed", inner);
        ex.Message.ShouldBe("decrypt failed");
        ex.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public async Task IsAssignableToException()
    {
        await Task.CompletedTask;
        var ex = new PdfEncryptedException("x");
        ex.ShouldBeAssignableTo<Exception>();
    }
}
