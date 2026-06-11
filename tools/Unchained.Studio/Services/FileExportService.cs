using Microsoft.JSInterop;

namespace Unchained.Studio.Services;

public sealed class FileExportService(IJSRuntime js)
{
    public async Task TriggerDownloadAsync(
        byte[] data,
        string fileName,
        string mimeType = "application/octet-stream",
        CancellationToken ct = default)
    {
        await js.InvokeVoidAsync(
            "unchainedStudio.downloadFile",
            ct,
            Convert.ToBase64String(data),
            fileName,
            mimeType).ConfigureAwait(false);
    }
}
