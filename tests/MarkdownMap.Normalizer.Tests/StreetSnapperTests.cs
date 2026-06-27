using System.Collections.Generic;
using MarkdownMap.Normalizer;
using Xunit;

namespace MarkdownMap.Normalizer.Tests;

public class StreetSnapperTests
{
    // A short E–W road segment near (45.52, -120.50).
    private static readonly List<Road> Roads = new()
    {
        new Road("Main Street", new[] { (-120.5010, 45.5200), (-120.4990, 45.5200) }),
    };

    private static Dictionary<string, string> Tags(params string[] kv)
    {
        var d = new Dictionary<string, string>();
        for (int i = 0; i < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
        return d;
    }

    [Fact]
    public void Addr_street_wins_and_is_not_approximate()
    {
        var (street, approx) = StreetSnapper.Assign(
            Tags("addr:street", "Oak Avenue"), (-120.5000, 45.5200), Roads, 30);
        Assert.Equal("Oak Avenue", street);
        Assert.False(approx);
    }

    [Fact]
    public void Snaps_to_nearby_road_as_approximate()
    {
        // ~11 m north of Main Street (0.0001 deg lat ≈ 11 m).
        var (street, approx) = StreetSnapper.Assign(
            Tags(), (-120.5000, 45.5201), Roads, 30);
        Assert.Equal("Main Street", street);
        Assert.True(approx);
    }

    [Fact]
    public void No_street_when_all_roads_beyond_radius()
    {
        // ~330 m north — well beyond the 30 m snap radius.
        var (street, _) = StreetSnapper.Assign(
            Tags(), (-120.5000, 45.5230), Roads, 30);
        Assert.Null(street);
    }

    [Fact]
    public void Point_to_segment_distance_is_perpendicular()
    {
        // Point directly above the midpoint of the segment.
        double d = StreetSnapper.PointToSegmentMeters(
            (-120.5000, 45.5201), (-120.5010, 45.5200), (-120.4990, 45.5200));
        Assert.InRange(d, 9, 13); // ~11 m
    }
}
