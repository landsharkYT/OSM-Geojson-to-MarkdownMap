using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarkdownMap.Normalizer;

/// <summary>Canonical serialization options so the CLI and tests emit identical GeoJSON.</summary>
public static class NormalizerJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };
}
