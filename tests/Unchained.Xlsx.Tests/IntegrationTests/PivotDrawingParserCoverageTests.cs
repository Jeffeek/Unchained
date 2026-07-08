using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Drawings;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Drives PivotParser and DrawingParser branches via reload of saved/injected parts.</summary>
public class PivotDrawingParserCoverageTests
{
    private static SpreadsheetDocument WithPivot()
    {
        var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Region");
        sheet.SetValue(1, 2, "Amount");
        sheet.SetValue(2, 1, "East");
        sheet.SetValue(2, 2, 100.0);
        sheet.SetValue(3, 1, "West");
        sheet.SetValue(3, 2, 200.0);
        var pivot = sheet.AddPivotTable(CellRange.FromA1("A1:B3"), CellReference.FromA1("E1"), "Sales");
        pivot.AddRowField("Region");
        pivot.AddDataField("Amount");
        return document;
    }

    [Fact]
    public async Task Pivot_Reload_ParsesIdentityAndFields()
    {
        using var document = WithPivot();
        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        var pivots = reloaded.Sheets[0].PivotTables;
        pivots.Count.ShouldBe(1);
        var pivot = pivots[0];
        pivot.Name.ShouldBe("Sales");
        pivot.Fields.Count.ShouldBe(2);
        pivot.Fields.Select(static f => f.Name).ShouldBe(["Region", "Amount"]);
        pivot.SourceRange.ToA1().ShouldBe("A1:B3");
    }

