namespace Unchained.Pptx.Animations;

/// <summary>
/// Timing parameters for a single animation effect.
/// </summary>
public sealed class EffectTiming
{
    /// <summary>
    /// Delay before the effect starts, in seconds.
    /// Default: <c>0.0</c> (starts immediately relative to its trigger).
    /// </summary>
    public double DelaySeconds { get; set; }

    /// <summary>
    /// How long the animation plays, in seconds.
    /// Default: <c>0.5</c>.
    /// </summary>
    public double DurationSeconds { get; set; } = 0.5;

    /// <summary>
    /// Number of times the animation repeats. <c>0</c> means the animation plays once
    /// (no repeat). Set to a negative value to repeat indefinitely.
    /// Default: <c>0</c>.
    /// </summary>
    public int RepeatCount { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the animation plays forward then reverses at the end.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool AutoReverse { get; set; }
}
