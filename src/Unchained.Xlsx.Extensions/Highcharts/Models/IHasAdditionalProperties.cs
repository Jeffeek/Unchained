namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Marker interface indicating the object exposes an <see cref="Dictionary&lt;string, object&gt;" /> dictionary
///     that can be merged into the corresponding JSON subtree during serialization.
/// </summary>
public interface IHasAdditionalProperties
{
    /// <summary>
    ///     Returns the additional properties dictionary.
    ///     Returns null if no additional properties are set.
    /// </summary>
    Dictionary<string, object>? GetAdditionalProperties();
}
