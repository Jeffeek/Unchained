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

    extension(XElement element)
    {
        /// <summary>
        ///     Returns the string value of an attribute, or <see langword="null" /> if the
        ///     attribute is absent.
        /// </summary>
        public string? GetAttr(string attributeName) =>
            (string?)element.Attribute(attributeName);

        /// <summary>
        ///     Returns the string value of an attribute, or <paramref name="defaultValue" /> if absent.
        /// </summary>
        public string GetAttr(
            string attributeName,
            string defaultValue
        ) => (string?)element.Attribute(attributeName) ?? defaultValue;

        /// <summary>
        ///     Returns the <see langword="long" /> value of an attribute, or <see langword="null" />
        ///     if the attribute is absent or not a valid integer.
        /// </summary>
        public long? GetAttrLong(string attributeName)
        {
            var raw = (string?)element.Attribute(attributeName);
            return raw != null && long.TryParse(raw, out var value) ? value : null;
        }

        /// <summary>
        ///     Returns the <see langword="long" /> value of an attribute, or
        ///     <paramref name="defaultValue" /> if absent or unparseable.
        /// </summary>
        public long GetAttrLong(
            string attributeName,
            long defaultValue
        ) => GetAttrLong(element, attributeName) ?? defaultValue;

        /// <summary>
        ///     Returns the <see langword="int" /> value of an attribute, or <see langword="null" />
        ///     if the attribute is absent or not a valid integer.
        /// </summary>
        public int? GetAttrInt(string attributeName)
        {
            var raw = (string?)element.Attribute(attributeName);
            return raw != null && int.TryParse(raw, out var value) ? value : null;
        }

        /// <summary>
        ///     Returns the <see langword="int" /> value of an attribute, or
        ///     <paramref name="defaultValue" /> if absent or unparseable.
        /// </summary>
        public int GetAttrInt(
            string attributeName,
            int defaultValue
        ) => GetAttrInt(element, attributeName) ?? defaultValue;

        /// <summary>
        ///     Returns the <see langword="double" /> value of an attribute, or <see langword="null" />
        ///     if absent or unparseable.
        /// </summary>
        public double? GetAttrDouble(string attributeName)
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
        public bool? GetAttrBool(string attributeName)
        {
            var raw = (string?)element.Attribute(attributeName);
            if (raw is null) return null;
            if (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (raw == "0" || raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

            return null;
        }

        /// <summary>
        ///     Returns the first child element with the given name, or <see langword="null" /> if absent.
        /// </summary>
        public XElement? Child(XName name) =>
            element.Element(name);

        /// <summary>
        ///     Returns the first child element with the given name.
        ///     Throws <see cref="OoXmlException" /> if the element is absent.
        /// </summary>
        public XElement RequiredChild(XName name) =>
            element.Element(name)
            ?? throw new OoXmlException(
                $"Required child element '{name.LocalName}' is missing inside '{element.Name.LocalName}'.");

        /// <summary>
        ///     Returns all child elements with the given name.
        /// </summary>
        public IEnumerable<XElement> Children(XName name) =>
            element.Elements(name);

        /// <summary>
        ///     Reads a long integer attribute and wraps it as an <see cref="Emu" />.
        ///     Returns <see cref="Emu.Zero" /> when the attribute is absent.
        /// </summary>
        public Emu GetAttrEmu(string attributeName) =>
            new(GetAttrLong(element, attributeName) ?? 0L);
    }

    // ── Element navigation ───────────────────────────────────────────────────

    // ── EMU helpers ──────────────────────────────────────────────────────────

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
