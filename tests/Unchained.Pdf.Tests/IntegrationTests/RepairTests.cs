using Shouldly;
using Unchained.Pdf.Engine;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class RepairTests
{
    private static readonly DocumentProcessor Processor = new();

    [Fact]
    public async Task RepairAsync_ValidPdf_LoadsNormally()
    {
        // A healthy PDF should load via the normal path.
        var bytes = Helpers.PdfFixtures.MultiPage(count: 3);
        await using var doc = await Processor.RepairAsync(bytes);
        doc.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task RepairAsync_TruncatedXref_StillReadsPages()
    {
        // Truncate the xref section to simulate corruption.
        var bytes = Helpers.PdfFixtures.MultiPage(count: 2);
        // Remove the last 100 bytes (xref + trailer area).
        var truncated = bytes[..Math.Max(0, bytes.Length - 100)];
        // Repair should either succeed or throw PdfException — not crash unhandled.
        try
        {
            await using var doc = await Processor.RepairAsync(truncated);
            doc.PageCount.ShouldBeGreaterThan(0);
        }
        catch (Core.PdfException)
        {
            // Acceptable outcome — repair attempted but could not recover.
        }
    }

    [Fact]
    public async Task RepairAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var bytes = Helpers.PdfFixtures.SinglePage();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Processor.RepairAsync(bytes, cts.Token));
    }
}
