using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MarkdownMap.Contract;

namespace MarkdownMap.Generator;

/// <summary>Reads a contract GeoJSON FeatureCollection and extracts Point coordinates.</summary>
public static class GeoJsonReader
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    public static FeatureCollection Read(string json) =>
        JsonSerializer.Deserialize<FeatureCollection>(json, Options) ?? new FeatureCollection();

    /// <summary>
    /// The lon/lat of a Point feature. Handles both an in-memory <c>double[]</c> and a
    /// deserialized <see cref="JsonElement"/> array (System.Text.Json materializes the
    /// contract's <c>object Coordinates</c> as a JsonElement).
    /// </summary>
    public static LonLat PointOf(Feature f)
    {
        var coords = f.Geometry.Coordinates;
        switch (coords)
        {
            case double[] a when a.Length >= 2:
                return new LonLat(a[0], a[1]);
            case JsonElement je when je.ValueKind == JsonValueKind.Array && je.GetArrayLength() >= 2:
                return new LonLat(je[0].GetDouble(), je[1].GetDouble());
            default:
                throw new FormatException(
                    $"Feature {f.Properties.OsmId} has unreadable Point coordinates ({coords?.GetType().Name ?? "null"}).");
        }
    }

    /// <summary>The vertices of a LineString feature (barriers).</summary>
    public static IReadOnlyList<LonLat> LineOf(Feature f) => Positions(f.Geometry.Coordinates);

    /// <summary>The outer ring of a Polygon feature (water/park).</summary>
    public static IReadOnlyList<LonLat> PolygonOuterOf(Feature f)
    {
        switch (f.Geometry.Coordinates)
        {
            case double[][][] rings when rings.Length > 0:
                return Positions(rings[0]);
            case JsonElement je when je.ValueKind == JsonValueKind.Array && je.GetArrayLength() > 0:
                return Positions(je[0]);
            default:
                return System.Array.Empty<LonLat>();
        }
    }

    private static IReadOnlyList<LonLat> Positions(object? coords) => coords switch
    {
        double[][] arr => arr.Where(p => p.Length >= 2).Select(p => new LonLat(p[0], p[1])).ToList(),
        JsonElement je when je.ValueKind == JsonValueKind.Array =>
            je.EnumerateArray().Select(p => new LonLat(p[0].GetDouble(), p[1].GetDouble())).ToList(),
        _ => System.Array.Empty<LonLat>(),
    };
}
