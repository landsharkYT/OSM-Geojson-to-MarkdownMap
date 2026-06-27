using System;
using System.Collections.Generic;

namespace MarkdownMap.Normalizer;

/// <summary>A named road as a polyline of (lon,lat) vertices.</summary>
public sealed class Road
{
    public string Name { get; }
    public IReadOnlyList<(double lon, double lat)> Points { get; }
    public Road(string name, IReadOnlyList<(double lon, double lat)> points) { Name = name; Points = points; }
}

/// <summary>
/// Assigns the street a POI sits on (schema §6): <c>addr:street</c> if present, else the
/// name of the nearest named road within a snap radius (flagged approximate). Pure.
/// </summary>
public static class StreetSnapper
{
    /// <returns>(street, approx). street is null when nothing is close enough.</returns>
    public static (string? street, bool approx) Assign(
        IReadOnlyDictionary<string, string> tags, (double lon, double lat) point,
        IReadOnlyList<Road> roads, double maxMeters)
    {
        if (tags.TryGetValue("addr:street", out var addr) && !string.IsNullOrWhiteSpace(addr))
            return (addr, false);

        double best = double.MaxValue;
        string? bestName = null;
        foreach (var road in roads)
        {
            var pts = road.Points;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                double d = PointToSegmentMeters(point, pts[i], pts[i + 1]);
                if (d < best) { best = d; bestName = road.Name; }
            }
        }
        return best <= maxMeters ? (bestName, true) : (null, false);
    }

    /// <summary>Distance (m) from point p to segment a–b, via a local equirectangular metric.</summary>
    public static double PointToSegmentMeters(
        (double lon, double lat) p, (double lon, double lat) a, (double lon, double lat) b)
    {
        double mPerLon = 111_320.0 * Math.Cos(p.lat * Math.PI / 180.0);
        const double mPerLat = 110_540.0;
        double px = (p.lon - a.lon) * mPerLon, py = (p.lat - a.lat) * mPerLat;
        double bx = (b.lon - a.lon) * mPerLon, by = (b.lat - a.lat) * mPerLat;
        double len2 = bx * bx + by * by;
        double t = len2 <= 0 ? 0 : Math.Max(0, Math.Min(1, (px * bx + py * by) / len2));
        double cx = t * bx, cy = t * by;
        return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }
}
