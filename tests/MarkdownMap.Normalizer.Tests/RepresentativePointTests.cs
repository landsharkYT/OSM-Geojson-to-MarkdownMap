using MarkdownMap.Normalizer;
using Xunit;

namespace MarkdownMap.Normalizer.Tests;

public class RepresentativePointTests
{
    [Fact]
    public void Empty_returns_false()
    {
        Assert.False(RepresentativePoint.TryCompute(System.Array.Empty<(double, double)>(), out _, out _));
    }

    [Fact]
    public void Closed_unit_square_centroid_is_center()
    {
        var square = new (double lon, double lat)[]
        {
            (0, 0), (2, 0), (2, 2), (0, 2), (0, 0), // closed ring
        };
        Assert.True(RepresentativePoint.TryCompute(square, out var lon, out var lat));
        Assert.Equal(1.0, lon, 6);
        Assert.Equal(1.0, lat, 6);
    }

    [Fact]
    public void Open_ring_falls_back_to_vertex_average()
    {
        var line = new (double lon, double lat)[] { (0, 0), (10, 0), (20, 0) };
        Assert.True(RepresentativePoint.TryCompute(line, out var lon, out var lat));
        Assert.Equal(10.0, lon, 6);
        Assert.Equal(0.0, lat, 6);
    }

    [Fact]
    public void Single_point_returns_itself()
    {
        Assert.True(RepresentativePoint.TryCompute(new[] { (5.0, 7.0) }, out var lon, out var lat));
        Assert.Equal(5.0, lon, 6);
        Assert.Equal(7.0, lat, 6);
    }
}