    [Fact]
    public async Task Pivot_Reload_PreservesSourceSheet()
    {
        using var document = WithPivot();
        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].PivotTables[0].SourceSheetName.ShouldBe("Data");
    }

    [Fact]
    public async Task Pivot_Reload_ThenResave_UsesPreservedBytes()
    {
        // A reloaded-but-unrefreshed pivot writes its preserved raw bytes back verbatim.
        using var document = WithPivot();
        using var once = await XlsxFixtures.RoundTripAsync(document);
        using var twice = await XlsxFixtures.RoundTripAsync(once);
        twice.Sheets[0].PivotTables[0].Name.ShouldBe("Sales");
    }

    [Fact]
    public async Task Drawing_AbsoluteAnchor_Reloads()
    {
        // Build a real workbook with one picture, then rewrite its drawing XML to use absoluteAnchor.
        var tiny = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/pLvAAAAAElFTkSuQmCC");
        using var seed = XlsxFixtures.WithSheets("Data");
        seed.Sheets[0].AddImage(tiny, "image/png", CellReference.FromA1("B2"));
        var bytes = await XlsxFixtures.SaveBytesAsync(seed);

        using var ms = new MemoryStream();
        await ms.WriteAsync(bytes);
        string? drawingPath;
#if NET10_0_OR_GREATER
        await using (var probe = new ZipArchive(ms, ZipArchiveMode.Read, true))
            drawingPath = probe.Entries.FirstOrDefault(static e => e.FullName.Contains("drawings/drawing"))?.FullName;
#else
        using (var probe = new ZipArchive(ms, ZipArchiveMode.Read, true))
            drawingPath = probe.Entries.FirstOrDefault(static e => e.FullName.Contains("drawings/drawing"))?.FullName;
#endif

        drawingPath.ShouldNotBeNull();

        // Read the embed rel id from the existing drawing so the rewritten one stays valid.
        string embedId;
#if NET10_0_OR_GREATER
        await using (var read = new ZipArchive(ms, ZipArchiveMode.Read, true))
        {
            using var r = new StreamReader(await read.GetEntry(drawingPath)!.OpenAsync());
            var xml = await r.ReadToEndAsync(TestContext.Current.CancellationToken);
            var idx = xml.IndexOf("r:embed=\"", StringComparison.Ordinal) + 9;
            embedId = xml[idx..xml.IndexOf('"', idx)];
        }
#else
        using (var read = new ZipArchive(ms, ZipArchiveMode.Read, true))
        {
            using var r = new StreamReader(read.GetEntry(drawingPath)!.Open());
            var xml = await r.ReadToEndAsync(TestContext.Current.CancellationToken);
            var idx = xml.IndexOf("r:embed=\"", StringComparison.Ordinal) + 9;
            embedId = xml[idx..xml.IndexOf('"', idx)];
        }
#endif

        var absolute =
            $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><xdr:absoluteAnchor><xdr:pos x="100" y="200"/><xdr:ext cx="900000" cy="900000"/><xdr:pic><xdr:nvPicPr><xdr:cNvPr id="1" name="Pic"/><xdr:cNvPicPr/></xdr:nvPicPr><xdr:blipFill><a:blip r:embed="{embedId}"/></xdr:blipFill><xdr:spPr/></xdr:pic><xdr:clientData/></xdr:absoluteAnchor></xdr:wsDr>""";

#if NET10_0_OR_GREATER
        await using (var update = new ZipArchive(ms, ZipArchiveMode.Update, true))
        {
            update.GetEntry(drawingPath)!.Delete();
            var entry = update.CreateEntry(drawingPath);
            await using var w = new StreamWriter(await entry.OpenAsync(), new UTF8Encoding(false));
            await w.WriteAsync(absolute);
        }
#else
        using (var update = new ZipArchive(ms, ZipArchiveMode.Update, true))
        {
            update.GetEntry(drawingPath)!.Delete();
            var entry = update.CreateEntry(drawingPath);
            await using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            await w.WriteAsync(absolute);
        }
#endif

        using var processor = new SpreadsheetProcessor();
        using var reloaded = await processor.LoadAsync(ms.ToArray(), cancellationToken: TestContext.Current.CancellationToken);
        var pictures = reloaded.Sheets[0].Drawings.Pictures.ToList();
        pictures.Count.ShouldBe(1);
        pictures[0].Anchor.AnchorType.ShouldBe(DrawingAnchorType.Absolute);
    }

    [Fact]
    public async Task Drawing_PictureWithMissingEmbed_IsSkipped()
    {
        var tiny = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/pLvAAAAAElFTkSuQmCC");
        using var seed = XlsxFixtures.WithSheets("Data");
        seed.Sheets[0].AddImage(tiny, "image/png", CellReference.FromA1("A1"));
        var bytes = await XlsxFixtures.SaveBytesAsync(seed);

        using var ms = new MemoryStream();
        await ms.WriteAsync(bytes);
        string drawingPath;
#if NET10_0_OR_GREATER
        await using (var probe = new ZipArchive(ms, ZipArchiveMode.Read, true))
            drawingPath = probe.Entries.First(static e => e.FullName.Contains("drawings/drawing")).FullName;
#else
        using (var probe = new ZipArchive(ms, ZipArchiveMode.Read, true))
            drawingPath = probe.Entries.First(static e => e.FullName.Contains("drawings/drawing")).FullName;
#endif

        // A pic element with no <a:blip r:embed> → ReadPicture returns null and is skipped.
        const string noEmbed =
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><xdr:oneCellAnchor><xdr:from><xdr:col>0</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>0</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from><xdr:ext cx="900000" cy="900000"/><xdr:pic><xdr:nvPicPr><xdr:cNvPr id="1" name="P"/><xdr:cNvPicPr/></xdr:nvPicPr><xdr:blipFill/><xdr:spPr/></xdr:pic><xdr:clientData/></xdr:oneCellAnchor></xdr:wsDr>""";

#if NET10_0_OR_GREATER
        await using (var update = new ZipArchive(ms, ZipArchiveMode.Update, true))
        {
            update.GetEntry(drawingPath)!.Delete();
            var entry = update.CreateEntry(drawingPath);
            await using var w = new StreamWriter(await entry.OpenAsync(), new UTF8Encoding(false));
            await w.WriteAsync(noEmbed);
        }
#else
        using (var update = new ZipArchive(ms, ZipArchiveMode.Update, true))
        {
            update.GetEntry(drawingPath)!.Delete();
            var entry = update.CreateEntry(drawingPath);
            await using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            await w.WriteAsync(noEmbed);
        }
#endif


        using var processor = new SpreadsheetProcessor();
        using var reloaded = await processor.LoadAsync(ms.ToArray(), cancellationToken: TestContext.Current.CancellationToken);
        reloaded.Sheets[0].Drawings.Pictures.Count().ShouldBe(0);
    }
}
