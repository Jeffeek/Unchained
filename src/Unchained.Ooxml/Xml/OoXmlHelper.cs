using System.Globalization;
using System.Xml.Linq;

namespace Unchained.Ooxml.Xml;

/// <summary>
///     Extension methods and helper utilities for working with <see cref="XElement" /> objects
///     in the context of OOXML parsing and writing.
/// </summary>
internal static class OoXmlHelper
{
    // ── Attribute reading ────────────────────────────────────────────────────

    /// <summary>
    ///     Returns the string value of an attribute, or <see langword="null" /> if the
    ///     attribute is absent.
    /// </summary>
    public static string? GetAttr(this XElement element, string attributeName) =>
        (string?)element.Attribute(attributeName);

    /// <summary>
    ///     Returns the string value of an attribute, or <paramref name="defaultValue" /> if absent.
    /// </summary>
    public static string GetAttr(
        this XElement element,
        string attributeName,
        string defaultValue
    ) =>
        (string?)element.Attribute(attributeName) ?? defaultValue;

    /// <summary>
    ///     Returns the <see langword="long" /> value of an attribute, or <see langword="null" />
    ///     if the attribute is absent or not a valid integer.
    /// </summary>
    public static long? GetAttrLong(this XElement element, string attributeName)
    {
        var raw = (string?)element.Attribute(attributeName);
        return raw != null && long.TryParse(raw, out var value) ? value : null;
    }

    /// <summary>
    ///     Returns the <see langword="long" /> value of an attribute, or
    ///     <paramref name="defaultValue" /> if absent or unparseable.
    /// </summary>
    public static long GetAttrLong(
        this XElement element,
        string attributeName,
        long defaultValue
    ) =>
        GetAttrLong(element, attributeName) ?? defaultValue;

    /// <summary>
    ///     Returns the <see langword="int" /> value of an attribute, or <see langword="null" />
    ///     if the attribute is absent or not a valid integer.
    /// </summary>
    public static int? GetAttrInt(this XElement element, string attributeName)
    {
        var raw = (string?)element.Attribute(attributeName);
        return raw != null && int.TryParse(raw, out var value) ? value : null;
    }

    /// <summary>
    ///     Returns the <see langword="int" /> value of an attribute, or
    ///     <paramref name="defaultValue" /> if absent or unparseable.
    /// </summary>
    public static int GetAttrInt(
        this XElement element,
        string attributeName,
        int defaultValue
    ) =>
        GetAttrInt(element, attributeName) ?? defaultValue;

    /// <summary>
    ///     Returns the <see langword="double" /> value of an attribute, or <see langword="null" />
    ///     if absent or unparseable.
    /// </summary>
    public static double? GetAttrDouble(this XElement element, string attributeName)
    {
        var raw = (string?)element.Attribute(attributeName);
        return raw != null && double.TryParse(
            raw,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    /// <summary>
    ///     Returns the <see langword="bool" /> value of an attribute encoded as <c>"1"</c>/<c>"0"</c>
    ///     or <c>"true"</c>/<c>"false"</c>, or <see langword="null" /> if absent.
    /// </summary>
    public static bool? GetAttrBool(this XElement element, string attributeName)
    {
        var raw = (string?)element.Attribute(attributeName);
        if (raw is null) return null;
        if (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (raw == "0" || raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    // ── Element navigation ───────────────────────────────────────────────────

    /// <summary>
    ///     Returns the first child element with the given name, or <see langword="null" /> if absent.
    /// </summary>
    public static XElement? Child(this XElement element, XName name) =>
        element.Element(name);

    /// <summary>
    ///     Returns the first child element with the given name.
    ///     Throws <see cref="OoXmlException" /> if the element is absent.
    /// </summary>
    public static XElement RequiredChild(this XElement element, XName name) =>
        element.Element(name)
        ?? throw new OoXmlException(
            $"Required child element '{name.LocalName}' is missing inside '{element.Name.LocalName}'.");

    /// <summary>
    ///     Returns all child elements with the given name.
    /// </summary>
    public static IEnumerable<XElement> Children(this XElement element, XName name) =>
        element.Elements(name);

    // ── EMU helpers ──────────────────────────────────────────────────────────

    /// <summary>
    ///     Reads a long integer attribute and wraps it as an <see cref="Emu" />.
    ///     Returns <see cref="Emu.Zero" /> when the attribute is absent.
    /// </summary>
    public static Emu GetAttrEmu(this XElement element, string attributeName) =>
        new(GetAttrLong(element, attributeName) ?? 0L);

    // ── Serialization helpers ─────────────────────────────────────────────────

    /// <summary>
    ///     Converts an <see cref="XDocument" /> to a UTF-8 byte array with an XML declaration.
    /// </summary>
    public static byte[] ToUtf8Bytes(this XDocument document)
    {
        using var ms = new MemoryStream();
        document.Save(ms);
        return ms.ToArray();
    }

    /// <summary>
    ///     Parses an XML byte array into an <see cref="XDocument" />.
    /// </summary>
    public static XDocument ParseXml(byte[] bytes) =>
        XDocument.Load(new MemoryStream(bytes));

    // ── Rotation helpers ─────────────────────────────────────────────────────

    /// <summary>
    ///     Converts an OOXML rotation value (in 1/60,000 degrees) to degrees.
    /// </summary>
    public static double OoxmlRotationToDegrees(int ooxmlRotation) =>
        ooxmlRotation / 60_000.0;

    /// <summary>
    ///     Converts degrees to an OOXML rotation value (in 1/60,000 degrees).
    /// </summary>
    public static int DegreesToOoxmlRotation(double degrees) =>
        (int)(degrees * 60_000);
}
