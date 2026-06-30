using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarkdownMap.Generator;
using MarkdownMap.Normalizer;

namespace MarkdownMap.Bridge;

/// <summary>
/// Orchestrates the pipeline into MapModel JSON for the Explorer. The WASM module
/// (`MarkdownMap.Wasm`) is a thin [JSExport] shim over this; keeping the logic here makes
/// it unit-testable without a wasm host. Errors are returned as <c>{"error": "..."}</c>.
/// </summary>
public static class MapBuilder
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Full pipeline: raw OSM bytes → MapModel JSON.</summary>
    public static string FromOsm(byte[] osmBytes)
    {
        try
        {
            using var stream = new MemoryStream(osmBytes);
            var fc = new OsmNormalizer().Normalize(stream);
            return JsonSerializer.Serialize(new MapGenerator().BuildModel(fc), Options);
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Generator only: GeoJSON text → MapModel JSON.</summary>
    public static string FromGeoJson(string geojson)
    {
        try
        {
            var fc = GeoJsonReader.Read(geojson);
            return JsonSerializer.Serialize(new MapGenerator().BuildModel(fc), Options);
        }
        catch (Exception ex) { return Error(ex); }
    }

    private static string Error(Exception ex) =>
        JsonSerializer.Serialize(new ErrorResult { Error = ex.Message }, Options);

    private sealed class ErrorResult { public string Error { get; set; } = ""; }
}
