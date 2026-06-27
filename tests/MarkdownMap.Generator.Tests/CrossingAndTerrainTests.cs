using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

public class CrossingAndTerrainTests
{
    [Fact]
    public void Segments_that_form_an_X_cross()
    {
        Assert.True(Geo.SegmentsCross(
            new LonLat(0, 0), new LonLat(2, 2), new LonLat(0, 2), new LonLat(2, 0)));
    }

    [Fact]
    public void Parallel_segments_do_not_cross()
    {
        Assert.False(Geo.SegmentsCross(
            new LonLat(0, 0), new LonLat(2, 0), new LonLat(0, 1), new LonLat(2, 1)));
    }

    [Fact]
    public void Touching_at_a_shared_endpoint_is_not_a_proper_cross()
    {
        Assert.False(Geo.SegmentsCross(
            new LonLat(0, 0), new LonLat(2, 2), new LonLat(2, 2), new LonLat(3, 1)));
    }

    // Map centred on (0,0), bounds 20×20.
    private static readonly double[] Bounds = { -10, -10, 10, 10 };

    [Fact]
    public void Small_western_area_is_positioned_west()
    {
        var box = new[]
        {
            new LonLat(-8, -1), new LonLat(-7, -1), new LonLat(-7, 1), new LonLat(-8, 1), new LonLat(-8, -1),
        };
        Assert.Equal("W", TerrainPosition.Describe(box, Bounds, linear: false));
    }

    [Fact]
    public void Large_area_spanning_an_axis_is_an_edge()
    {
        // Spans most of the north half → ">40% of an axis".
        var box = new[]
        {
            new LonLat(-9, 2), new LonLat(9, 2), new LonLat(9, 9), new LonLat(-9, 9), new LonLat(-9, 2),
        };
        Assert.EndsWith("edge", TerrainPosition.Describe(box, Bounds, linear: false));
    }

    [Fact]
    public void Linear_barrier_reports_its_run_direction()
    {
        var line = new[] { new LonLat(5, -8), new LonLat(5, 8) }; // vertical, east of centre
        Assert.Equal("runs N–S, E", TerrainPosition.Describe(line, Bounds, linear: true));
    }
}
