using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class RepairTests : PdfTestBase
{
    [Fact]
    public async Task RepairAsync_ValidPdf_LoadsNormally()
    {
        // A healthy PDF should load via the normal path.
        var bytes = PdfFixtures.MultiPage(3);
        await using var doc = await Processor.RepairAsync(bytes, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task RepairAsync_TruncatedXref_StillReadsPages()
    {
        // Truncate the xref section to simulate corruption.
        var bytes = PdfFixtures.MultiPage(2);
        // Remove the last 100 bytes (xref + trailer area).
        var truncated = bytes[..Math.Max(0, bytes.Length - 100)];
        // Repair should either succeed or throw PdfException — not crash unhandled.
        try
        {
            await using var doc = await Processor.RepairAsync(truncated, TestContext.Current.CancellationToken);
            doc.PageCount.ShouldBeGreaterThan(0);
        }
        catch (PdfException)
        {
            // Acceptable outcome — repair attempted but could not recover.
        }
    }

    [Fact]
    public async Task RepairAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var bytes = PdfFixtures.SinglePage();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Processor.RepairAsync(bytes, cts.Token));
    }
}
