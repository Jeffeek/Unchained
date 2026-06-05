namespace Unchained.Pptx.Core;

/// <summary>
/// A tri-state boolean used for formatting properties that can be explicitly set to
/// <see langword="true"/>, explicitly set to <see langword="false"/>, or left unset
/// so that the value is inherited from the slide layout, master, or theme.
/// </summary>
/// <remarks>
/// <para>
/// Many text and shape formatting attributes in OOXML have three meaningful states:
/// <list type="bullet">
///   <item><description><see cref="True"/> — the attribute is explicitly enabled.</description></item>
///   <item><description><see cref="False"/> — the attribute is explicitly disabled.</description></item>
///   <item><description><see cref="Inherit"/> — the attribute is not set; the effective
///   value comes from the enclosing layout, master, or theme.</description></item>
/// </list>
/// </para>
/// <para>
/// Using a plain <see langword="bool?"/> would work mechanically, but <see cref="InheritableBool"/>
/// makes the semantics explicit at every call site.
/// </para>
/// </remarks>
public readonly struct InheritableBool : IEquatable<InheritableBool>
{
    private readonly bool? _value;

    private InheritableBool(bool? value) => _value = value;

    // ── Named instances ─────────────────────────────────────────────────────

    /// <summary>The attribute is not set; its effective value is inherited.</summary>
    public static readonly InheritableBool Inherit = new(null);

    /// <summary>The attribute is explicitly set to <see langword="true"/>.</summary>
    public static readonly InheritableBool True = new(true);

    /// <summary>The attribute is explicitly set to <see langword="false"/>.</summary>
    public static readonly InheritableBool False = new(false);

    // ── State inspection ────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when the attribute has an explicit value
    /// (<see cref="True"/> or <see cref="False"/>); <see langword="false"/> when
    /// the value should be inherited.
    /// </summary>
    public bool IsSet => _value.HasValue;

    /// <summary>
    /// The explicit value, or <see langword="null"/> when the attribute is unset
    /// and should be inherited.
    /// </summary>
    public bool? Value => _value;

    // ── Factory ─────────────────────────────────────────────────────────────

    /// <summary>Creates an <see cref="InheritableBool"/> from a nullable boolean.</summary>
    /// <param name="value">
    /// <see langword="true"/> or <see langword="false"/> for an explicit value;
    /// <see langword="null"/> to represent <see cref="Inherit"/>.
    /// </param>
    public static InheritableBool From(bool? value) => value.HasValue ? new(value.Value) : Inherit;

    // ── Equality ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public bool Equals(InheritableBool other) => _value == other._value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is InheritableBool other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>Returns <see langword="true"/> when both values represent the same state.</summary>
    public static bool operator ==(InheritableBool left, InheritableBool right) => left.Equals(right);

    /// <summary>Returns <see langword="true"/> when the values represent different states.</summary>
    public static bool operator !=(InheritableBool left, InheritableBool right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _value.HasValue ? _value.Value.ToString() : "Inherit";
}
