using System.Text.Json;
using System.Text.Json.Nodes;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Extensions.Highcharts.Models;

namespace Unchained.Xlsx.Extensions.Highcharts;

/// <summary>
///     Extension methods for <see cref="ChartDrawing" />
///     that provide fluent convenience overloads.
/// </summary>
public static class Extensions
{
    extension(ChartDrawing chart)
    {
        /// <summary>
        ///     Converts the chart drawing and returns the JSON string directly.
        /// </summary>
        public string ToHighchartsJson()
        {
            var converter = new HighchartsConverter();
            return converter.Convert(chart).ToJson();
        }

        /// <summary>
        ///     Converts the chart drawing into a <see cref="HighchartsOptions" /> object
        ///     with optional settings for additional properties and overrides.
        /// </summary>
        public HighchartsOptions ToHighchartsObject(HighchartsSettings? settings = null)
        {
            var converter = new HighchartsConverter();
            var options = converter.Convert(chart);

            if (settings?.AdditionalProperties is null)
                return options;

            options.AdditionalProperties ??= [];
            foreach (var (key, value) in settings.AdditionalProperties)
                options.AdditionalProperties[key] = value;

            return options;
        }
    }

    /// <summary>
    ///     Serialises this options object to camelCase JSON, ignoring null properties.
    ///     Optionally merges additional properties from settings.
    /// </summary>
    public static string ToJson(this HighchartsOptions options, HighchartsSettings? settings = null)
    {
        var node = JsonSerializer.SerializeToNode(options)!;

        // Merge additional properties from the object graph (fills gaps in the typed API).
        CollectAndMergeAdditionalProperties(options, node);

        // Merge user settings on top (highest priority — user can override anything).
        if (settings?.AdditionalProperties is not null)
            MergeAdditionalProperties(node, settings.AdditionalProperties, settings.AllowOverrides);

        return node.ToJsonString(HighchartsConverter.JsonOptions);
    }

    /// <summary>Walks the object graph and merges <see cref="IHasAdditionalProperties" /> dicts into the JSON tree.</summary>
    private static void CollectAndMergeAdditionalProperties(object obj, JsonNode node)
    {
        if (obj is IHasAdditionalProperties hp && hp.GetAdditionalProperties() is { Count: > 0 } dict)
            MergeAdditionalProperties(node, dict, allowOverride: false);

        // Recurse into object-valued properties.
        if (node is not JsonObject objNode) return;

        foreach (var (key, child) in objNode)
        {
            if (child is null) continue;

            // Find the corresponding child object in the original graph.
            var childObj = FindChild(obj, key);
            if (childObj is not null)
                CollectAndMergeAdditionalProperties(childObj, child);
        }
    }

    /// <summary>Finds the child object in <paramref name="parent" /> matching the given JSON key.</summary>
    private static object? FindChild(object parent, string key)
    {
        var prop = parent.GetType().GetProperty(key);
        return prop?.GetValue(parent);
    }

    private static void MergeAdditionalProperties(JsonNode target, IDictionary<string, object>? additional, bool allowOverride)
    {
        if (additional is null || additional.Count == 0) return;

        foreach (var (key, value) in additional)
        {
            if (!allowOverride && target[key] is not null)
                continue;

            target[key] = CreateJsonValue(value);
        }
    }

    private static JsonNode CreateJsonValue(object value) =>
        value switch
        {
            JsonArray ja => ja,
            JsonObject jo => jo,
            JsonNode jn => jn,
            string s => JsonValue.Create(s),
            int i => JsonValue.Create(i),
            double d => JsonValue.Create(d),
            bool b => JsonValue.Create(b),
            _ => JsonSerializer.SerializeToNode(value)!
        };
}
