using System.Collections.Generic;
using System.Linq;
using MarkdownMap.Contract;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

/// <summary>Unnamed handling (ADR-0012): category fallback label + tiered promotion.</summary>
public class UnnamedRenderTests
{
    private static Feature Poi(string id, string? name, string cat, int imp, string tier, double lon, double lat) =>
        new Feature
        {
            Properties = new FeatureProperties
            { Kind = "poi", Name = name, OsmId = id, Category = cat, Importance = imp, Tier = tier },
            Geometry = new Geometry { Type = "Point", Coordinates = new[] { lon, lat } },
        };

    private static Feature Place(string id, string name, double lon, double lat) =>
        new Feature
        {
            Properties = new FeatureProperties { Kind = "place", Name = name, OsmId = id },
            Geometry = new Geometry { Type = "Point", Coordinates = new[] { lon, lat } },
        };

    // A district anchor (so promotion is district-aware), a named landmark, an unnamed landmark,
    // and an unnamed destination (should fall below the promotion bar).
    private static FeatureCollection Build() => new FeatureCollection
    {
        Properties = new CollectionProperties
        { SchemaVersion = 1, Title = "Testville", Bounds = new[] { 10.0, 50.0, 10.01, 50.01 } },
        Features = new List<Feature>
        {
            Place("n1", "Downtown", 10.005, 50.005),
            Poi("n2", "Grand Cathedral", "landmark.place_of_worship", 95, "landmark", 10.004, 50.004),
            Poi("n3", null, "landmark.place_of_worship", 85, "landmark", 10.006, 50.006),
            Poi("n4", null, "food.cafe", 55, "destination", 10.005, 50.003),
        },
    };

    [Fact]
    public void Unnamed_landmark_promotes_with_lowercase_category_label_and_no_redundant_parenthetical()
    {
        var model = new MapGenerator().BuildModel(Build());

        Assert.Contains(model.Features, f => f.Name == "Grand Cathedral");
        Assert.Contains(model.Features, f => f.Name == "place of worship"); // unnamed → humanized

        var md = model.Markdown;
        Assert.Matches(@"\[\d+\] place of worship(?! \()", md);                 // no "(category)"
        Assert.Contains("Grand Cathedral (landmark.place_of_worship)", md);     // named keeps it
    }

    [Fact]
    public void Unnamed_below_landmark_tier_is_clustered_not_promoted()
    {
        var model = new MapGenerator().BuildModel(Build());

        // The unnamed destination café earns no token; it folds into the district's clustered count.
        Assert.DoesNotContain(model.Features, f => f.Category == "food.cafe");
        Assert.Equal(2, model.Features.Count);
        Assert.Contains(model.Districts, d => d.ClusteredCount >= 1);
    }
}
