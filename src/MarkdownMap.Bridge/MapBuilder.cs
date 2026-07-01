using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarkdownMap.Contract;
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

    /// <summary>Full pipeline: raw OSM bytes → MapModel JSON. <paramref name="optionsJson"/> tunes
    /// the embedded render; <paramref name="progress"/> is optional.</summary>
    public static string FromOsm(byte[] osmBytes, string? optionsJson = null, BuildProgress? progress = null)
    {
        try
        {
            using var stream = new MemoryStream(osmBytes);
            var fc = new OsmNormalizer().Normalize(stream, progress);
            return Build(fc, ParseOptions(optionsJson), progress);
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Generator only: GeoJSON text → MapModel JSON. <paramref name="optionsJson"/> tunes
    /// the embedded render; <paramref name="progress"/> is optional.</summary>
    public static string FromGeoJson(string geojson, string? optionsJson = null, BuildProgress? progress = null)
    {
        try
        {
            var fc = GeoJsonReader.Read(geojson);
            return Build(fc, ParseOptions(optionsJson), progress);
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>
    /// Re-render every view of an existing model with new options (ADR-0011) — no rebuild. Returns
    /// the refreshed MapModel JSON (whole-area markdown plus scene-chunks/manifest when Chunking is
    /// on, ADR-0016), or <c>{"error":"..."}</c> JSON on failure.
    /// </summary>
    public static string RenderFromModel(string mapModelJson, string? optionsJson = null)
    {
        try
        {
            var model = JsonSerializer.Deserialize<MapModel>(mapModelJson, Options)
                        ?? throw new ArgumentException("could not parse MapModel");
            new MapGenerator(ParseOptions(optionsJson)).Compose(model);
            return JsonSerializer.Serialize(model, Options);
        }
        catch (Exception ex) { return Error(ex); }
    }

    private static string Build(FeatureCollection fc, GeneratorOptions opts, BuildProgress? progress)
    {
        progress?.Invoke(BuildPhase.Building, fc.Features.Count);
        var model = new MapGenerator(opts).BuildModel(fc);
        progress?.Invoke(BuildPhase.Serializing, 0);
        return JsonSerializer.Serialize(model, Options);
    }

    /// <summary>Map the three render-only knobs from camelCase JSON onto the defaults (ADR-0011).</summary>
    private static GeneratorOptions ParseOptions(string? optionsJson)
    {
        var o = GeneratorOptions.Default;
        if (string.IsNullOrWhiteSpace(optionsJson)) return o;
        var dto = JsonSerializer.Deserialize<OptionsDto>(optionsJson!, Options);
        if (dto is null) return o;
        if (dto.Bidirectional is bool b) o.Bidirectional = b;
        if (dto.InlineNeighborName is bool i) o.InlineNeighborName = i;
        if (dto.DirectivePreamble is bool d) o.DirectivePreamble = d;
        if (dto.Chunking is bool ch) o.Chunking = ch;
        if (dto.SceneSize is string s) o.SceneSize = GeneratorOptions.SceneSizeFor(s);
        return o;
    }

    private sealed class OptionsDto
    {
        public bool? Bidirectional { get; set; }
        public bool? InlineNeighborName { get; set; }
        public bool? DirectivePreamble { get; set; }
        public bool? Chunking { get; set; }
        public string? SceneSize { get; set; }
    }

    private static string Error(Exception ex) =>
        JsonSerializer.Serialize(new ErrorResult { Error = ex.Message }, Options);

    private sealed class ErrorResult { public string Error { get; set; } = ""; }
}
