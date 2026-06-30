using System.IO;
using System.Text;
using System.Text.Json;
using MarkdownMap.Bridge;
using Xunit;

namespace MarkdownMap.Bridge.Tests;

public class MapBuilderTests
{
    private static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;

    private static string Rivertown =>
        File.ReadAllText(Path.Combine(System.AppContext.BaseDirectory, "rivertown.geojson"));

    // A tiny fictional OSM extract to exercise the FromOsm (Normalizer) path.
    private const string SyntheticOsm = """
        <?xml version="1.0" encoding="UTF-8"?>
        <osm version="0.6">
         <bounds minlat="45.5195" minlon="-120.5008" maxlat="45.5212" maxlon="-120.4990"/>
         <node id="1" lat="45.5210" lon="-120.5001"><tag k="amenity" v="cafe"/><tag k="name" v="Bluebird Coffee"/></node>
         <node id="2" lat="45.5205" lon="-120.5000"><tag k="amenity" v="bar"/><tag k="name" v="The Anchor Tavern"/></node>
         <node id="3" lat="45.5207" lon="-120.5000"><tag k="place" v="neighbourhood"/><tag k="name" v="Old Town"/></node>
        </osm>
        """;

    [Fact]
    public void FromGeoJson_returns_camelCase_MapModel_json()
    {
        var root = Root(MapBuilder.FromGeoJson(Rivertown));
        Assert.False(root.TryGetProperty("Features", out _));        // not PascalCase
        Assert.Equal(10, root.GetProperty("features").GetArrayLength());
        Assert.Equal(4, root.GetProperty("minors").GetArrayLength());
        Assert.Equal(2, root.GetProperty("districts").GetArrayLength());
        Assert.Equal(3, root.GetProperty("terrain").GetArrayLength());
        Assert.True(root.GetProperty("edges").GetArrayLength() > 0);
        Assert.Contains("MARKDOWNMAP", root.GetProperty("markdown").GetString());
    }

    [Fact]
    public void FromGeoJson_features_carry_token_and_coordinates()
    {
        var first = Root(MapBuilder.FromGeoJson(Rivertown)).GetProperty("features")[0];
        Assert.Equal("[01]", first.GetProperty("token").GetString());
        Assert.NotEqual(0, first.GetProperty("lat").GetDouble());
    }

    [Fact]
    public void FromOsm_runs_the_full_pipeline()
    {
        var root = Root(MapBuilder.FromOsm(Encoding.UTF8.GetBytes(SyntheticOsm)));
        Assert.True(root.GetProperty("features").GetArrayLength() >= 2);
        Assert.Contains("Old Town", root.GetProperty("markdown").GetString());
    }

    [Fact]
    public void Invalid_input_returns_error_json()
    {
        var root = Root(MapBuilder.FromGeoJson("not json at all"));
        Assert.True(root.TryGetProperty("error", out var err));
        Assert.False(string.IsNullOrEmpty(err.GetString()));
    }
}
