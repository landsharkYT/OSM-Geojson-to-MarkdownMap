using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

public class GeoTests
{
    [Fact]
    public void Haversine_one_degree_latitude_is_about_111km()
    {
        var d = Geo.HaversineMeters(new LonLat(0, 0), new LonLat(0, 1));
        Assert.InRange(d, 111_000, 111_400);
    }

    [Fact]
    public void Haversine_is_symmetric_and_zero_for_same_point()
    {
        var a = new LonLat(-120.5, 45.52);
        var b = new LonLat(-120.499, 45.521);
        Assert.Equal(0, Geo.HaversineMeters(a, a), 6);
        Assert.Equal(Geo.HaversineMeters(a, b), Geo.HaversineMeters(b, a), 6);
    }

    [Theory]
    [InlineData(0, 1, "N")]
    [InlineData(1, 0, "E")]
    [InlineData(0, -1, "S")]
    [InlineData(-1, 0, "W")]
    [InlineData(1, 1, "NE")]
    [InlineData(-1, -1, "SW")]
    public void EightWind_points_in_the_right_compass_octant(double dLon, double dLat, string expected)
    {
        var from = new LonLat(-120.5, 45.52);
        // small offsets near the equator-ish; sign drives the octant
        var to = new LonLat(from.Lon + dLon * 0.001, from.Lat + dLat * 0.001);
        Assert.Equal(expected, Geo.EightWind(from, to));
    }

    [Theory]
    [InlineData(10, "adjacent")]
    [InlineData(24.9, "adjacent")]
    [InlineData(25, "near")]
    [InlineData(149, "near")]
    [InlineData(150, "short walk")]
    [InlineData(499, "short walk")]
    [InlineData(500, "far")]
    [InlineData(5000, "far")]
    public void Bucket_cutoffs(double meters, string expected)
    {
        Assert.Equal(expected, Geo.Bucket(meters, BucketCutoffs.Default));
    }
}
