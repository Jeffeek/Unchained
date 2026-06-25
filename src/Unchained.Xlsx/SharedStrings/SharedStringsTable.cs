using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;

namespace Unchained.Xlsx.SharedStrings;

/// <summary>
///     The workbook shared-string interning table (<c>xl/sharedStrings.xml</c>). Provides O(1)
///     index → string lookup for reads and O(1) get-or-add for writes via a value → index map.
///     Rich-text strings are stored as their concatenated plain text; the original <c>&lt;si&gt;</c>
///     element is preserved for round-tripping.
/// </summary>
internal sealed class SharedStringsTable
{
    private readonly List<string> _strings = [];
    private readonly Dictionary<string, int> _lookup = new(StringComparer.Ordinal);
    private readonly List<XElement?> _rawElements = [];

    /// <summary>The number of unique strings in the table.</summary>
    public int Count => _strings.Count;

    /// <summary>
    ///     <see langword="true" /> when at least one entry was added or replaced since the table was
    ///     parsed — used to decide whether the part must be rewritten on save.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>Returns the string at <paramref name="index" />, or <see langword="null" /> if out of range.</summary>
    public string? Get(int index) =>
        index >= 0 && index < _strings.Count ? _strings[index] : null;

    /// <summary>Returns the existing index of <paramref name="value" /> or appends it and returns the new index.</summary>
    public int GetOrAdd(string value)
    {
        if (_lookup.TryGetValue(value, out var existing))
            return existing;

        var index = _strings.Count;
        _strings.Add(value);
        _rawElements.Add(null);
        _lookup[value] = index;
        IsDirty = true;
        return index;
    }

    // ── Parse ──────────────────────────────────────────────────────────────────

    public static SharedStringsTable Parse(byte[]? bytes)
    {
        var table = new SharedStringsTable();
        if (bytes == null || bytes.Length == 0)
            return table;

        var root = OoXmlHelper.ParseXml(bytes).Root;
        if (root == null)
            return table;

        foreach (var si in root.Children(SmlNames.Si))
        {
            var text = ExtractText(si);
            var index = table._strings.Count;
            table._strings.Add(text);
            table._rawElements.Add(si);
            table._lookup.TryAdd(text, index);
        }

        return table;
    }

    /// <summary>Concatenates the plain text of an <c>&lt;si&gt;</c> element (rich runs included).</summary>
    private static string ExtractText(XElement si)
    {
        // Plain string: <si><t>...</t></si>
        var direct = si.Child(SmlNames.Text);
        if (direct != null && !si.Children(SmlNames.RichRun).Any())
            return direct.Value;

        // Rich text: <si><r><t>...</t></r>...</si>
        return string.Concat(si.Children(SmlNames.RichRun)
            .Select(static r => r.Child(SmlNames.Text)?.Value ?? string.Empty));
    }

    // ── Serialize ──────────────────────────────────────────────────────────────

    public byte[] Serialize()
    {
        var root = new XElement(
            SmlNames.Sst,
            new XAttribute("xmlns", SmlNames.X.NamespaceName),
            new XAttribute("count", _strings.Count),
            new XAttribute("uniqueCount", _strings.Count));

        for (var i = 0; i < _strings.Count; i++)
        {
            if (_rawElements[i] != null)
            {
                root.Add(new XElement(_rawElements[i]!));
                continue;
            }

            root.Add(new XElement(SmlNames.Si, MakeText(_strings[i])));
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
    }

    private static XElement MakeText(string value)
    {
        var element = new XElement(SmlNames.Text, value);
        // Preserve leading/trailing whitespace exactly as XLSX requires.
        if (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])))
            element.SetAttributeValue(XNamespace.Xml + "space", "preserve");
        return element;
    }
}
