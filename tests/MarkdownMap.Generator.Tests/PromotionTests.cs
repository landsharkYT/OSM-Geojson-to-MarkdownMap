using System.Collections.Generic;
using System.Linq;
using MarkdownMap.Contract;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

/// <summary>
/// Narrative salience + per-District promotion budget (ADR-0018): a `core` Feature always promotes,
/// `budgeted` Features fill up to the budget by importance, and the rest cluster.
/// </summary>
public class PromotionTests
{
    [Theory]
    [InlineData("landmark.place_of_worship", "core")]
    [InlineData("landmark.museum", "core")]
    [InlineData("landmark.artwork", "budgeted")]
    [InlineData("landmark.viewpoint", "budgeted")]
    [InlineData("civic.school", "core")]
    [InlineData("civic.hospital", "core")]
    [InlineData("civic.dentist", "budgeted")]
    [InlineData("civic.pharmacy", "budgeted")]
    [InlineData("food.cafe", "budgeted")]
    [InlineData("shop.clothes", "budgeted")]
    [InlineData("leisure.marina", "core")]
    [InlineData("leisure.stadium", "core")]
    [InlineData("leisure.fitness_centre", "budgeted")]
    [InlineData("leisure.swimming_pool", "budgeted")] // facility, not a major venue (ADR-0018)
    [InlineData("leisure.ice_rink", "budgeted")]
    [InlineData("residential.apartments", "clustered")]
    public void Salience_classifies_by_category(string category, string expected) =>
        Assert.Equal(expected, SalienceClassifier.Of(category));

    [Theory]
    [InlineData("landmark.place_of_worship", true)]
    [InlineData("landmark.church", true)]
    [InlineData("landmark.museum", false)]
    [InlineData("leisure.swimming_pool", false)]
    [InlineData("civic.school", false)]
    public void IsWorship_only_true_for_worship(string category, bool expected) =>
        Assert.Equal(expected, SalienceClassifier.IsWorship(category));

    private static Feature Poi(string name, string category, int importance, double lon, double lat, string? salience = null) => new Feature
    {
        Properties = new FeatureProperties
        {
            Kind = "poi", Name = name, Category = category, Importance = importance,
            Tier = "destination", Salience = salience ?? SalienceClassifier.Of(category),
            OsmId = "n" + (name ?? category) + importance,
        },
        Geometry = new Geometry { Type = "Point", Coordinates = new[] { lon, lat } },
    };

    private static Feature Place(string name, double lon, double lat) => new Feature
    {
        Properties = new FeatureProperties { Kind = "place", Name = name },
        Geometry = new Geometry { Type = "Point", Coordinates = new[] { lon, lat } },
    };

    [Fact]
    public void Budget_caps_budgeted_features_per_district_core_always_promotes()
    {
        var feats = new List<Feature> { Place("Downtown", 0, 0) };
        for (int i = 0; i < 3; i++) feats.Add(Poi("School" + i, "civic.school", 90, 0.0001 * i, 0.0));   // core
        for (int i = 0; i < 30; i++) feats.Add(Poi("Cafe" + i, "food.cafe", 80 - i, 0.0002 * i, 0.0003)); // budgeted, distinct imp

        var fc = new FeatureCollection
        {
            Properties = new CollectionProperties { Title = "Budgetville", Bounds = new[] { -0.01, -0.01, 0.01, 0.01 } },
            Features = feats,
        };

        var m = new MapGenerator(new GeneratorOptions { PromotionBudget = 20 }).BuildModel(fc);
        Assert.Equal(3, m.Features.Count(f => f.Category == "civic.school"));  // all core promote
        Assert.Equal(20, m.Features.Count(f => f.Category == "food.cafe"));    // budget caps at 20
        Assert.Equal(10, m.Minors.Count(f => f.Category == "food.cafe"));      // the rest cluster
        // The 20 kept are the highest-importance ones.
        Assert.All(m.Features.Where(f => f.Category == "food.cafe"), f => Assert.True(f.Importance >= 80 - 19));
    }

    [Fact]
    public void Unnamed_clusters_unless_worship()
    {
        var fc = new FeatureCollection
        {
            Properties = new CollectionProperties { Title = "Nameless", Bounds = new[] { -0.01, -0.01, 0.01, 0.01 } },
            Features = new List<Feature>
            {
                Place("Downtown", 0, 0),
                Poi("Named Cafe", "food.cafe", 65, 0.001, 0.0),
                Poi(null!, "landmark.place_of_worship", 90, 0.0011, 0.0), // unnamed worship → promotes
                Poi(null!, "civic.school", 90, 0.0012, 0.0),              // unnamed non-worship core → clusters
                Poi(null!, "food.cafe", 60, 0.0013, 0.0),                 // unnamed budgeted → clusters
            },
        };

        var m = new MapGenerator().BuildModel(fc);
        Assert.Contains(m.Features, f => f.Category == "landmark.place_of_worship"); // the church promotes unnamed
        Assert.DoesNotContain(m.Features, f => f.Category == "civic.school");        // unnamed non-worship clusters now
        Assert.Equal(1, m.Features.Count(f => f.Category == "food.cafe"));           // only the named cafe promoted
        Assert.Equal(2, m.Minors.Count);                                            // unnamed school + unnamed cafe
    }
}
