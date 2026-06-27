using System;
using System.Collections.Generic;

namespace MarkdownMap.Normalizer;

/// <summary>
/// Classifies OSM ways into barriers and terrain areas (impl-step-4, split-by-geometry):
/// roads/rail/linear-waterways → barrier; named area water/park → terrain. Pure.
/// </summary>
public static class TerrainClassifier
{
    private static readonly HashSet<string> MotorwayHighway = new(StringComparer.Ordinal)
        { "motorway", "motorway_link", "trunk" };
    private static readonly HashSet<string> RailRailway = new(StringComparer.Ordinal)
        { "rail", "light_rail" };
    private static readonly HashSet<string> LinearWaterway = new(StringComparer.Ordinal)
        { "river", "canal" };

    /// <returns>(label, class) for a barrier way; (null, null) otherwise. label = name ?? ref.</returns>
    public static (string? label, string? cls) Barrier(IReadOnlyDictionary<string, string> tags)
    {
        string? cls = null;
        if (tags.TryGetValue("highway", out var hw) && MotorwayHighway.Contains(hw)) cls = "motorway";
        else if (tags.TryGetValue("railway", out var rw) && RailRailway.Contains(rw)) cls = "rail";
        else if (tags.TryGetValue("waterway", out var ww) && LinearWaterway.Contains(ww)) cls = "water";
        if (cls is null) return (null, null);

        string? label = tags.TryGetValue("name", out var n) && !string.IsNullOrWhiteSpace(n) ? n
            : tags.TryGetValue("ref", out var r) && !string.IsNullOrWhiteSpace(r) ? r
            : null;
        return label is null ? (null, null) : (label, cls);
    }

    /// <returns>(kind, name) for a named terrain area (water|park); (null, null) otherwise.</returns>
    public static (string? kind, string? name) Area(IReadOnlyDictionary<string, string> tags)
    {
        if (!tags.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            return (null, null);
        if (tags.TryGetValue("natural", out var nat) && nat == "water")
            return ("water", name);
        if ((tags.TryGetValue("leisure", out var l) && l == "park")
            || (tags.TryGetValue("landuse", out var lu) && lu == "recreation_ground"))
            return ("park", name);
        return (null, null);
    }
}
