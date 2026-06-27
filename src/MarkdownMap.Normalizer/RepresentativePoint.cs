using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;

namespace MarkdownMap.Normalizer;

/// <summary>
/// Reduces a way's vertices to a single representative point (schema §6): polygon
/// centroid for a closed ring, vertex average otherwise. Pure and testable.
/// </summary>
public static class RepresentativePoint
{
    public static bool TryCompute(IReadOnlyList<(double lon, double lat)> pts, out double lon, out double lat)
    {
        lon = lat = 0;
        if (pts.Count == 0) return false;

        bool closed = pts.Count >= 4 && pts[0] == pts[^1];
        if (closed)
        {
            try
            {
                var ring = GeometryFactory.Default.CreateLinearRing(
                    pts.Select(p => new Coordinate(p.lon, p.lat)).ToArray());
                var centroid = GeometryFactory.Default.CreatePolygon(ring).Centroid;
                if (!double.IsNaN(centroid.X) && !double.IsNaN(centroid.Y))
                {
                    lon = centroid.X; lat = centroid.Y;
                    return true;
                }
            }
            catch { /* fall through to average */ }
        }

        lon = pts.Average(p => p.lon);
        lat = pts.Average(p => p.lat);
        return true;
    }
}
