using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Branch coverage for the <c>Cell</c> value accessors and formatting.</summary>
public class CellCoverageTests
{
    [Fact]
    public void TypedGetters_ReturnNullForWrongType()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue(42.0);

        cell.GetDouble().ShouldBe(42.0);
        cell.GetString().ShouldBeNull();
        cell.GetBoolean().ShouldBeNull();
        cell.GetError().ShouldBeNull();
    }

    [Fact]
    public void RowColumn_MirrorReference()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][3, 5];
        cell.Row.ShouldBe(3);
        cell.Column.ShouldBe(5);
        cell.Reference.ShouldBe(new CellReference(3, 5));
    }

    [Fact]
    public void SetValue_String_NullThrows()
    {
        using var document = XlsxFixtures.WithSheets("S");
        Should.Throw<ArgumentNullException>(() => document.Sheets[0][1, 1].SetValue((string)null!));
    }

    [Fact]
    public void Clear_ResetsToEmpty()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue(5.0);
        cell.Clear();

        cell.CellType.ShouldBe(CellType.Empty);
        cell.GetDouble().ShouldBeNull();
    }

    [Fact]
    public void FormulaText_NullWhenNotFormula()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue(1.0);
        cell.FormulaText.ShouldBeNull();
    }

    [Fact]
    public void FormulaText_SetAndClear()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];
        cell.FormulaText = "=A2+1";
        cell.CellType.ShouldBe(CellType.Formula);
        cell.FormulaText.ShouldBe("=A2+1");

        cell.FormulaText = null;
        cell.CellType.ShouldBe(CellType.Empty);
        cell.FormulaText.ShouldBeNull();
    }

    [Fact]
    public void GetDateTime_NonNumeric_ReturnsNull()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue("text");
        cell.GetDateTime().ShouldBeNull();
    }

    [Fact]
    public void GetDateTimeOffset_FromSerial()
    {
        using var document = XlsxFixtures.WithSheets("S");
        document.Sheets[0].SetValue(1, 1, new DateTime(2023, 6, 15));
        var cell = document.Sheets[0][1, 1];

        var offset = cell.GetDateTimeOffset();
        offset.ShouldNotBeNull();
        offset.Value.Date.ShouldBe(new DateTime(2023, 6, 15));
    }

    [
        Theory,
        InlineData(CellType.Empty),
        InlineData(CellType.String),
        InlineData(CellType.Boolean),
        InlineData(CellType.Error),
        InlineData(CellType.Number)
    ]
    public void GetFormattedString_HandlesEachType(CellType type)
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];
        switch (type)
        {
            case CellType.String: cell.SetValue("hi"); break;
            case CellType.Boolean: cell.SetValue(true); break;
            case CellType.Error: cell.SetValue(CellError.Value); break;
            case CellType.Number: cell.SetValue(3.5); break;
            case CellType.Empty:
            case CellType.Formula:
            default: break;
        }

        cell.GetFormattedString().ShouldNotBeNull();
    }

    [Fact]
    public void GetFormattedString_Boolean()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue(true);
        cell.GetFormattedString().ShouldBe("TRUE");
        cell.SetValue(false);
        cell.GetFormattedString().ShouldBe("FALSE");
    }

    [Fact]
    public void GetFormattedString_Error()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];
        cell.SetValue(CellError.Reference);
        cell.GetFormattedString().ShouldBe("#REF!");
    }

    [Fact]
    public void ValueProperties_ReflectState()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];

        cell.SetValue(7.0);
        cell.NumberValue.ShouldBe(7.0);
        cell.StringValue.ShouldBeNull();

        cell.SetValue("abc");
        cell.StringValue.ShouldBe("abc");
        cell.NumberValue.ShouldBeNull();

        cell.SetValue(true);
        cell.BooleanValue.ShouldBe(true);

        cell.SetValue(CellError.Number);
        cell.ErrorValue.ShouldBe(CellError.Number);
    }

    [Fact]
    public async Task CachedFormulaResult_RoundTripsText()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];
        cell.FormulaText = "=\"a\"&\"b\"";
        cell.SetFormulaCachedText("ab");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var loaded = reloaded.Sheets[0].GetCell(1, 1);
        loaded!.CellType.ShouldBe(CellType.Formula);
        loaded.GetString().ShouldBe("ab");
    }

    [Fact]
    public void CachedFormulaResult_Error()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];
        cell.FormulaText = "=1/0";
        cell.SetFormulaCachedError(CellError.DivisionByZero);
        cell.ErrorValue.ShouldBe(CellError.DivisionByZero);
    }

    [Fact]
    public void CachedFormulaResult_Number()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var cell = document.Sheets[0][1, 1];
        cell.FormulaText = "=1+1";
        cell.SetFormulaCachedNumber(2);
        cell.NumberValue.ShouldBe(2);
    }
}

/// <summary>Branch coverage for <c>SpreadsheetProcessor</c> construction and error paths.</summary>
public class SpreadsheetProcessorCoverageTests
{
    [Fact]
    public void Constructor_InvalidConcurrency_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SpreadsheetProcessor(0));

    [Fact]
    public void Constructor_CustomConcurrency_Works()
    {
        using var processor = new SpreadsheetProcessor(2);
        using var document = processor.CreateBlank();
        document.Sheets.Count.ShouldBe(1);
    }

    [Fact]
    public void CreateBlank_DefaultName()
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank();
        document.Sheets[0].Name.ShouldBe("Sheet1");
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var processor = new SpreadsheetProcessor();
        processor.Dispose();
        Should.NotThrow(() => processor.Dispose());
    }

    [Fact]
    public async Task LoadAsync_NullStream_Throws()
    {
        using var processor = new SpreadsheetProcessor();
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await processor.LoadAsync((Stream)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_EmptyPath_Throws()
    {
        using var processor = new SpreadsheetProcessor();
        await Should.ThrowAsync<ArgumentException>(
            async () => await processor.LoadAsync("  ", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_NullDocument_Throws()
    {
        using var processor = new SpreadsheetProcessor();
        using var ms = new MemoryStream();
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await processor.SaveAsync(null!, ms, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_CorruptBytes_ThrowsSpreadsheetException()
    {
        using var processor = new SpreadsheetProcessor();
        var garbage = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        await Should.ThrowAsync<Core.SpreadsheetException>(
            async () => await processor.LoadAsync(garbage, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RoundTrip_ThroughByteArrayOverload()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "persisted");
        var bytes = await XlsxFixtures.SaveBytesAsync(document);

        using var processor = new SpreadsheetProcessor();
        using var reloaded = await processor.LoadAsync(
            (ReadOnlyMemory<byte>)bytes, cancellationToken: TestContext.Current.CancellationToken);
        reloaded.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("persisted");
    }
}
