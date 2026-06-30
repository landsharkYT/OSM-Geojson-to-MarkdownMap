using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Linemerge;
using NetTopologySuite.Simplify;

namespace MarkdownMap.Normalizer;

/// <summary>
/// Assembles geometry for a terrain multipolygon relation (ADR-0014). Member ways (any order)
/// are stitched with NTS <see cref="LineMerger"/>: a merged line that closes becomes a real
/// <see cref="Assembled.Rings">ring</see> (filled); one that can't (bbox-clipped boundary)
/// becomes a <see cref="Assembled.Lines">shoreline line</see> — drawn as the water/park edge
/// rather than a false filled hull. Both are Douglas–Peucker simplified (~5 m).
/// </summary>
public static class RelationRings
{
    private const double SimplifyToleranceDeg = 0.000045; // ~5 m, matches way-polygons

    public readonly struct Assembled
    {
        public readonly List<double[][]> Rings; // closed → filled Polygon parts
        public readonly List<double[][]> Lines; // open → shoreline LineString parts (clipped)
        public Assembled(List<double[][]> rings, List<double[][]> lines) { Rings = rings; Lines = lines; }
    }

    public static Assembled Assemble(IEnumerable<IReadOnlyList<(double lon, double lat)>> outerWays)
    {
        var gf = GeometryFactory.Default;
        var merger = new LineMerger();
        bool any = false;
        foreach (var way in outerWays)
        {
            if (way.Count < 2) continue;
            merger.Add(gf.CreateLineString(way.Select(p => new Coordinate(p.lon, p.lat)).ToArray()));
            any = true;
        }

        var rings = new List<double[][]>();
        var lines = new List<double[][]>();
        if (!any) return new Assembled(rings, lines);

        foreach (var geom in merger.GetMergedLineStrings())
        {
            if (geom is not LineString line) continue;
            var coords = line.Coordinates;
            if (coords.Length < 2) continue;

            bool closed = coords.Length >= 4 && coords[0].Equals2D(coords[coords.Length - 1]);
            if (closed)
            {
                Polygon poly;
                try { poly = gf.CreatePolygon(gf.CreateLinearRing(coords)); }
                catch { continue; }
                if (poly.IsEmpty || poly.Area <= 0) continue;
                var simp = DouglasPeuckerSimplifier.Simplify(poly, SimplifyToleranceDeg);
                var outer = simp is Polygon p && !p.IsEmpty ? p.ExteriorRing.Coordinates : poly.ExteriorRing.Coordinates;
                if (outer.Length >= 4) rings.Add(Round(outer));
            }
            else
            {
                // Clipped boundary → keep the shoreline as a (simplified) line, not a filled hull.
                var simp = DouglasPeuckerSimplifier.Simplify(line, SimplifyToleranceDeg);
                var c = simp.Coordinates.Length >= 2 ? simp.Coordinates : coords;
                lines.Add(Round(c));
            }
        }
        return new Assembled(rings, lines);
    }

    private static double[][] Round(Coordinate[] cs) =>
        cs.Select(c => new[] { Math.Round(c.X, 7), Math.Round(c.Y, 7) }).ToArray();
}
