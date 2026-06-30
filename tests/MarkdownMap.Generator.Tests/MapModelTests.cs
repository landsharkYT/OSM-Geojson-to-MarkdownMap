using System.IO;
using System.Linq;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

public class MapModelTests
{
    private static MapModel Model()
    {
        var path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "fixtures", "rivertown.geojson");
        return new MapGenerator().BuildModel(GeoJsonReader.Read(File.ReadAllText(path)));
    }

    [Fact]
    public void Markdown_field_equals_the_golden()
    {
        var golden = File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "golden", "rivertown.expected.md"));
        Assert.Equal(golden.Replace("\r\n", "\n").TrimEnd('\n'), Model().Markdown.Replace("\r\n", "\n").TrimEnd('\n'));
    }

    [Fact]
    public void Promoted_features_and_minors_are_separated()
    {
        var m = Model();
        Assert.Equal(10, m.Features.Count);                 // landmark + destination tiers
        Assert.Equal(4, m.Minors.Count);                    // minor tier, clustered
        Assert.Equal("[01]", m.Features[0].Token);
        Assert.Equal("Founders Mural", m.Features[0].Name);
        Assert.All(m.Features, f => Assert.NotEqual(0, f.Lat)); // carries coordinates
        Assert.Contains(m.Minors, x => x.Name == "Maple Apartments");
    }

    [Fact]
    public void Districts_carry_spine_counts_and_anchor()
    {
        var m = Model();
        Assert.Equal(2, m.Districts.Count);
        Assert.All(m.Districts, d =>
        {
            Assert.NotEmpty(d.SpineTokens);
            Assert.Equal(5, d.PromotedCount);
            Assert.Equal(2, d.ClusteredCount);
            Assert.NotEqual(0, d.AnchorLat);
        });
    }

    [Fact]
    public void Edges_include_a_barrier_crossing()
    {
        var m = Model();
        Assert.NotEmpty(m.Edges);
        Assert.Contains(m.Edges, e => e.Crosses == "Route 9");
        Assert.All(m.Edges, e => Assert.Matches(@"^\[\d+\]$", e.ToToken));
    }

    [Fact]
    public void Terrain_carries_kind_position_and_drawable_geometry()
    {
        var m = Model();
        Assert.Equal(3, m.Terrain.Count); // Mill Lake, Riverside Park, Route 9
        var lake = m.Terrain.Single(t => t.Name == "Mill Lake");
        Assert.Equal("water", lake.Kind);
        Assert.Equal("Polygon", lake.GeometryType);
        Assert.NotEmpty(lake.Parts);
        Assert.All(lake.Parts.First(), p => Assert.Equal(2, p.Length)); // [lon,lat]
        Assert.Equal("barrier:motorway", m.Terrain.Single(t => t.Name == "Route 9").KindLabel);
    }
}
