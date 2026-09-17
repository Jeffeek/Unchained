using Shouldly;
using Unchained.Xlsx.Formatting;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

public class DateTimeSerializerTests
{
    [
        Theory,
        InlineData(1, 1900, 1, 1),
        InlineData(2, 1900, 1, 2),
        InlineData(59, 1900, 2, 28),
        InlineData(61, 1900, 3, 1),
        InlineData(44927, 2023, 1, 1)
    ]
    public void ToDateTime_1900System(double serial, int year, int month, int day)
    {
        var result = DateTimeSerializer.ToDateTime(serial, false);
        result.ShouldBe(new DateTime(year, month, day));
    }

    [Fact]
    public void ToDateTime_PhantomLeapDay_ReturnsNull() =>
        DateTimeSerializer.ToDateTime(60, false).ShouldBeNull();

    [Fact]
    public void ToDateTime_1904System()
    {
        DateTimeSerializer.ToDateTime(0, true).ShouldBe(new DateTime(1904, 1, 1));
        // 1900-system serial minus 1462 gives the 1904-system serial.
        DateTimeSerializer.ToDateTime(44927 - 1462, true).ShouldBe(new DateTime(2023, 1, 1));
    }

    [
        Theory,
        InlineData(2023, 1, 1),
        InlineData(2000, 2, 29),
        InlineData(1900, 1, 1),
        InlineData(1900, 3, 1)
    ]
    public void RoundTrip_1900System(int year, int month, int day)
    {
        var date = new DateTime(year, month, day);
        var serial = DateTimeSerializer.ToSerial(date, false);
        DateTimeSerializer.ToDateTime(serial, false).ShouldBe(date);
    }

    [Fact]
    public void ToDateTime_OutOfRange_ReturnsNull()
    {
        DateTimeSerializer.ToDateTime(0.5, false).ShouldBeNull();
        DateTimeSerializer.ToDateTime(3_000_000, false).ShouldBeNull();
    }

    [Fact]
    public void ToSerial_NoonIsHalf()
    {
        var serial = DateTimeSerializer.ToSerial(
            new DateTime(
                2023,
                1,
                1,
                12,
                0,
                0
            ),
            false
        );
        (serial % 1).ShouldBe(0.5, 1e-9);
    }

    [Fact]
    public void ToDateTime_1904System_NegativeSerial_ReturnsNull() =>
        DateTimeSerializer.ToDateTime(-1, true).ShouldBeNull();

    [Fact]
    public void ToSerial_1904System_ReturnsCorrectValue()
    {
        var date = new DateTime(2023, 1, 1);
        var serial = DateTimeSerializer.ToSerial(date, true);
        serial.ShouldBeGreaterThan(0);
        DateTimeSerializer.ToDateTime(serial, true).ShouldBe(date);
    }

    [Fact]
    public void ToSerial_1900System_BeforeMarch1900_NoPhantomAdjustment()
    {
        var feb28 = new DateTime(1900, 2, 28);
        var serial = DateTimeSerializer.ToSerial(feb28, false);
        serial.ShouldBe(59);
    }

    [Fact]
    public void ToSerial_1900System_OnOrAfterMarch1900_IncludesPhantomAdjustment()
    {
        var mar1 = new DateTime(1900, 3, 1);
        var serial = DateTimeSerializer.ToSerial(mar1, false);
        serial.ShouldBe(61); // +1 for phantom leap day
    }

    [Fact]
    public void ToDateTime_1900System_Serial60Point5_ReturnsNull() =>
        // Phantom leap day with time component
        DateTimeSerializer.ToDateTime(60.5, false).ShouldBeNull();

    [Fact]
    public void ToDateTime_1900System_BelowSerial1_ReturnsNull() =>
        DateTimeSerializer.ToDateTime(0.9, false).ShouldBeNull();

    [Fact]
    public void ToDateTime_1900System_ExactlyMaxSerial_ReturnsDate()
    {
        var result = DateTimeSerializer.ToDateTime(DateTimeSerializer.MaxSerial, false);
        result.ShouldNotBeNull();
        result.Value.Year.ShouldBe(9999);
    }

    [Fact]
    public void ToDateTime_1900System_AboveMaxSerial_ReturnsNull() =>
        DateTimeSerializer.ToDateTime(DateTimeSerializer.MaxSerial + 0.1, false).ShouldBeNull();
}
