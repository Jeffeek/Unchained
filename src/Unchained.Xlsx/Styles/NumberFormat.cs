namespace Unchained.Xlsx.Styles;

/// <summary>
///     A number format: a format id and its format code. Built-in ids (0–163) are defined by
///     ECMA-376 and do not appear in the file; custom formats use id ≥ 164.
/// </summary>
public sealed class NumberFormat : IEquatable<NumberFormat>
{
    /// <summary>The first id assigned to a custom (non-built-in) number format.</summary>
    public const int FirstCustomId = 164;

    /// <summary>Initialises a number format with the given id and code.</summary>
    public NumberFormat(int formatId, string formatCode)
    {
        FormatId = formatId;
        FormatCode = formatCode;
    }

    /// <summary>The numeric format id.</summary>
    public int FormatId { get; }

    /// <summary>The format code string (e.g. <c>"0.00"</c>, <c>"dd/MM/yyyy"</c>).</summary>
    public string FormatCode { get; }

    /// <summary><see langword="true" /> when this is a built-in format (id &lt; 164).</summary>
    public bool IsBuiltIn => FormatId < FirstCustomId;

    /// <inheritdoc />
    public bool Equals(NumberFormat? other) =>
        other != null && FormatId == other.FormatId && FormatCode == other.FormatCode;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as NumberFormat);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(FormatId, FormatCode);
}
