using Shouldly;
using Unchained.Xlsx.Engine;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests.Worksheets;

/// <summary>Tests for <see cref="Unchained.Xlsx.Worksheets.WorksheetCollection" /> operations.</summary>
public sealed class WorksheetCollectionTests
{
    [Fact]
    public void Count_NewDocument_ReturnsSheetCount()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        doc.Sheets.Count.ShouldBe(1);
    }

    [Fact]
    public void Indexer_ByIndex_ReturnsSheet()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        var sheet = doc.Sheets[0];
        sheet.Name.ShouldBe("Sheet1");
    }

    [Fact]
    public void Indexer_ByName_ReturnsSheet()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        var sheet = doc.Sheets["Sheet1"];
        sheet.Name.ShouldBe("Sheet1");
    }

    [Fact]
    public void Indexer_ByNameCaseInsensitive_ReturnsSheet()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        var sheet = doc.Sheets["SHEET1"];
        sheet.Name.ShouldBe("Sheet1");
    }

    [Fact]
    public void Indexer_ByName_NotFound_ThrowsKeyNotFoundException()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        Should.Throw<KeyNotFoundException>(() => doc.Sheets["NonExistent"]);
    }

    [Fact]
    public void Find_ExistingSheet_ReturnsSheet()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        var sheet = doc.Sheets.Find("Sheet1");
        sheet.ShouldNotBeNull();
        sheet.Name.ShouldBe("Sheet1");
    }

    [Fact]
    public void Find_NonExistingSheet_ReturnsNull()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        doc.Sheets.Find("Sheet2").ShouldBeNull();
    }

    [Fact]
    public void FindById_ExistingSheet_ReturnsSheet()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        var sheetId = doc.Sheets[0].SheetId;
        var found = doc.Sheets.FindById(sheetId);

        found.ShouldNotBeNull();
        found.SheetId.ShouldBe(sheetId);
    }

    [Fact]
    public void FindById_NonExistingId_ReturnsNull()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        doc.Sheets.FindById(999).ShouldBeNull();
    }

    [Fact]
    public void IndexOf_ExistingSheet_ReturnsIndex()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        doc.Sheets.Add("Sheet2");

        doc.Sheets.IndexOf(doc.Sheets[1]).ShouldBe(1);
    }

    [Fact]
    public void Add_NewSheet_AddsToEnd()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        var newSheet = doc.Sheets.Add("Sheet2");

        doc.Sheets.Count.ShouldBe(2);
        newSheet.Name.ShouldBe("Sheet2");
        doc.Sheets[1].ShouldBe(newSheet);
    }

    [Fact]
    public void Add_DuplicateName_ThrowsArgumentException()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        Should.Throw<ArgumentException>(() => doc.Sheets.Add("Sheet1"));
    }

    [Fact]
    public void Insert_AtIndex_InsertsSheet()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        doc.Sheets.Add("Sheet3");

        doc.Sheets.Insert(1, "Sheet2");

        doc.Sheets.Count.ShouldBe(3);
        doc.Sheets[1].Name.ShouldBe("Sheet2");
        doc.Sheets[2].Name.ShouldBe("Sheet3");
    }

    [Fact]
    public void Insert_DuplicateName_ThrowsArgumentException()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        Should.Throw<ArgumentException>(() => doc.Sheets.Insert(0, "Sheet1"));
    }

    [Fact]
    public void Remove_ExistingSheet_RemovesSheet()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        doc.Sheets.Add("Sheet2");

        doc.Sheets.Remove(doc.Sheets[0]);

        doc.Sheets.Count.ShouldBe(1);
        doc.Sheets[0].Name.ShouldBe("Sheet2");
    }

    [Fact]
    public void Remove_LastSheet_ThrowsInvalidOperationException()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        Should.Throw<InvalidOperationException>(() => doc.Sheets.Remove(doc.Sheets[0]));
    }

    [Fact]
    public void RemoveAt_RemovesSheetAtIndex()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        doc.Sheets.Add("Sheet2");

        doc.Sheets.RemoveAt(0);

        doc.Sheets.Count.ShouldBe(1);
        doc.Sheets[0].Name.ShouldBe("Sheet2");
    }

    [Fact]
    public void MoveTo_MovesSheetToNewIndex()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        doc.Sheets.Add("Sheet2");
        doc.Sheets.Add("Sheet3");

        doc.Sheets.MoveTo(doc.Sheets[0], 2);

        doc.Sheets[0].Name.ShouldBe("Sheet2");
        doc.Sheets[1].Name.ShouldBe("Sheet3");
        doc.Sheets[2].Name.ShouldBe("Sheet1");
    }

    [Fact]
    public void MoveTo_SheetNotInCollection_ThrowsArgumentException()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc1 = processor.CreateBlank("Sheet1");
        using var doc2 = processor.CreateBlank("Sheet2");

        Should.Throw<ArgumentException>(() => doc1.Sheets.MoveTo(doc2.Sheets[0], 0));
    }

    [Fact]
    public void GetEnumerator_EnumeratesSheets()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        doc.Sheets.Add("Sheet2");

        var names = doc.Sheets.Select(static s => s.Name).ToList();

        names.Count.ShouldBe(2);
        names[0].ShouldBe("Sheet1");
        names[1].ShouldBe("Sheet2");
    }

    [Fact]
    public void GetEnumerator_NonGeneric_EnumeratesSheets()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        doc.Sheets.Count.ShouldBe(1);
    }

    [Fact]
    public void NextSheetId_EmptyCollection_Returns1()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");

        // First sheet should have ID 1
        doc.Sheets[0].SheetId.ShouldBe(1);
    }

    [Fact]
    public void NextSheetId_WithExistingSheets_ReturnsMaxPlusOne()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet2 = doc.Sheets.Add("Sheet2");

        // Second sheet should have ID 2
        sheet2.SheetId.ShouldBe(2);
    }
}
