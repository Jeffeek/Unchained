# Unchained.Xlsx — Samples

A runnable console walkthrough of the [`Unchained.Xlsx`](../../src/Unchained.Xlsx) public API.

## Run

```bash
# Interactive menu
dotnet run --project samples/Unchained.Xlsx.Samples

# Run every demo
dotnet run --project samples/Unchained.Xlsx.Samples -- all

# Run a single demo
dotnet run --project samples/Unchained.Xlsx.Samples -- charts
```

Output files are written to an `output/` directory next to the built executable
(`bin/Debug/net9.0/output`).

## Demos

| Key | What it shows |
|---|---|
| `create` | Build a styled workbook (bold/filled header, currency format) and set document properties |
| `formulas` | Write `SUM`/`AVERAGE` formulas, `Recalculate()`, and evaluate an ad-hoc formula with `EvaluateFormula` |
| `tables` | Promote a range to a banded Excel table (`AddTable` → `ListObject`) |
| `validation` | Add a drop-down (`AddDropdownValidation`) and a workbook-scoped named range |
| `charts` | Embed a clustered-column chart anchored over its data (`AddChart`) |
| `pivot` | Summarise raw rows with a pivot table (`AddPivotTable` → group by region, sum amount) |
| `csv` | Import a CSV with type inference (`LoadFromCsvAsync`) and export back out (`SaveAsCsvAsync`) |
| `encrypt` | Save with AES-256 encryption and re-open with the password |
| `read` | Read every non-empty cell from a workbook (`GetUsedRange` + `GetFormattedString`) |

`Unchained.Xlsx` is pure-managed — these demos have no native dependencies.
