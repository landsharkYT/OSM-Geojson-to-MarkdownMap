using System.Collections.Generic;
using System.Linq;
using MarkdownMap.Contract;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

/// <summary>
/// ADR-0017: the markdown terrain block lists only **orienting-scale** parks/water (area ≥ the
/// threshold); pocket parks are dropped from the text but kept in the model + Explorer. The
/// redundant `green space` / `open water` note is dropped; the barrier note stays.
/// </summary>
public class TerrainFilterTests
{
    private static Feature Poi(string name, double lon, double lat) => new Feature
    {
        Properties = new FeatureProperties { Kind = "poi", Name = name, Category = "civic.hall", Importance = 50, Tier = "landmark", OsmId = "n" + name },
        Geometry = new Geometry { Type = "Point", Coordinates = new[] { lon, lat } },
    };

    private static Feature Park(string name, double lon, double lat, double dLon, double dLat) => new Feature
    {
        Properties = new FeatureProperties { Kind = "park", Name = name },
        Geometry = new Geometry
        {
            Type = "Polygon",
            Coordinates = new[] { new[]
            {
                new[] { lon, lat }, new[] { lon + dLon, lat },
                new[] { lon + dLon, lat + dLat }, new[] { lon, lat + dLat }, new[] { lon, lat },
            } },
        },
    };

    private static FeatureCollection Scene() => new FeatureCollection
    {
        Properties = new CollectionProperties { Title = "Parktown", Bounds = new[] { -0.01, -0.01, 0.02, 0.02 } },
        Features = new List<Feature>
        {
            Poi("Town Hall", 0.0, 0.0),
            Poi("Library", 0.001, 0.0),
            Park("Great Meadow", 0.005, 0.005, 0.001, 0.002), // ~0.001°×0.002° ≈ 17,000 m² → kept
            Park("Corner Pocket", 0.008, 0.008, 0.0002, 0.0002), // ~340 m² → dropped from text
        },
    };

    [Fact]
    public void Small_park_is_dropped_from_markdown_but_kept_in_the_model()
    {
        var gen = new MapGenerator();
        var fc = Scene();
        var md = gen.Generate(fc);
        var model = gen.BuildModel(fc);

        Assert.Contains("Great Meadow (park)", md);        // orienting-scale → listed
        Assert.DoesNotContain("Corner Pocket", md);        // pocket park → dropped from text
        Assert.Contains("Great Meadow", model.Terrain.Select(t => t.Name)); // geometry stays in model
        Assert.Contains("Corner Pocket", model.Terrain.Select(t => t.Name));
    }

    [Fact]
    public void Park_line_drops_the_redundant_note()
    {
        var md = new MapGenerator().Generate(Scene());
        // "Great Meadow (park) · <position>" with no trailing "· green space".
        Assert.Matches(@"- Great Meadow \(park\) · [^\n·]+\n", md);
        Assert.DoesNotContain("green space", md);
    }
}
