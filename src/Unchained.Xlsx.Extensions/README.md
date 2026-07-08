# Unchained.Xlsx.Extensions

Extensions for **Unchained.Xlsx** — adding Highcharts JSON export and other utilities.

## Highcharts Converter

`HighchartsConverter` converts an `Unchained.Xlsx.Drawings.ChartDrawing` into a strongly-typed `HighchartsOptions` that serialises to camelCase JSON via `System.Text.Json`.

### Usage

```csharp
using Unchained.Xlsx.Extensions.Highcharts;

// Simple one-liner
var json = chartDrawing.ToHighchartsJson();

// Or with custom settings
var settings = new HighchartsSettings
{
    AdditionalProperties = new Dictionary<string, object>
    {
        ["renderTo"] = "my-chart-container"
    },
    AllowOverrides = true
};
var options = chartDrawing.ToHighchartsObject(settings);
var json = options.ToJson(settings);
```

### Features

- **Chart type mapping** — all 28 Excel chart types mapped to Highcharts equivalents (`column`, `line`, `bar`, `pie`, `doughnut`, `area`, `scatter`, `bubble`, `line`+polar for radar)
- **Formula-safe data extraction** — reads evaluated values from the chart's underlying data (call `chart.Workbook?.Recalculate()` first to force a recalculation pass)
- **Explicit colour support** — extracts solid RGB fill colours as `#RRGGBB` hex strings; omits the property when not set (frontend theme handles it)
- **Null safety** — `NaN` and `Infinity` data values become `null` in the output; empty titles default to "Untitled Chart"
- **JSON serialisation** — `ToJson()` helper with camelCase policy and null-property omission
- **AdditionalProperties escape hatch** — both `ToHighchartsObject` and `ToJson` accept `HighchartsSettings` with an `AdditionalProperties` dictionary, letting you inject or override any property in the output

### Series-level mapping

Each series is mapped individually, preserving its name, type, colour, and data array. This enables hybrid/mixed charts (e.g. Column + Line) in future iterations where series-level type overrides are respected.

### Colour scheme

By default, only explicitly-set RGB colours on series fills are emitted. To also resolve theme-colour references, pass a `ColorScheme` to the converter:

```csharp
var converter = new HighchartsConverter(colorScheme: document.Styles.ColorScheme);
```
