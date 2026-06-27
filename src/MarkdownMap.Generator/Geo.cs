using System;

namespace MarkdownMap.Generator;

/// <summary>A lon/lat point. X = longitude, Y = latitude (GeoJSON order).</summary>
public readonly struct LonLat
{
    public readonly double Lon;
    public readonly double Lat;
    public LonLat(double lon, double lat) { Lon = lon; Lat = lat; }
}

/// <summary>
/// Self-contained spherical geometry (no NetTopologySuite, so the Generator stays a lean,
/// portable netstandard2.0 library). Distances in metres; bearings as 8-wind compass.
/// </summary>
public static class Geo
{
    private const double EarthRadiusM = 6_371_000.0;
    private static readonly string[] Winds = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    /// <summary>Great-circle (haversine) distance in metres.</summary>
    public static double HaversineMeters(LonLat a, LonLat b)
    {
        double la1 = Rad(a.Lat), la2 = Rad(b.Lat);
        double dLa = Rad(b.Lat - a.Lat), dLo = Rad(b.Lon - a.Lon);
        double h = Math.Sin(dLa / 2) * Math.Sin(dLa / 2)
                 + Math.Cos(la1) * Math.Cos(la2) * Math.Sin(dLo / 2) * Math.Sin(dLo / 2);
        return 2 * EarthRadiusM * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
    }

    /// <summary>Initial-bearing from <paramref name="from"/> to <paramref name="to"/>, snapped to 8-wind.</summary>
    public static string EightWind(LonLat from, LonLat to)
    {
        double la1 = Rad(from.Lat), la2 = Rad(to.Lat), dLo = Rad(to.Lon - from.Lon);
        double y = Math.Sin(dLo) * Math.Cos(la2);
        double x = Math.Cos(la1) * Math.Sin(la2) - Math.Sin(la1) * Math.Cos(la2) * Math.Cos(dLo);
        double deg = (Deg(Math.Atan2(y, x)) + 360.0) % 360.0;
        int idx = (int)Math.Round(deg / 45.0) % 8;
        return Winds[idx];
    }

    /// <summary>Plain-language size of a gap (schema bucket cutoffs).</summary>
    public static string Bucket(double meters, BucketCutoffs c) =>
        meters < c.Adjacent ? "adjacent"
        : meters < c.Near ? "near"
        : meters < c.ShortWalk ? "short walk"
        : "far";

    /// <summary>Round metres to the nearest step (default 5 m) for display.</summary>
    public static int RoundMeters(double meters, int step = 5) =>
        (int)(Math.Round(meters / step) * step);

    /// <summary>True if open segments a–b and c–d properly cross (planar lon/lat; collinear touches ignored).</summary>
    public static bool SegmentsCross(LonLat a, LonLat b, LonLat c, LonLat d)
    {
        double d1 = Cross(c, d, a), d2 = Cross(c, d, b);
        double d3 = Cross(a, b, c), d4 = Cross(a, b, d);
        return ((d1 > 0) != (d2 > 0)) && ((d3 > 0) != (d4 > 0));
    }

    private static double Cross(LonLat o, LonLat p, LonLat q) =>
        (p.Lon - o.Lon) * (q.Lat - o.Lat) - (p.Lat - o.Lat) * (q.Lon - o.Lon);

    private static double Rad(double d) => d * Math.PI / 180.0;
    private static double Deg(double r) => r * 180.0 / Math.PI;
}
