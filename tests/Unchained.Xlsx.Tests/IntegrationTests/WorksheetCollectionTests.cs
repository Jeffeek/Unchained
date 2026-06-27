using Shouldly;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class WorksheetCollectionTests
{
    [Fact]
    public void Add_AppendsSheet()
    {
        using var document = XlsxFixtures.WithSheets("Sheet1");
        var added = document.Sheets.Add("Sheet2");

        document.Sheets.Count.ShouldBe(2);
        added.Name.ShouldBe("Sheet2");
        added.TabIndex.ShouldBe(1);
    }

    [Fact]
    public void Add_DuplicateName_Throws() =>
        Should.Throw<ArgumentException>(static () =>
            {
                using var document = XlsxFixtures.WithSheets("Data");
                document.Sheets.Add("Data");
            }
        );

    [
        Theory,
        InlineData("Has/Slash"),
        InlineData("Question?"),
        InlineData("Star*"),
        InlineData("")
    ]
    public void Add_InvalidName_Throws(string name) =>
        Should.Throw<ArgumentException>(() =>
            {
                using var document = XlsxFixtures.WithSheets("Data");
                document.Sheets.Add(name);
            }
        );

    [Fact]
    public void Add_NameTooLong_Throws() =>
        Should.Throw<ArgumentException>(static () =>
            {
                using var document = XlsxFixtures.WithSheets("Data");
                document.Sheets.Add(new string('x', 32));
            }
        );

    [Fact]
    public void Remove_LastSheet_Throws() =>
        Should.Throw<InvalidOperationException>(static () =>
            {
                using var document = XlsxFixtures.WithSheets("Only");
                document.Sheets.Remove(document.Sheets[0]);
            }
        );

    [Fact]
    public async Task Remove_ThenRoundTrip_DropsSheet()
    {
        using var document = XlsxFixtures.WithSheets("A", "B", "C");
        document.Sheets.Remove(document.Sheets[1]);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);

        reloaded.Sheets.Count.ShouldBe(2);
        reloaded.Sheets.Find("B").ShouldBeNull();
    }

    [Fact]
    public void MoveTo_ReordersTabs()
    {
        using var document = XlsxFixtures.WithSheets("A", "B", "C");
        var c = document.Sheets[2];
        document.Sheets.MoveTo(c, 0);

        document.Sheets[0].Name.ShouldBe("C");
        document.Sheets[1].Name.ShouldBe("A");
    }

    [Fact]
    public void SheetId_IsStableAndUnique()
    {
        using var document = XlsxFixtures.WithSheets("A", "B", "C");
        var ids = document.Sheets.Select(static s => s.SheetId).ToList();

        ids.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void Indexer_ByName_ReturnsSheet()
    {
        using var document = XlsxFixtures.WithSheets("Alpha", "Beta");
        document.Sheets["Beta"].Name.ShouldBe("Beta");
    }

    [Fact]
    public void FindById_ReturnsMatchingSheet()
    {
        using var document = XlsxFixtures.WithSheets("Alpha", "Beta");
        var id = document.Sheets[1].SheetId;
        document.Sheets.FindById(id)!.Name.ShouldBe("Beta");
    }
}
