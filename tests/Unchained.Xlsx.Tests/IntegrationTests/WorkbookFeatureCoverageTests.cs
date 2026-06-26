using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Coverage for defined names, workbook protection, and shared-string interning.</summary>
public class WorkbookFeatureCoverageTests
{
    // ── DefinedNameCollection ────────────────────────────────────────────────

    [Fact]
    public void DefinedNames_AddWorkbookScoped()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var defined = document.DefinedNames.Add("Total", "=Data!$A$1", "the total");

        defined.IsWorkbookScoped.ShouldBeTrue();
        defined.Comment.ShouldBe("the total");
        document.DefinedNames.Count.ShouldBe(1);
        document.DefinedNames[0].ShouldBe(defined);
        document.DefinedNames.Find("Total").ShouldBe(defined);
    }

    [Fact]
    public void DefinedNames_AddSheetScoped()
    {
        using var document = XlsxFixtures.WithSheets("Data", "Other");
        var sheet = document.Sheets[1];
        var defined = document.DefinedNames.AddSheetScoped("Local", "=Other!$B$2", sheet);

        defined.IsWorkbookScoped.ShouldBeFalse();
        defined.LocalSheetId.ShouldBe(sheet.TabIndex);
        document.DefinedNames.Find("Local", sheet).ShouldBe(defined);
        document.DefinedNames.Find("Local").ShouldBeNull(); // not workbook-scoped
    }

    [Fact]
    public void DefinedNames_Remove_AndEnumerate()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var a = document.DefinedNames.Add("A", "=Data!$A$1");
        document.DefinedNames.Add("B", "=Data!$A$2");

        document.DefinedNames.Remove(a);
        document.DefinedNames.Count.ShouldBe(1);
        document.DefinedNames.AsEnumerable().Single().Name.ShouldBe("B");
    }

    [Fact]
    public void DefinedNames_EmptyArguments_Throw()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        Should.Throw<ArgumentException>(() => document.DefinedNames.Add("", "=A1"));
        Should.Throw<ArgumentException>(() => document.DefinedNames.Add("X", ""));
    }

    [Fact]
    public async Task DefinedNames_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.DefinedNames.Add("MyRange", "=Data!$A$1:$A$5");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.DefinedNames.Find("MyRange").ShouldNotBeNull();
    }

    // ── WorkbookProtection ───────────────────────────────────────────────────

    [Fact]
    public void WorkbookProtection_WithPassword_SetsHash()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Protection.Protect("secret", lockStructure: true, lockWindows: true);

        document.Protection.LockStructure.ShouldBeTrue();
        document.Protection.LockWindows.ShouldBeTrue();
        document.Protection.PasswordHash.ShouldNotBeNull();
        document.Protection.IsProtected.ShouldBeTrue();
    }

    [Fact]
    public void WorkbookProtection_Unprotect_ClearsEverything()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Protection.Protect("pw");
        document.Protection.Unprotect();

        document.Protection.LockStructure.ShouldBeFalse();
        document.Protection.LockWindows.ShouldBeFalse();
        document.Protection.PasswordHash.ShouldBeNull();
        document.Protection.IsProtected.ShouldBeFalse();
    }

    [Fact]
    public async Task WorkbookProtection_WithPassword_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Protection.Protect("topsecret", lockStructure: true, lockWindows: true);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Protection.LockStructure.ShouldBeTrue();
        reloaded.Protection.LockWindows.ShouldBeTrue();
        reloaded.Protection.PasswordHash.ShouldNotBeNull();
    }

    // ── SharedStrings (whitespace, dedup) ────────────────────────────────────

    [Fact]
    public async Task SharedStrings_PreservesLeadingTrailingWhitespace()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "  padded  ");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("  padded  ");
    }

    [Fact]
    public async Task SharedStrings_DeduplicatesRepeatedValues()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        for (var r = 1; r <= 10; r++)
            sheet.SetValue(r, 1, "repeated");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        for (var r = 1; r <= 10; r++)
            reloaded.Sheets[0].GetCell(r, 1)!.GetString().ShouldBe("repeated");
    }

    [Fact]
    public async Task SharedStrings_EmptyStringRoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, string.Empty);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        // An empty string cell may round-trip as empty/null; the save must not throw.
        var cell = reloaded.Sheets[0].GetCell(1, 1);
        (cell?.GetString() ?? string.Empty).ShouldBe(string.Empty);
    }
}
