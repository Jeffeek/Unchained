namespace Unchained.Studio.Studio;

/// <summary>
///     Parses a user-entered page-range expression such as <c>"1,3,5-8"</c> into a sorted,
///     deduplicated list of 1-based page numbers, validated against the document's page count.
///     Shared by the page-range dialogs (remove, split, export-comparison, batch export).
/// </summary>
public static class PageRangeParser
{
    /// <summary>
    ///     Parses <paramref name="text" /> into a sorted, deduplicated page list in <c>1..max</c>.
    ///     Returns <see langword="null" /> and sets <paramref name="error" /> on any invalid token
    ///     or out-of-bounds value.
    /// </summary>
    public static List<int>? Parse(string text, int max, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Enter at least one page number.";
            return null;
        }

        var pages = new SortedSet<int>();

        foreach (var token in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Contains('-'))
            {
                var parts = token.Split('-');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0].Trim(), out var from) ||
                    !int.TryParse(parts[1].Trim(), out var to))
                {
                    error = $"Invalid range: \"{token}\"";
                    return null;
                }

                if (from < 1 || to > max || from > to)
                {
                    error = $"Range {from}-{to} is out of bounds (1-{max}).";
                    return null;
                }

                for (var p = from; p <= to; p++) pages.Add(p);
            }
            else
            {
                if (!int.TryParse(token, out var p) || p < 1 || p > max)
                {
                    error = $"Invalid page number: \"{token}\" (1-{max}).";
                    return null;
                }

                pages.Add(p);
            }
        }

        if (pages.Count != 0) return [.. pages];

        error = "No valid pages found.";
        return null;
    }
}
