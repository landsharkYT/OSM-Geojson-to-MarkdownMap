using System.Collections.Generic;

namespace MarkdownMap.Generator;

/// <summary>A district anchor: a `place` feature's name + location.</summary>
public readonly struct Anchor
{
    public readonly string Name;
    public readonly LonLat Point;
    public Anchor(string name, LonLat point) { Name = name; Point = point; }
}

/// <summary>Voronoi district assignment: each point joins its nearest anchor (no radius cap).</summary>
public static class Districts
{
    public static string? Nearest(LonLat p, IReadOnlyList<Anchor> anchors)
    {
        if (anchors.Count == 0) return null;
        string? best = null;
        double bestD = double.MaxValue;
        foreach (var a in anchors)
        {
            double d = Geo.HaversineMeters(p, a.Point);
            if (d < bestD) { bestD = d; best = a.Name; }
        }
        return best;
    }
}
