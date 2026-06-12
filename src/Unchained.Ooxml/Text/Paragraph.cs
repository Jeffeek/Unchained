namespace Unchained.Ooxml.Text;

/// <summary>
///     A paragraph within a <see cref="TextFrame" />, containing an ordered sequence of
///     <see cref="Run" /> objects with shared paragraph-level formatting.
/// </summary>
public sealed class Paragraph
{
    /// <summary>The text runs that make up this paragraph.</summary>
    public RunCollection Runs { get; } = new();

    /// <summary>
    ///     Gets or sets the paragraph's text as a plain string.
    ///     <para>
    ///         Getter: concatenates all run texts in order.
    ///         Setter: replaces all existing runs with a single run containing the given text.
    ///     </para>
    /// </summary>
    public string PlainText
    {
        get => string.Concat(Runs.Select(static r => r.Text));
        set
        {
            Runs.Clear();
            Runs.Add(value);
        }
    }

    // ── Paragraph formatting ─────────────────────────────────────────────────

    /// <summary>Horizontal text alignment. <see langword="null" /> means inherit.</summary>
    public TextAlignment? Alignment { get; set; }

    /// <summary>Space before the paragraph in points. <see langword="null" /> means inherit.</summary>
    public double? SpaceBeforePoints { get; set; }

    /// <summary>Space after the paragraph in points. <see langword="null" /> means inherit.</summary>
    public double? SpaceAfterPoints { get; set; }

    /// <summary>Line spacing. <see langword="null" /> means inherit (typically single-spaced).</summary>
    public LineSpacing? Spacing { get; set; }

    /// <summary>Left margin in EMU. <see langword="null" /> means inherit.</summary>
    public Emu? MarginLeft { get; set; }

    /// <summary>Right margin in EMU. <see langword="null" /> means inherit.</summary>
    public Emu? MarginRight { get; set; }

    /// <summary>
    ///     Indent (first-line indent) in EMU. A negative value creates a hanging indent.
    ///     <see langword="null" /> means inherit.
    /// </summary>
    public Emu? Indent { get; set; }

    /// <summary>Outline level (0 = top level). Used for SmartArt and outline views.</summary>
    public int OutlineLevel { get; set; }

    /// <summary><see langword="true" /> when the paragraph reads right-to-left.</summary>
    public bool RightToLeft { get; set; }

    /// <summary>Bullet / list marker settings for this paragraph.</summary>
    public BulletFormat Bullet { get; } = new();

    // ── Find & replace ───────────────────────────────────────────────────────

    /// <summary>
    ///     Replaces every occurrence of <paramref name="oldText" /> with <paramref name="newText" />
    ///     across this paragraph's runs, preserving the formatting of the run where each match begins.
    ///     Field runs (slide number, date, etc.) act as boundaries and are never altered.
    /// </summary>
    /// <returns>The number of occurrences replaced.</returns>
    public int ReplaceText(
        string oldText,
        string newText,
        StringComparison comparison = StringComparison.Ordinal
    )
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        if (oldText.Length == 0) return 0;

        // Split runs into segments of consecutive non-field runs; matches never cross a field.
        var count = 0;
        var segment = new List<Run>();
        foreach (var run in Runs)
        {
            if (run.Field.HasValue)
            {
                count += ReplaceInSegment(segment, oldText, newText, comparison);
                segment.Clear();
            }
            else
                segment.Add(run);
        }

        count += ReplaceInSegment(segment, oldText, newText, comparison);
        return count;
    }

    /// <summary>
    ///     Replaces matches within a contiguous run segment. The match's replacement text is written
    ///     onto the run where the match starts (keeping that run's format); text belonging to other
    ///     runs spanned by the match is removed.
    /// </summary>
    private static int ReplaceInSegment(
        List<Run> runs,
        string oldText,
        string newText,
        StringComparison comparison
    )
    {
        if (runs.Count == 0) return 0;

        var count = 0;
        while (true)
        {
            var combined = string.Concat(runs.Select(static r => r.Text));
            var idx = combined.IndexOf(oldText, comparison);
            if (idx < 0) break;

            var end = idx + oldText.Length;
            int startRun = -1, startOffset = 0, endRun = -1, endOffset = 0;
            var pos = 0;
            for (var i = 0; i < runs.Count; i++)
            {
                var len = runs[i].Text.Length;
                var runStart = pos;
                var runEnd = pos + len;
                if (startRun == -1 && idx < runEnd)
                {
                    startRun = i;
                    startOffset = idx - runStart;
                }

                if (startRun != -1 && end <= runEnd)
                {
                    endRun = i;
                    endOffset = end - runStart;
                    break;
                }

                pos = runEnd;
            }

            if (startRun == -1) break; // defensive: should not happen

            if (startRun == endRun)
            {
                var r = runs[startRun];
                r.Text = string.Concat(r.Text.AsSpan(0, startOffset), newText, r.Text.AsSpan(endOffset));
            }
            else
            {
                runs[startRun].Text = string.Concat(runs[startRun].Text.AsSpan(0, startOffset), newText);
                for (var i = startRun + 1; i < endRun; i++)
                    runs[i].Text = string.Empty;
                runs[endRun].Text = runs[endRun].Text[endOffset..];
            }

            count++;
        }

        return count;
    }
}
