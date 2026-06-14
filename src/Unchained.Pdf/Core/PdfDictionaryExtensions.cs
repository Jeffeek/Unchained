namespace Unchained.Pdf.Core;

/// <summary>
///     Convenience predicates for the recurring <c>/Type</c> and <c>/Subtype</c> name checks on
///     PDF dictionaries. Centralises the <c>dict.GetName("Type") == "…"</c> idiom that otherwise
///     recurs across the editors and writers.
/// </summary>
internal static class PdfDictionaryExtensions
{
    extension(PdfDictionary? dict)
    {
        /// <summary>Whether the dictionary's <c>/Type</c> entry equals <paramref name="type" />.</summary>
        internal bool IsType(string type) => dict?.GetName("Type") == type;

        /// <summary>Whether the dictionary's <c>/Subtype</c> entry equals <paramref name="subtype" />.</summary>
        internal bool IsSubtype(string subtype) => dict?.GetName("Subtype") == subtype;

        /// <summary>Whether the dictionary is a page object (<c>/Type /Page</c>).</summary>
        internal bool IsPage() => dict.IsType("Page");

        /// <summary>Whether the dictionary is the document catalog (<c>/Type /Catalog</c>).</summary>
        internal bool IsCatalog() => dict.IsType("Catalog");
    }
}
