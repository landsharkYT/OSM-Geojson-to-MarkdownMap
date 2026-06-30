using System.Linq;
using MarkdownMap.Contract;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

/// <summary>
/// ADR-0015: a straight-line link through water is flagged `[separated by water]`, and a feature
/// reachable only across water `stands apart`. Distance stays crow-flies; we never route.
/// </summary>
public class WaterSeparationTests
{
    private static Feature Poi(string name, double lon, double lat, int importance) => new Feature
    {
        Properties = new FeatureProperties
        {
            Kind = "poi", Name = name, Category = "civic.hall",
            Importance = importance, Tier = "landmark", OsmId = "n" + name.GetHashCode(),
        },
        Geometry = new Geometry { Type = "Point", Coordinates = new[] { lon, lat } },
    };

    // A lone west feature [Lighthouse] and an east cluster, with a north–south water band between.
    private static FeatureCollection Scene(Feature divider) => new FeatureCollection
    {
        Properties = new CollectionProperties { Title = "Bay", Bounds = new[] { -0.01, -0.02, 0.01, 0.02 } },
        Features = new System.Collections.Generic.List<Feature>
        {
            Poi("Lighthouse", -0.002, 0.0, 50),
            Poi("Cafe", 0.0020, 0.0000, 90),
            Poi("Bakery", 0.0021, 0.0003, 80),
            Poi("Tavern", 0.0022, -0.0003, 70),
            divider,
        },
    };

    private static Feature WaterBand() => new Feature
    {
        Properties = new FeatureProperties { Kind = "water", Name = "The Channel" },
        Geometry = new Geometry
        {
            Type = "Polygon",
            Coordinates = new[] { new[]
            {
                new[] { -0.001, -0.01 }, new[] { 0.001, -0.01 },
                new[] { 0.001, 0.01 }, new[] { -0.001, 0.01 }, new[] { -0.001, -0.01 },
            } },
        },
    };

    private static Feature RiverLine() => new Feature
    {
        Properties = new FeatureProperties { Kind = "barrier", Name = "Mill Race", BarrierClass = "water" },
        Geometry = new Geometry
        {
            Type = "LineString",
            Coordinates = new[] { new[] { 0.0, -0.01 }, new[] { 0.0, 0.01 } },
        },
    };

    [Fact]
    public void Link_through_a_water_area_is_separated_but_links_on_one_side_are_not()
    {
        var m = new MapGenerator().BuildModel(Scene(WaterBand()));
        string Tok(string n) => m.Features.Single(f => f.Name == n).Token;

        Assert.True(m.Edges.Single(e => e.FromToken == Tok("Lighthouse") && e.ToToken == Tok("Cafe")).SeparatedByWater);
        Assert.False(m.Edges.Single(e => e.FromToken == Tok("Cafe") && e.ToToken == Tok("Bakery")).SeparatedByWater);
    }

    [Fact]
    public void A_feature_reachable_only_across_water_stands_apart()
    {
        var md = new MapGenerator().Generate(Scene(WaterBand()));
        Assert.Contains("[separated by water]", md);
        Assert.Matches(@"\[\d+\] Lighthouse[^\n]*· stands apart — reached only across water", md);
    }

    [Fact]
    public void A_feature_with_a_dry_link_does_not_stand_apart()
    {
        // The east cluster each have at least one same-side (dry) neighbour.
        var md = new MapGenerator().Generate(Scene(WaterBand()));
        Assert.DoesNotMatch(@"\[\d+\] Cafe[^\n]*stands apart", md);
    }

    [Fact]
    public void A_linear_river_barrier_also_separates()
    {
        var m = new MapGenerator().BuildModel(Scene(RiverLine()));
        string Tok(string n) => m.Features.Single(f => f.Name == n).Token;
        Assert.True(m.Edges.Single(e => e.FromToken == Tok("Lighthouse") && e.ToToken == Tok("Cafe")).SeparatedByWater);
        // A river/canal is water, not a named road crossing — so no `[crosses Mill Race]`.
        Assert.DoesNotContain("[crosses Mill Race]", m.Markdown);
    }

    [Fact]
    public void Point_in_polygon_and_segment_crossing_hold()
    {
        var ring = new[]
        {
            new LonLat(-1, -1), new LonLat(1, -1), new LonLat(1, 1), new LonLat(-1, 1),
        };
        Assert.True(Geo.PointInPolygon(new LonLat(0, 0), ring));
        Assert.False(Geo.PointInPolygon(new LonLat(2, 0), ring));
        Assert.True(Geo.SegmentCrossesPolygon(new LonLat(-2, 0), new LonLat(2, 0), ring)); // passes through
        Assert.False(Geo.SegmentCrossesPolygon(new LonLat(-2, 2), new LonLat(2, 2), ring)); // clears it to the north
    }
}
