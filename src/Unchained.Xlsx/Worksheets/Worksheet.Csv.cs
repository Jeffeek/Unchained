using Unchained.Xlsx.Export;
using Unchained.Xlsx.Models;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    /// <summary>Exports this worksheet's cells to a CSV file.</summary>
    public async Task SaveAsCsvAsync(
        string path,
        CsvSaveOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = await Task.Run(() => CsvExporter.Export(this, options ?? CsvSaveOptions.Default), cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Exports this worksheet's cells to a CSV stream.</summary>
    public async Task SaveAsCsvAsync(
        Stream stream,
        CsvSaveOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(stream);
        var bytes = await Task.Run(() => CsvExporter.Export(this, options ?? CsvSaveOptions.Default), cancellationToken)
            .ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Exports this worksheet's cells to CSV and returns the bytes.</summary>
    public byte[] ToCsv(CsvSaveOptions? options = null) =>
        CsvExporter.Export(this, options ?? CsvSaveOptions.Default);
}
