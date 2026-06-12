namespace Unchained.Ooxml.Media;

/// <summary>
///     An audio clip embedded in or linked from the presentation package.
///     Full playback is not supported in M1–M4; this class exists to preserve the
///     metadata and bytes faithfully through load/save round-trips.
/// </summary>
public sealed class EmbeddedAudio
{
    /// <summary>The raw audio bytes, or <see langword="null" /> when the clip is externally linked.</summary>
    public ReadOnlyMemory<byte>? Data { get; init; }

    /// <summary>The path of an externally linked audio file, or <see langword="null" /> for embedded clips.</summary>
    public string? LinkedFilePath { get; init; }

    /// <summary>The MIME content type (e.g. <c>"audio/mpeg"</c>, <c>"audio/wav"</c>).</summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary><see langword="true" /> when the clip data is embedded in the package.</summary>
    public bool IsEmbedded => Data.HasValue;
}
