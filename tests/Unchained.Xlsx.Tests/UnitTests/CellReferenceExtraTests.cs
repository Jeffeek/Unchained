using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

/// <summary>Covers the remaining <c>CellReference</c> members: factories, navigation, operators, errors.</summary>
public class CellReferenceExtraTests
{
    [Fact]
    public void FromRowColumn_BuildsReference() =>
        CellReference.FromRowColumn(3, 2).ShouldBe(new CellReference(3, 2));

    [Fact]
    public void ToString_RendersA1() =>
        new CellReference(5, 3).ToString().ShouldBe("C5");

    [Fact]
    public void WithRow_And_WithColumn()
    {
        var reference = new CellReference(5, 3);
        reference.WithRow(10).ShouldBe(new CellReference(10, 3));
        reference.WithColumn(7).ShouldBe(new CellReference(5, 7));
    }

    [Fact]
    public void ColumnLetter_Property() =>
        new CellReference(1, 28).ColumnLetter.ShouldBe("AB");

    [Fact]
    public void ColumnNumberToLetters_OutOfRange_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CellReference.ColumnNumberToLetters(0));
        Should.Throw<ArgumentOutOfRangeException>(() => CellReference.ColumnNumberToLetters(CellReference.MaxColumn + 1));
    }

    [Fact]
    public void ColumnLettersToNumber_Invalid_Throws()
    {
        Should.Throw<FormatException>(() => CellReference.ColumnLettersToNumber(""));
        Should.Throw<FormatException>(() => CellReference.ColumnLettersToNumber("A1"));
    }

    [Fact]
    public void FromA1_Invalid_Throws() =>
        Should.Throw<FormatException>(() => CellReference.FromA1("not a ref"));

    [Fact]
    public void Equals_Object_And_HashCode()
    {
        var a = new CellReference(2, 3);
        var b = new CellReference(2, 3);
        a.Equals((object)b).ShouldBeTrue();
        a.Equals("not a ref").ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void ComparisonOperators_RowMajor()
    {
        var a = new CellReference(1, 1);
        var b = new CellReference(2, 1);
        (a < b).ShouldBeTrue();
        (b > a).ShouldBeTrue();
        (a <= new CellReference(1, 1)).ShouldBeTrue();
        (b >= a).ShouldBeTrue();
        (a == new CellReference(1, 1)).ShouldBeTrue();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void CompareTo_OrdersByRowThenColumn()
    {
        new CellReference(1, 5).CompareTo(new CellReference(2, 1)).ShouldBeLessThan(0);
        new CellReference(2, 1).CompareTo(new CellReference(2, 3)).ShouldBeLessThan(0);
        new CellReference(2, 3).CompareTo(new CellReference(2, 3)).ShouldBe(0);
    }
}
