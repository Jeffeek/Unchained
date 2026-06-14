using Shouldly;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Tests for the XML document schema — <see cref="Abstractions.IDocumentProcessor.SaveAsXmlAsync" />
///     and <see cref="Abstractions.IDocumentProcessor.LoadFromXmlAsync" />.
///     Covers the Unchained document XML format: Document, Page, Paragraph, Heading, Table, Line.
/// </summary>
public sealed class XmlDocumentTests : PdfTestBase
{
    // ── SaveAsXml ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsXml_SinglePage_ReturnsValidXml()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello XML world",
            ct: TestContext.Current.CancellationToken);

        var xml = await Processor.SaveAsXmlAsync(doc, TestContext.Current.CancellationToken);

        xml.ShouldNotBeNullOrEmpty();
        xml.ShouldContain("<Document");
        xml.ShouldContain("<Page");
    }

    [Fact]
    public async Task SaveAsXml_TextContent_ContainsParagraphElements()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello XML",
            ct: TestContext.Current.CancellationToken);

        var xml = await Processor.SaveAsXmlAsync(doc, TestContext.Current.CancellationToken);

        xml.ShouldContain("<Paragraph");
    }

    [Fact]
    public async Task SaveAsXml_MultiPage_ContainsMultiplePageElements()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            string.Join("\n", Enumerable.Repeat("Line.", 200)),
            ct: TestContext.Current.CancellationToken);

        var xml = await Processor.SaveAsXmlAsync(doc, TestContext.Current.CancellationToken);
        var pageCount = CountOccurrences(xml, "<Page");
        pageCount.ShouldBeGreaterThan(1);
    }

    // ── LoadFromXml ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadFromXml_MinimalDocument_ProducesLoadablePdf()
    {
        const string xml = """
                           <Document>
                             <Page width="595" height="842">
                               <Paragraph font="Helvetica" size="12" x="72" y="700">Hello from XML</Paragraph>
                             </Page>
                           </Document>
                           """;

        await using var doc = await Processor.LoadFromXmlAsync(xml, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromXml_MultiPage_CorrectPageCount()
    {
        const string xml = """
                           <Document>
                             <Page width="595" height="842">
                               <Paragraph font="Helvetica" size="12" x="72" y="700">Page 1</Paragraph>
                             </Page>
                             <Page width="595" height="842">
                               <Paragraph font="Helvetica" size="12" x="72" y="700">Page 2</Paragraph>
                             </Page>
                           </Document>
                           """;

        await using var doc = await Processor.LoadFromXmlAsync(xml, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task LoadFromXml_WithHeading_ProducesLoadablePdf()
    {
        const string xml = """
                           <Document>
                             <Page width="595" height="842">
                               <Heading level="1" font="Helvetica-Bold" size="22" x="72" y="750">Title</Heading>
                               <Paragraph font="Helvetica" size="12" x="72" y="700">Body text</Paragraph>
                             </Page>
                           </Document>
                           """;

        await using var doc = await Processor.LoadFromXmlAsync(xml, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromXml_WithTable_ProducesLoadablePdf()
    {
        const string xml = """
                           <Document>
                             <Page width="595" height="842">
                               <Table x="72" y="600">
                                 <Header>Name</Header>
                                 <Header>Value</Header>
                                 <Row><Cell>Alice</Cell><Cell>100</Cell></Row>
                                 <Row><Cell>Bob</Cell><Cell>200</Cell></Row>
                               </Table>
                             </Page>
                           </Document>
                           """;

        await using var doc = await Processor.LoadFromXmlAsync(xml, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromXml_EmptyPage_ProducesLoadablePdf()
    {
        const string xml = """
                           <Document>
                             <Page width="595" height="842" />
                           </Document>
                           """;

        await using var doc = await Processor.LoadFromXmlAsync(xml, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromXml_InvalidRoot_ThrowsInvalidOperation()
    {
        const string xml = "<NotADocument><Page/></NotADocument>";
        await Should.ThrowAsync<InvalidOperationException>(static () => Processor.LoadFromXmlAsync(xml, TestContext.Current.CancellationToken));
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadXml_RoundTrip_SamePageCount()
    {
        await using var source = await Processor.LoadFromTxtAsync(
            "Round-trip test content",
            ct: TestContext.Current.CancellationToken);

        var xml = await Processor.SaveAsXmlAsync(source, TestContext.Current.CancellationToken);
        await using var reloaded = await Processor.LoadFromXmlAsync(xml, TestContext.Current.CancellationToken);

        reloaded.PageCount.ShouldBe(source.PageCount);
    }

    [Fact]
    public async Task SaveAndLoadXml_ReloadedDocumentSaveable()
    {
        await using var source = await Processor.LoadFromMarkdownAsync(
            "# Title\n\nParagraph text.",
            ct: TestContext.Current.CancellationToken);

        var xml = await Processor.SaveAsXmlAsync(source, TestContext.Current.CancellationToken);
        await using var reloaded = await Processor.LoadFromXmlAsync(xml, TestContext.Current.CancellationToken);

        await using var final = await SaveAndReloadAsync(reloaded, TestContext.Current.CancellationToken);
        final.PageCount.ShouldBe(reloaded.PageCount);
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
