using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     End-to-end tests for content stream parsing
/// </summary>
public sealed class ContentStreamTests : IDisposable
{
    private readonly DocumentProcessor _processor = new();

    public void Dispose() => _processor.Dispose();

    [Fact]
    public async Task GetContentOperators_PageWithText_ReturnsOperators()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.WithTextContent()), TestContext.Current.CancellationToken);

        var ops = doc.Pages[1].GetContentOperators();

        ops.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetContentOperators_PageWithText_ContainsBtEt()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.WithTextContent()), TestContext.Current.CancellationToken);

        var names = doc.Pages[1]
            .GetContentOperators()
            .Select(static o => o.Name)
            .ToList();

        names.ShouldContain("BT");
        names.ShouldContain("ET");
    }

    [Fact]
    public async Task GetContentOperators_PageWithText_ContainsTjWithString()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(PdfFixtures.WithTextContent("Test string")),
            TestContext.Current.CancellationToken
        );

        var tjOp = doc.Pages[1].GetContentOperators().FirstOrDefault(static o => o.Name == "Tj");

        tjOp.ShouldNotBeNull();
        tjOp.Operands.Count.ShouldBe(1);
        tjOp.Operands[0].ShouldBeOfType<PdfString>();
    }

    [Fact]
    public async Task GetContentOperators_PageWithText_ContainsTfWithNameAndSize()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.WithTextContent()), TestContext.Current.CancellationToken);

        var tfOp = doc.Pages[1]
            .GetContentOperators()
            .FirstOrDefault(static o => o.Name == "Tf");

        tfOp.ShouldNotBeNull();
        tfOp.Operands.Count.ShouldBe(2);
        tfOp.Operands[0].ShouldBeOfType<PdfName>();
        ((PdfInteger)tfOp.Operands[1]).Value.ShouldBe(12);
    }

    [Fact]
    public async Task GetContentOperators_EmptyPage_ReturnsEmpty()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);

        // MinimalPdfFactory pages have no /Contents entry.
        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetContentOperators_CalledTwice_ReturnsSameCount()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.WithTextContent()), TestContext.Current.CancellationToken);

        var first = doc.Pages[1].GetContentOperators().Count;
        var second = doc.Pages[1].GetContentOperators().Count;

        first.ShouldBe(second);
    }

    [Fact]
    public async Task GetContentOperators_FormXObjectDo_InlinesFormContent()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(PdfFixtures.WithFormXObjectDo()),
            TestContext.Current.CancellationToken
        );

        var ops = doc.Pages[1].GetContentOperators();

        // The form's "re"/"f" operators must be inlined; the Do is replaced by q/cm/<form>/Q.
        ops.ShouldContain(static o => o.Name == "re");
        ops.ShouldContain(static o => o.Name == "f");
        ops.ShouldContain(static o => o.Name == "q");
        ops.ShouldContain(static o => o.Name == "Q");
        // The Do operator should have been expanded away.
        ops.ShouldNotContain(static o => o.Name == "Do");
    }

    [Fact]
    public async Task GetContentOperators_ContentStreamArray_ConcatenatesStreams()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(PdfFixtures.WithContentStreamArray()),
            TestContext.Current.CancellationToken
        );

        var ops = doc.Pages[1].GetContentOperators();

        // Both streams' fill operators must appear (two rectangles, two fills).
        ops.Count(static o => o.Name == "re").ShouldBe(2);
        ops.Count(static o => o.Name == "f").ShouldBe(2);
    }
}
