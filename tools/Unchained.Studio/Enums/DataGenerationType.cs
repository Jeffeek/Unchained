namespace Unchained.Studio.Enums;

/// <summary>The kind of data to generate.</summary>
public enum DataGenerationType
{
    Sequence,
    Decimal,
    RandomNumber,
    RandomInteger,
    Date,
    DateTime
}

/// <summary>Direction to fill generated data.</summary>
public enum FillDirection
{
    Down,
    Right
}
