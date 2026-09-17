using System.Text;
using Unchained.Pdf.Engine;
using Unchained.Studio.Services;

namespace Unchained.Studio.Tests.Services;

/// <summary>
///     Tests for <see cref="PdfSessionState" /> — create, tree build, refresh (serialize + reload),
///     dirty tracking, and disposal for the PDF document session.
/// </summary>
public sealed class PdfSessionStateTests
{
    [Fact]
    public async Task CreateAsync_BuildsTreeAndStartsClean()
    {
        using var processor = new DocumentProcessor();

        var state = await PdfSessionState.CreateAsync(processor, MinimalPdf(pageCount: 2), "doc.pdf", TestContext.Current.CancellationToken);

        state.FileName.ShouldBe("doc.pdf");
        state.IsDirty.ShouldBeFalse();
        state.CurrentPage.ShouldBe(1);
        state.PlayboardState.ShouldNotBeNull();
        state.Tree.Label.ShouldBe("doc.pdf");
        state.Document.PageCount.ShouldBe(2);

        await state.DisposeAsync();
    }

    [Fact]
    public async Task MarkDirty_SetsDirtyAndRaisesRefreshed()
    {
        using var processor = new DocumentProcessor();
        var state = await PdfSessionState.CreateAsync(processor, MinimalPdf(), "doc.pdf", TestContext.Current.CancellationToken);
        var raised = false;
        state.Refreshed += () => raised = true;

        state.MarkDirty();

        state.IsDirty.ShouldBeTrue();
        raised.ShouldBeTrue();

        await state.DisposeAsync();
    }

    [Fact]
    public async Task RefreshAsync_SerializesReloadsAndClearsDirty()
    {
        using var processor = new DocumentProcessor();
        var state = await PdfSessionState.CreateAsync(processor, MinimalPdf(pageCount: 2), "doc.pdf", TestContext.Current.CancellationToken);
        state.MarkDirty();

        await state.RefreshAsync(TestContext.Current.CancellationToken);

        state.IsDirty.ShouldBeFalse();
        state.CurrentBytes.ShouldNotBeEmpty();
        state.FileSizeBytes.ShouldBe(state.CurrentBytes.LongLength);
        state.Document.PageCount.ShouldBe(2);

        await state.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        using var processor = new DocumentProcessor();
        var state = await PdfSessionState.CreateAsync(processor, MinimalPdf(), "doc.pdf", TestContext.Current.CancellationToken);

        await state.DisposeAsync();

        await Should.NotThrowAsync(async () => await state.DisposeAsync());
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
