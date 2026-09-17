using Shouldly;
using Unchained.Pdf.Rendering.Abstractions;
using Xunit;

namespace Unchained.Pdf.Rendering.Tests.UnitTests;

/// <summary>
///     Tests for <see cref="PdfRendererFactory" /> — factory method for creating PDF renderers.
/// </summary>
public sealed class PdfRendererFactoryTests
{
    [Fact]
    public void CreateRenderer_ReturnsNonNullRenderer()
    {
        var renderer = PdfRendererFactory.CreateRenderer();

        renderer.ShouldNotBeNull();
    }

    [Fact]
    public void CreateRenderer_ReturnsIPdfRendererImplementation()
    {
        var renderer = PdfRendererFactory.CreateRenderer();

        renderer.ShouldBeAssignableTo<IPdfRenderer>();
    }

    [Fact]
    public void CreateRenderer_SuccessiveCallsReturnNewInstances()
    {
        var renderer1 = PdfRendererFactory.CreateRenderer();
        var renderer2 = PdfRendererFactory.CreateRenderer();

        renderer1.ShouldNotBeSameAs(renderer2);
    }
}
