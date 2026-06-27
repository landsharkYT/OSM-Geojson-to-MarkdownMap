using System.Linq;
using MarkdownMap.Normalizer;
using Xunit;

namespace MarkdownMap.Normalizer.Tests;

public class RelationGeometryTests
{
    [Fact]
    public void Hull_of_a_square_plus_interior_point_is_the_square()
    {
        var ring = RelationGeometry.ConvexHullRing(new (double, double)[]
        {
            (0, 0), (2, 0), (2, 2), (0, 2), (1, 1), // interior point dropped by the hull
        });
        Assert.NotNull(ring);
        Assert.Equal(ring![0], ring[^1]); // closed
        Assert.Equal(0, ring.Min(p => p[0]));
        Assert.Equal(2, ring.Max(p => p[0]));
        Assert.Equal(0, ring.Min(p => p[1]));
        Assert.Equal(2, ring.Max(p => p[1]));
        Assert.Equal(5, ring.Length); // 4 corners + closing vertex
    }

    [Fact]
    public void Collinear_points_have_no_area()
    {
        Assert.Null(RelationGeometry.ConvexHullRing(new (double, double)[] { (0, 0), (1, 1), (2, 2) }));
    }

    [Fact]
    public void Fewer_than_three_distinct_points_is_null()
    {
        Assert.Null(RelationGeometry.ConvexHullRing(new (double, double)[] { (0, 0), (0, 0), (1, 1) }));
    }
}
