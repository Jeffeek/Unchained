using System.IO.Compression;
using Shouldly;
using Unchained.Xlsx.Core;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Sheets;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class DocumentLifecycleTests
{
    [Fact]
    public void CreateBlank_HasSingleDefaultSheet()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank();

        document.Sheets.Count.ShouldBe(1);
        document.Sheets[0].Name.ShouldBe("Sheet1");
        document.Date1904.ShouldBeFalse();
    }

    [Fact]
    public void CreateBlank_WithName_UsesGivenSheetName()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sales");

        document.Sheets[0].Name.ShouldBe("Sales");
    }

    [Fact]
    public async Task Save_ProducesValidZipPackage()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var bytes = await XlsxFixtures.SaveBytesAsync(document);
#if NET10_0_OR_GREATER
        await using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#else
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#endif
        archive.GetEntry("[Content_Types].xml").ShouldNotBeNull();
        archive.GetEntry("xl/workbook.xml").ShouldNotBeNull();
        archive.GetEntry("xl/worksheets/sheet1.xml").ShouldNotBeNull();
        archive.GetEntry("_rels/.rels").ShouldNotBeNull();
    }

    [Fact]
    public async Task RoundTrip_PreservesSheetNamesAndCount()
    {
        using var document = XlsxFixtures.WithSheets("First", "Second", "Third");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        reloaded.Sheets.Count.ShouldBe(3);
        reloaded.Sheets[0].Name.ShouldBe("First");
        reloaded.Sheets[1].Name.ShouldBe("Second");
        reloaded.Sheets[2].Name.ShouldBe("Third");
    }

    [Fact]
    public async Task RoundTrip_PreservesSheetState()
    {
        using var document = XlsxFixtures.WithSheets("Visible", "Hidden", "Secret");
        document.Sheets[1].State = SheetState.Hidden;
        document.Sheets[2].State = SheetState.VeryHidden;

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        reloaded.Sheets[0].State.ShouldBe(SheetState.Visible);
        reloaded.Sheets[1].State.ShouldBe(SheetState.Hidden);
        reloaded.Sheets[2].State.ShouldBe(SheetState.VeryHidden);
    }

    [Fact]
    public async Task RoundTrip_PreservesProperties()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Properties.Title = "Quarterly Report";
        document.Properties.Author = "Mikhail";
        document.Properties.Company = "Jeffeek";

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        reloaded.Properties.Title.ShouldBe("Quarterly Report");
        reloaded.Properties.Author.ShouldBe("Mikhail");
        reloaded.Properties.Company.ShouldBe("Jeffeek");
    }

    [Fact]
    public async Task RoundTrip_PreservesDate1904()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Date1904 = true;

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        reloaded.Date1904.ShouldBeTrue();
    }

    [Fact]
    public void Load_NonZipBytes_ThrowsSpreadsheetException()
    {
        using var processor = new SpreadsheetProcessor();
        var garbage = "not a zip file"u8.ToArray();

        // ReSharper disable once AccessToDisposedClosure
        Should.ThrowAsync<SpreadsheetException>(async () => await processor.LoadAsync(garbage));
    }
}
