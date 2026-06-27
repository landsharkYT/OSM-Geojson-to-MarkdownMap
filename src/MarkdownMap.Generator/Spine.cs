using System;
using System.Collections.Generic;
using System.Linq;

namespace MarkdownMap.Generator;

/// <summary>
/// Orders a district's promoted features along their principal axis (PCA) and names the
/// axis direction (N–S / NE–SW / E–W / NW–SE). Geometry-only, so no road features needed.
/// </summary>
public static class Spine
{
    /// <returns>(direction label, member indices ordered along the axis).</returns>
    public static (string direction, IReadOnlyList<int> order) Compute(IReadOnlyList<LonLat> pts)
    {
        int n = pts.Count;
        if (n <= 1) return ("N–S", Enumerable.Range(0, n).ToList());

        double clon = pts.Average(p => p.Lon), clat = pts.Average(p => p.Lat);
        double mPerLon = 111_320.0 * Math.Cos(clat * Math.PI / 180.0);
        const double mPerLat = 110_540.0;

        // Local east/north metres relative to centroid.
        var local = pts.Select(p => (x: (p.Lon - clon) * mPerLon, y: (p.Lat - clat) * mPerLat)).ToArray();

        double sxx = local.Sum(t => t.x * t.x);
        double syy = local.Sum(t => t.y * t.y);
        double sxy = local.Sum(t => t.x * t.y);

        // Principal axis angle (PCA): direction of maximum variance.
        double theta = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        double ux = Math.Cos(theta), uy = Math.Sin(theta); // (east, north)

        // Orient consistently: prefer pointing north; tie → east.
        if (uy < -1e-9 || (Math.Abs(uy) <= 1e-9 && ux < 0)) { ux = -ux; uy = -uy; }

        var order = Enumerable.Range(0, n)
            .OrderByDescending(i => local[i].x * ux + local[i].y * uy) // leading end first
            .ThenBy(i => i)
            .ToList();

        return (Direction(ux, uy), order);
    }

    private static string Direction(double ux, double uy)
    {
        double a = Deg(Math.Atan2(ux, uy)); // 0 = north, +90 = east
        double m = Math.Abs(a);
        if (m < 22.5) return "N–S";
        if (m >= 67.5) return "E–W";
        return a > 0 ? "NE–SW" : "NW–SE";
    }

    private static double Deg(double r) => r * 180.0 / Math.PI;
}
