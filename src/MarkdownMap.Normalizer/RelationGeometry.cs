using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;

namespace MarkdownMap.Normalizer;

/// <summary>
/// Builds the extent-only representative geometry for a multipolygon terrain relation
/// (ADR-0008): the convex hull of all its member coordinates, as a closed ring. Pure.
/// </summary>
public static class RelationGeometry
{
    /// <returns>A closed ring [[lon,lat],...] for the convex hull, or null if degenerate (&lt;3 distinct points / collinear).</returns>
    public static double[][]? ConvexHullRing(IReadOnlyList<(double lon, double lat)> coords)
    {
        var distinct = coords.Distinct().ToArray();
        if (distinct.Length < 3) return null;

        var mp = GeometryFactory.Default.CreateMultiPointFromCoords(
            distinct.Select(p => new Coordinate(p.lon, p.lat)).ToArray());
        var hull = mp.ConvexHull();
        if (hull is Polygon poly && !poly.IsEmpty)
            return poly.ExteriorRing.Coordinates
                .Select(c => new[] { Math.Round(c.X, 7), Math.Round(c.Y, 7) })
                .ToArray();
        return null; // collinear or fewer than 3 effective points → no area
    }
}
