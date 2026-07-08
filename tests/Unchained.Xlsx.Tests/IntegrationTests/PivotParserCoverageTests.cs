using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Drives PivotParser defensive branches by injecting malformed pivot parts and reloading.</summary>
public class PivotParserCoverageTests
{
    private const string PivotTablePath = "xl/pivotTables/pivotTable1.xml";
    private const string CacheDefPath = "xl/pivotCache/pivotCacheDefinition1.xml";
    private const string CacheDefRelsPath = "xl/pivotCache/_rels/pivotCacheDefinition1.xml.rels";

    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static async Task<byte[]> SavedPivotBytes()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Region");
        sheet.SetValue(1, 2, "Amount");
        sheet.SetValue(2, 1, "East");
        sheet.SetValue(2, 2, 100.0);
        var pivot = sheet.AddPivotTable(CellRange.FromA1("A1:B2"), CellReference.FromA1("E1"), "P");
        pivot.AddRowField("Region");
        pivot.AddDataField("Amount");
        return await XlsxFixtures.SaveBytesAsync(document);
    }

    private static async Task<SpreadsheetDocument> ReloadWithReplacements(
        byte[] bytes,
        params (string Path, string? Content)[] replacements
    )
    {
        using var ms = new MemoryStream();
        await ms.WriteAsync(bytes);
#if NET10_0_OR_GREATER
        await using (var archive = new ZipArchive(ms, ZipArchiveMode.Update, true))
        {
            foreach (var (path, content) in replacements)
            {
                archive.GetEntry(path)?.Delete();
                if (content == null)
                    continue;

                var entry = archive.CreateEntry(path);
                await using var writer = new StreamWriter(await entry.OpenAsync(), new UTF8Encoding(false));
                await writer.WriteAsync(content);
            }
        }
#else
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Update, true))
        {
            foreach (var (path, content) in replacements)
            {
                archive.GetEntry(path)?.Delete();
                if (content == null)
                    continue;

                var entry = archive.CreateEntry(path);
                await using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                await writer.WriteAsync(content);
            }
        }
#endif

        var processor = new SpreadsheetProcessor();
        return await processor.LoadAsync(ms.ToArray());
    }

    [Fact]
    public async Task Pivot_NoNameNoLocation_UsesDefaults()
    {
        var bytes = await SavedPivotBytes();
        const string minimal = $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><pivotTableDefinition xmlns="{Ns}" xmlns:r="{RNs}" cacheId="1"/>""";

        using var document = await ReloadWithReplacements(bytes, (PivotTablePath, minimal));
        var pivot = document.Sheets[0].PivotTables[0];
        pivot.Name.ShouldBe("PivotTable");                  // default name
        pivot.TargetCell.ShouldBe(new CellReference(1, 1)); // default location
    }

    [Fact]
    public async Task Pivot_CacheDefWithoutWorksheetSource_DefaultsRange()
    {
        var bytes = await SavedPivotBytes();
        // cacheDef with a cacheSource that has no worksheetSource child, and no cacheFields.
        const string cacheDef =
            $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><pivotCacheDefinition xmlns="{Ns}" xmlns:r="{RNs}" recordCount="0"><cacheSource type="worksheet"/></pivotCacheDefinition>""";

        using var document = await ReloadWithReplacements(bytes, (CacheDefPath, cacheDef));
        var pivot = document.Sheets[0].PivotTables[0];
        // Default 1x1 range; no fields parsed.
        pivot.SourceSheetName.ShouldBe(string.Empty);
        pivot.Fields.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Pivot_CacheDefWithoutRecordsRel_StillParses()
    {
        var bytes = await SavedPivotBytes();
        // Remove the cache-definition rels entirely → no records relationship.
        const string emptyRels =
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"/>""";

        using var document = await ReloadWithReplacements(bytes, (CacheDefRelsPath, emptyRels));
        var pivot = document.Sheets[0].PivotTables[0];
        pivot.Name.ShouldBe("P");
        pivot.CacheRecordsData.ShouldBeNull(); // no records part resolved
    }

    [Fact]
    public async Task Pivot_RangeLocationRef_ParsesTopLeft()
    {
        var bytes = await SavedPivotBytes();
        // location ref is a range "E1:H10"; parser should take the top-left (E1).
        const string def =
            $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><pivotTableDefinition xmlns="{Ns}" xmlns:r="{RNs}" name="Ranged" cacheId="1"><location ref="E1:H10"/></pivotTableDefinition>""";

        using var document = await ReloadWithReplacements(bytes, (PivotTablePath, def));
        var pivot = document.Sheets[0].PivotTables[0];
        pivot.Name.ShouldBe("Ranged");
        pivot.TargetCell.ShouldBe(CellReference.FromA1("E1"));
    }

    [Fact]
    public async Task Pivot_CacheFieldWithoutName_UsesGeneratedName()
    {
        var bytes = await SavedPivotBytes();
        // cacheField entries without a name attribute → parser generates Field1/Field2.
        const string cacheDef =
            $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><pivotCacheDefinition xmlns="{Ns}" xmlns:r="{RNs}" recordCount="0"><cacheSource type="worksheet"><worksheetSource ref="A1:B2" sheet="Data"/></cacheSource><cacheFields count="2"><cacheField/><cacheField/></cacheFields></pivotCacheDefinition>""";

        using var document = await ReloadWithReplacements(bytes, (CacheDefPath, cacheDef));
        var pivot = document.Sheets[0].PivotTables[0];
        pivot.Fields.Count.ShouldBe(2);
        pivot.Fields[0].Name.ShouldBe("Field1");
        pivot.Fields[1].Name.ShouldBe("Field2");
    }
}
