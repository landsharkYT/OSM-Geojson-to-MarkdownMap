using System.Collections.Generic;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

public class SpineAndDistrictsTests
{
    [Fact]
    public void Spine_detects_a_north_south_axis()
    {
        var pts = new[]
        {
            new LonLat(-120.50, 45.520),
            new LonLat(-120.50, 45.521),
            new LonLat(-120.50, 45.522),
        };
        var (dir, order) = Spine.Compute(pts);
        Assert.Equal("N–S", dir);
        Assert.Equal(3, order.Count);
        Assert.Equal(2, order[0]); // northernmost (highest lat) listed first
    }

    [Fact]
    public void Spine_detects_an_east_west_axis()
    {
        var pts = new[]
        {
            new LonLat(-120.500, 45.52),
            new LonLat(-120.501, 45.52),
            new LonLat(-120.502, 45.52),
        };
        var (dir, _) = Spine.Compute(pts);
        Assert.Equal("E–W", dir);
    }

    [Fact]
    public void Districts_assigns_to_nearest_anchor()
    {
        var anchors = new List<Anchor>
        {
            new("North", new LonLat(-120.50, 45.530)),
            new("South", new LonLat(-120.50, 45.510)),
        };
        Assert.Equal("North", Districts.Nearest(new LonLat(-120.50, 45.528), anchors));
        Assert.Equal("South", Districts.Nearest(new LonLat(-120.50, 45.512), anchors));
    }

    [Fact]
    public void Districts_returns_null_when_no_anchors()
    {
        Assert.Null(Districts.Nearest(new LonLat(0, 0), new List<Anchor>()));
    }
}
