using System.Collections.Generic;
using MarkdownMap.Normalizer;
using Xunit;

namespace MarkdownMap.Normalizer.Tests;

public class TerrainClassifierTests
{
    private static Dictionary<string, string> Tags(params string[] kv)
    {
        var d = new Dictionary<string, string>();
        for (int i = 0; i < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
        return d;
    }

    [Theory]
    [InlineData("highway", "motorway", "motorway")]
    [InlineData("highway", "trunk", "motorway")]
    [InlineData("railway", "rail", "rail")]
    [InlineData("waterway", "canal", "water")]
    public void Barrier_classes(string k, string v, string cls)
    {
        var (label, c) = TerrainClassifier.Barrier(Tags(k, v, "name", "X"));
        Assert.Equal("X", label);
        Assert.Equal(cls, c);
    }

    [Fact]
    public void Barrier_label_falls_back_to_ref()
    {
        var (label, _) = TerrainClassifier.Barrier(Tags("highway", "motorway", "ref", "Route 9"));
        Assert.Equal("Route 9", label);
    }

    [Fact]
    public void Barrier_without_name_or_ref_is_skipped()
    {
        Assert.Null(TerrainClassifier.Barrier(Tags("highway", "motorway")).label);
    }

    [Fact]
    public void Non_barrier_highway_is_not_a_barrier()
    {
        Assert.Null(TerrainClassifier.Barrier(Tags("highway", "residential", "name", "Oak St")).cls);
    }

    [Theory]
    [InlineData("natural", "water", "water")]
    [InlineData("leisure", "park", "park")]
    [InlineData("landuse", "recreation_ground", "park")]
    public void Area_kinds(string k, string v, string kind)
    {
        var (got, name) = TerrainClassifier.Area(Tags(k, v, "name", "Greenway"));
        Assert.Equal(kind, got);
        Assert.Equal("Greenway", name);
    }

    [Fact]
    public void Unnamed_area_is_skipped()
    {
        Assert.Null(TerrainClassifier.Area(Tags("natural", "water")).kind);
    }
}
