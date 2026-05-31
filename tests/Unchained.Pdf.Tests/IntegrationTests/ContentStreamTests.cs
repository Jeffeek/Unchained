using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// End-to-end tests for content stream parsing: load a PDF with a known
/// content stream → call <see cref="IPdfPage.GetContentOperators"/> → assert
/// that the operators and operands are correctly extracted.
/// </summary>
public sealed class ContentStreamTests : IDisposable
{
    private readonly DocumentProcessor _processor = new();

    public void Dispose() => _processor.Dispose();

    [Fact]
    public async Task GetContentOperators_PageWithText_ReturnsOperators()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(PdfFixtures.WithTextContent()));

        var ops = doc.Pages[1].GetContentOperators();

        ops.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetContentOperators_PageWithText_ContainsBtEt()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(PdfFixtures.WithTextContent()));

        var names = doc.Pages[1].GetContentOperators()
            .Select(static o => o.Name)
            .ToList();

        names.ShouldContain("BT");
        names.ShouldContain("ET");
    }

    [Fact]
    public async Task GetContentOperators_PageWithText_ContainsTjWithString()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(PdfFixtures.WithTextContent("Test string")));

        var tjOp = doc.Pages[1].GetContentOperators()
            .FirstOrDefault(static o => o.Name == "Tj");

        tjOp.ShouldNotBeNull();
        tjOp!.Operands.Count.ShouldBe(1);
        tjOp.Operands[0].ShouldBeOfType<PdfString>();
    }

    [Fact]
    public async Task GetContentOperators_PageWithText_ContainsTfWithNameAndSize()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(PdfFixtures.WithTextContent()));

        var tfOp = doc.Pages[1].GetContentOperators()
            .FirstOrDefault(static o => o.Name == "Tf");

        tfOp.ShouldNotBeNull();
        tfOp!.Operands.Count.ShouldBe(2);
        tfOp.Operands[0].ShouldBeOfType<PdfName>();
        ((PdfInteger)tfOp.Operands[1]).Value.ShouldBe(12);
    }

    [Fact]
    public async Task GetContentOperators_EmptyPage_ReturnsEmpty()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(PdfFixtures.SinglePage()));

        // MinimalPdfFactory pages have no /Contents entry.
        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetContentOperators_CalledTwice_ReturnsSameCount()
    {
        await using var doc = await _processor.LoadAsync(
            new MemoryStream(PdfFixtures.WithTextContent()));

        var first = doc.Pages[1].GetContentOperators().Count;
        var second = doc.Pages[1].GetContentOperators().Count;

        first.ShouldBe(second);
    }
}
