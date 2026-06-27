using Unchained.Ooxml;

namespace Unchained.Xlsx.Models;

/// <summary>Settings that control how a workbook is serialized to <c>.xlsx</c>.</summary>
public sealed class XlsxSaveOptions
{
    /// <summary>The default save options.</summary>
    public static readonly XlsxSaveOptions Default = new();

    /// <summary>The OOXML conformance class. Defaults to <see cref="OoXmlConformance.Transitional" />.</summary>
    public OoXmlConformance Conformance { get; init; } = OoXmlConformance.Transitional;

    /// <summary>The ZIP64 policy. Defaults to <see cref="Zip64Policy.IfNecessary" />.</summary>
    public Zip64Policy Zip64 { get; init; } = Zip64Policy.IfNecessary;

    /// <summary>
    ///     When set, the workbook is AES-256 encrypted (ECMA-376 Part 4) with this password on save.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    ///     When <see langword="true" /> (the default), all formulas are flagged for recalculation
    ///     the next time the workbook is opened in a spreadsheet application. Unchained does not
    ///     evaluate formulas itself.
    /// </summary>
    public bool RecalcAll { get; init; } = true;
}
