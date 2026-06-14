using Shouldly;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Shapes;

public sealed class OleShapeTests
{
    [Fact]
    public void Defaults()
    {
        var ole = new OleShape();
        ole.EmbeddedData.Length.ShouldBe(0);
        ole.ProgId.ShouldBe(string.Empty);
        ole.LinkedFilePath.ShouldBeNull();
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var ole = new OleShape
        {
            EmbeddedData = new byte[] { 1, 2, 3 },
            ProgId = "Excel.Sheet.12",
            LinkedFilePath = @"C:\book.xlsx"
        };
        ole.EmbeddedData.Length.ShouldBe(3);
        ole.ProgId.ShouldBe("Excel.Sheet.12");
        ole.LinkedFilePath.ShouldBe(@"C:\book.xlsx");
    }
}
