using System.Text;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Studio.Models;
using Unchained.Studio.Studio.Pdf;

namespace Unchained.Studio.Tests.Studio;

/// <summary>
///     Tests for <see cref="PdfPropertyAdapter" /> — turns a selected PDF tree node into a
///     <see cref="PropertyBag" />. Covers document-backed arms plus each model-payload arm.
/// </summary>
public sealed class PdfPropertyAdapterTests
{
    private static Task<IPdfDocument> LoadAsync(byte[] bytes)
    {
        var processor = new DocumentProcessor();
        return processor.LoadAsync(new MemoryStream(bytes), TestContext.Current.CancellationToken);
    }

    private static PropertyEntry Entry(PropertyBag bag, string key) =>
        bag.Groups.SelectMany(static g => g.Entries).First(e => e.Key == key);

    private static TreeNode Node(TreeNodeType type, object? payload, string label = "") =>
        new() { NodeType = type, Payload = payload, Label = label };

    [Fact]
    public async Task Build_DocumentNode_ReportsStructureComplianceAndId()
    {
        await using var document = await LoadAsync(MinimalPdf(pageCount: 2));

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.Document, document));

        bag.Title.ShouldBe("Document");
        bag.Groups.Select(static g => g.Header).ShouldBe(["Structure", "Compliance", "Document ID"]);
        Entry(bag, "Pages").DisplayValue.ShouldBe("2");
        Entry(bag, "Encrypted").DisplayValue.ShouldBe("False");
    }

    [Fact]
    public async Task Build_MetadataNode_ShowsInfoDictionaryFields()
    {
        await using var document = await LoadAsync(MinimalPdf());

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.Metadata, document));

        bag.Title.ShouldBe("Document Info (/Info)");
        Entry(bag, "Title").DisplayValue.ShouldBe("(absent)");
        Entry(bag, "Created").Kind.ShouldBe(PropertyValueKind.Date);
    }

    [Fact]
    public async Task Build_PageNode_ShowsDimensionsAndContentCounts()
    {
        await using var document = await LoadAsync(MinimalPdf());

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.Page, document.Pages[1]));

        bag.Title.ShouldBe("Page 1");
        Entry(bag, "Orientation").DisplayValue.ShouldBe("Portrait");
        Entry(bag, "Fonts").DisplayValue.ShouldBe("0");
    }

    [Fact]
    public async Task Build_ContentStreamNode_ShowsOperatorCount()
    {
        await using var document = await LoadAsync(MinimalPdf());

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.ContentStream, document.Pages[1]));

        bag.Title.ShouldStartWith("Content stream");
        Entry(bag, "Page").DisplayValue.ShouldBe("1");
    }

    [Fact]
    public void Build_OperatorNode_ShowsKeywordAndDescription()
    {
        var op = new ContentOperator("BT", []);

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.Operator, op));

        bag.Title.ShouldBe("Operator:  BT");
        bag.Subtitle.ShouldBe("Begin text object");
        Entry(bag, "Keyword").DisplayValue.ShouldBe("BT");
    }

    [Fact]
    public void Build_FontNode_ShowsResourceKeyAndBaseFont()
    {
        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.Font, ("F1", "Helvetica")));

        bag.Title.ShouldBe("Font  F1");
        bag.Subtitle.ShouldBe("Helvetica");
        Entry(bag, "Base font").DisplayValue.ShouldBe("Helvetica");
    }

    [Fact]
    public void Build_ImageNode_ShowsDimensionsAndDataSize()
    {
        var image = new ImageXObject(4, 2, new byte[24]);

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.Image, ("Im0", image)));

        bag.Title.ShouldBe("Image  Im0");
        Entry(bag, "Width").DisplayValue.ShouldBe("4 px");
        Entry(bag, "RGB data size").DisplayValue.ShouldContain("24");
    }

    [Fact]
    public void Build_AnnotationNode_ShowsSubtypeAndRect()
    {
        // ReSharper disable once BadListLineBreaks
        var annotation = new Annotation(AnnotationSubtype.Text, 10, 20, 30, 40, "hello");

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.Annotation, annotation));

        bag.Title.ShouldBe("Annotation: Text");
        Entry(bag, "Contents").DisplayValue.ShouldBe("hello");
        Entry(bag, "Rect").DisplayValue.ShouldContain("w=30");
    }

    [Fact]
    public void Build_BookmarkNode_ShowsTitleAndTargetPage()
    {
        var bookmark = new Bookmark("Chapter 1", 5);

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.Bookmark, bookmark));

        Entry(bag, "Title").DisplayValue.ShouldBe("Chapter 1");
        Entry(bag, "Target page").DisplayValue.ShouldBe("5");
        Entry(bag, "Children").DisplayValue.ShouldBe("0");
    }

    [Fact]
    public void Build_FormFieldNode_ShowsNameTypeAndValue()
    {
        var field = new FormField("email", "Tx", null);

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.FormField, field));

        Entry(bag, "Name").DisplayValue.ShouldBe("email");
        Entry(bag, "Type").DisplayValue.ShouldBe("Tx");
        Entry(bag, "Value").DisplayValue.ShouldBe("(empty)");
    }

    [Fact]
    public void Build_NamedDestinationNode_ShowsNameAndPage()
    {
        var dest = new NamedDestination("intro", 3);

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.NamedDestination, dest));

        bag.Title.ShouldBe("Named destination: intro");
        Entry(bag, "Target page").DisplayValue.ShouldBe("3");
    }

    [Fact]
    public void Build_XmpNode_ExposesRawText()
    {
        const string xmp = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"></x:xmpmeta>";

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.XmpMetadata, xmp));

        bag.Title.ShouldBe("XMP Metadata");
        bag.RawText.ShouldBe(xmp);
        bag.RawTextLabel.ShouldBe("Raw XMP XML");
    }

    [Fact]
    public async Task Build_EncryptionNode_ShowsAlgorithmAndPermissions()
    {
        await using var document = await LoadAsync(MinimalPdf());

        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.Encryption, document));

        bag.Title.ShouldBe("Encryption");
        Entry(bag, "Algorithm").DisplayValue.ShouldBe("(unknown)");
    }

    [Fact]
    public void Build_UnknownNode_ReturnsEmptyBagWithLabel()
    {
        var bag = PdfPropertyAdapter.Build(Node(TreeNodeType.Generic, payload: null, "nothing"));

        bag.Title.ShouldBe("nothing");
        bag.Groups.ShouldBeEmpty();
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
