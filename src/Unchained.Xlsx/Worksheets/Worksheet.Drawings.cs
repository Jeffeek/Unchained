using Unchained.Ooxml.Media;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    private DrawingCollection? _drawings;

    /// <summary>The drawings (pictures and charts) anchored on this worksheet.</summary>
    public DrawingCollection Drawings
    {
        get
        {
            if (_drawings != null)
                return _drawings;

            _drawings = new DrawingCollection();
            ParseDrawings(_drawings);
            return _drawings;
        }
    }

    /// <summary>Adds an image to the sheet at the given anchor and returns the picture drawing.</summary>
    public PictureDrawing AddImage(byte[] imageBytes, string contentType, DrawingAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentNullException.ThrowIfNull(anchor);

        var picture = new PictureDrawing(new EmbeddedImage(contentType, imageBytes)) { Anchor = anchor };
        Drawings.Add(picture);
        return picture;
    }

    /// <summary>Adds an image anchored to a single cell with a pixel size.</summary>
    public PictureDrawing AddImage(byte[] imageBytes, string contentType, CellReference cell,
        double widthPixels = 480, double heightPixels = 288) =>
        AddImage(imageBytes, contentType, DrawingAnchor.OneCell(cell, widthPixels, heightPixels));

    /// <summary>
    ///     Adds a chart of the given type plotting <paramref name="dataRange" /> at <paramref name="anchor" />.
    ///     The first row/column of the range is treated as the category labels; remaining columns
    ///     become series. Data values are snapshotted as chart literals at creation time.
    /// </summary>
    public ChartDrawing AddChart(
        Ooxml.Charts.ChartType type,
        CellRange dataRange,
        DrawingAnchor anchor,
        string? title = null
    )
    {
        ArgumentNullException.ThrowIfNull(anchor);

        var chart = new ChartDrawing { Anchor = anchor };
        chart.Chart.Type = type;
        if (!string.IsNullOrEmpty(title))
        {
            chart.Chart.Title = title;
            chart.Chart.HasTitle = true;
        }

        chart.Chart.Legend.IsVisible = true;
        PopulateChartData(chart.Chart, dataRange);
        Drawings.Add(chart);
        return chart;
    }

    /// <summary>Reads <paramref name="range" /> into the chart model: column 1 = categories, each other column = a series.</summary>
    private void PopulateChartData(Ooxml.Charts.ChartModel model, CellRange range)
    {
        var topRow = range.TopLeft.Row;
        var leftCol = range.TopLeft.Column;
        var hasHeaderRow = true;

        // Categories from the first column (skip the header cell).
        for (var r = topRow + (hasHeaderRow ? 1 : 0); r <= range.BottomRight.Row; r++)
            model.Data.Categories.Add(GetCell(r, leftCol)?.GetFormattedString() ?? string.Empty);

        // One series per remaining column.
        for (var c = leftCol + 1; c <= range.BottomRight.Column; c++)
        {
            var series = new Ooxml.Charts.ChartSeries
            {
                Name = GetCell(topRow, c)?.GetFormattedString() ?? CellReference.ColumnNumberToLetters(c)
            };
            for (var r = topRow + (hasHeaderRow ? 1 : 0); r <= range.BottomRight.Row; r++)
                series.Values.Add(GetCell(r, c)?.GetDouble() ?? 0);
            model.Data.Series.Add(series);
        }
    }

    internal bool DrawingsMaterialised => _drawings != null;

    internal DrawingCollection? DrawingsOrNull => _drawings;

    /// <summary>The OPC part URI of this sheet's drawing layer (assigned on write).</summary>
    internal string DrawingPartUri { get; set; } = string.Empty;

    /// <summary>The relationship id from this sheet to its drawing part (assigned on write).</summary>
    internal string DrawingRelationshipId { get; set; } = string.Empty;

    private void ParseDrawings(DrawingCollection drawings)
    {
        if (RawElement == null || Document.Package == null)
            return;

        var part = Document.Package.TryGetPart(PartUri);
        var drawingRelId = (string?)RawElement.Child(SmlNames.Drawing)?.Attribute(SmlNames.R + "id");
        if (part == null || drawingRelId == null)
            return;

        var rel = part.Relationships.FirstOrDefault(r => r.Id == drawingRelId);
        if (rel == null)
            return;

        var drawingUri = part.ResolveUri(rel.TargetUri);
        var drawingPart = Document.Package.TryGetPart(drawingUri);
        if (drawingPart == null)
            return;

        Parsing.DrawingParser.Parse(Document, drawingPart, drawingUri, OoXmlHelper.ParseXml(drawingPart.Data).Root!, drawings);
    }
}
