using DocumentFormat.OpenXml.Presentation;
using Microsoft.AspNetCore.Components;

namespace Unchained.Studio.Services;

/// <summary>
///     Helper for picking files in Blazor Server. Uses a hidden file input element.
/// </summary>
public static class FilePicker
{
    /// <summary>Picks an image file from the user. Returns (bytes, contentType, name) or null if cancelled.</summary>
    public static async Task<(byte[] Bytes, string ContentType, string Name)?> PickImageAsync()
    {
        return null; // Simplified — will use inline approach in playboard instead.
    }
}
