using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class ProtectionTests
{
    [Fact]
    public async Task SheetProtection_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].Protection.Protect("secret");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].Protection.IsProtected.ShouldBeTrue();
        reloaded.Sheets[0].Protection.PasswordHash.ShouldNotBeNull();
        reloaded.Sheets[0].Protection.PasswordHash.ShouldNotBe("secret"); // legacy hash, not plaintext
    }

    [Fact]
    public async Task WorkbookProtection_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Protection.Protect(lockStructure: true, lockWindows: false);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Protection.LockStructure.ShouldBeTrue();
        reloaded.Protection.IsProtected.ShouldBeTrue();
    }

    [Fact]
    public void Unprotect_ClearsState()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].Protection.Protect("x");
        document.Sheets[0].Protection.Unprotect();
        document.Sheets[0].Protection.IsProtected.ShouldBeFalse();
    }

    [Fact]
    public async Task AutoFilter_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetAutoFilter(CellRange.FromA1("A1:D1"));

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].AutoFilter!.Value.ToA1().ShouldBe("A1:D1");
    }
}
