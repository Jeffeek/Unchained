using System.Text;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;
using Unchained.Studio.Models;
using Unchained.Studio.Studio.Pdf;

namespace Unchained.Studio.Tests.Studio;

/// <summary>
///     Tests for <see cref="PdfTreeBuilder" /> — builds the navigable tree for a loaded PDF:
///     document → Document Info, Pages (lazy per-page children), and content sections (empty ones
///     pruned).
/// </summary>
public sealed class PdfTreeBuilderTests
{
    private static Task<IPdfDocument> LoadAsync(byte[] bytes)
    {
        var processor = new DocumentProcessor();
        return processor.LoadAsync(new MemoryStream(bytes), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Build_Root_IsExpandedDocumentNodeWithFileName()
    {
        await using var document = await LoadAsync(MinimalPdf());

        var root = PdfTreeBuilder.Build(document, "doc.pdf");

        root.Label.ShouldBe("doc.pdf");
        root.NodeType.ShouldBe(TreeNodeType.Document);
        root.IsExpanded.ShouldBeTrue();
        root.Payload.ShouldBe(document);
    }

    [Fact]
    public async Task Build_PagesNode_ListsEveryPageWithDimensions()
    {
        await using var document = await LoadAsync(MinimalPdf(pageCount: 2));

        var root = PdfTreeBuilder.Build(document, "doc.pdf");

        var pagesNode = root.Children.First(static n => n.NodeType == TreeNodeType.Pages);
        pagesNode.Label.ShouldBe("Pages (2)");
        pagesNode.Children.Count.ShouldBe(2);
        pagesNode.Children[0].NodeType.ShouldBe(TreeNodeType.Page);
        pagesNode.Children[0].Label.ShouldContain("595");
        pagesNode.Children[0].Label.ShouldContain("842");
        pagesNode.Children[0].HasLazyChildren.ShouldBeTrue();
    }

    [Fact]
    public async Task Build_MetadataNode_ReportsPageCountAndComplianceFlags()
    {
        await using var document = await LoadAsync(MinimalPdf(pageCount: 3));

        var root = PdfTreeBuilder.Build(document, "doc.pdf");

        var metadata = root.Children.First(static n => n.NodeType == TreeNodeType.Metadata);
        metadata.Label.ShouldBe("Document Info");
        metadata.Children.ShouldContain(static c => c.Label == "Pages: 3");
        metadata.Children.ShouldContain(static c => c.Label == "Linearized: False");
    }

    [Fact]
    public async Task Build_EmptySections_ArePruned()
    {
        await using var document = await LoadAsync(MinimalPdf());

        var root = PdfTreeBuilder.Build(document, "doc.pdf");

        // The builder removes childless "… (none)" sections; none should survive.
        root.Children.ShouldNotContain(static n => n.Label.EndsWith(" (none)", StringComparison.Ordinal));
    }

    private static byte[] MinimalPdf(int pageCount = 1)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        Ln(sb, "%PDF-1.7");

        offsets.Add(Len(sb));
        Ln(sb, "1 0 obj");
        Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        Ln(sb, "endobj");

        offsets.Add(Len(sb));
        Ln(sb, "2 0 obj");
        var kids = string.Join(" ", Enumerable.Range(3, pageCount).Select(static n => $"{n} 0 R"));
        Ln(sb, $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");
        Ln(sb, "endobj");

        for (var i = 0; i < pageCount; i++)
        {
            offsets.Add(Len(sb));
            Ln(sb, $"{3 + i} 0 obj");
            Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
            Ln(sb, "endobj");
        }

        var size = 3 + pageCount;
        var xref = Len(sb);
        Ln(sb, "xref");
        Ln(sb, $"0 {size}");
        Ln(sb, "0000000000 65535 f ");
        foreach (var offset in offsets)
            Ln(sb, $"{offset:D10} 00000 n ");
        Ln(sb, "trailer");
        Ln(sb, $"<< /Size {size} /Root 1 0 R >>");
        Ln(sb, "startxref");
        Ln(sb, xref.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());

        static void Ln(StringBuilder builder, string line) => builder.Append(line).Append('\n');
        static int Len(StringBuilder builder) => Encoding.Latin1.GetByteCount(builder.ToString());
    }
}
