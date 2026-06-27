using System.Collections.Generic;
using System.Linq;

namespace MarkdownMap.Generator;

/// <summary>
/// Describes where a terrain/barrier feature sits relative to the map (schema §7):
/// octant of its centroid vs the map centre; "&lt;dir&gt; edge" when it spans &gt;40% of an
/// axis; linear barriers as "runs &lt;axis&gt;[, &lt;side&gt;]". Computed in the Generator.
/// </summary>
public static class TerrainPosition
{
    public static string Describe(IReadOnlyList<LonLat> coords, double[] mapBounds, bool linear)
    {
        if (coords.Count == 0 || mapBounds.Length != 4) return "central";

        double mapW = mapBounds[2] - mapBounds[0], mapH = mapBounds[3] - mapBounds[1];
        var center = new LonLat((mapBounds[0] + mapBounds[2]) / 2, (mapBounds[1] + mapBounds[3]) / 2);
        var centroid = new LonLat(coords.Average(c => c.Lon), coords.Average(c => c.Lat));

        // octant of the feature centre relative to the map centre
        double dx = centroid.Lon - center.Lon, dy = centroid.Lat - center.Lat;
        bool central = (mapW > 0 && System.Math.Abs(dx) < mapW * 0.12)
                     && (mapH > 0 && System.Math.Abs(dy) < mapH * 0.12);
        string oct = central ? "central" : Geo.EightWind(center, centroid);

        if (linear)
        {
            var (dir, _) = Spine.Compute(coords);
            return central ? $"runs {dir}" : $"runs {dir}, {oct}";
        }

        double spanLon = mapW > 0 ? (coords.Max(c => c.Lon) - coords.Min(c => c.Lon)) / mapW : 0;
        double spanLat = mapH > 0 ? (coords.Max(c => c.Lat) - coords.Min(c => c.Lat)) / mapH : 0;
        if ((spanLon > 0.4 || spanLat > 0.4) && !central) return $"{oct} edge";
        return oct;
    }
}
