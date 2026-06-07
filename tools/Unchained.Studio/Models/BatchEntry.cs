using Unchained.Studio.Enums;

namespace Unchained.Studio.Models;

internal sealed class BatchEntry
{
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public byte[] Bytes { get; init; } = [];
    public BatchStatus Status { get; set; } = BatchStatus.Waiting;
    public int TotalPages { get; set; }
    public int PagesOkUnchained { get; set; }
    public int PagesOkPdfJs { get; set; }
    public List<string> RenderErrors { get; } = [];
    public string? LoadError { get; set; }
}
