using System.Collections.Generic;
using System.Linq;
using MarkdownMap.Contract;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

/// <summary>
/// Minor vs prop features (ADR-0020): named clustered features are "minor features" (listable via the
/// opt-in setting, deduped ×N); nameless ones are "prop features" (count only). Render-only: with the
/// toggle off the markdown is byte-identical to before the tier existed.
/// </summary>
public class MinorFeatureTests
{
    // Features spaced ~111 m apart (0.001°) so the ~40 m co-location merge never fuses same-name pairs.
    private static Feature Poi(string? name, string category, double lon) => new Feature
    {
        Properties = new FeatureProperties
        {
            Kind = "poi", Name = name, Category = category, Importance = 30, Tier = "minor",
            Salience = SalienceClassifier.Of(category), OsmId = "n" + (name ?? category) + lon,
        },
        Geometry = new Geometry { Type = "Point", Coordinates = new[] { lon, 0.0 } },
    };

    private static FeatureCollection Fixture() => new FeatureCollection
    {
        Properties = new CollectionProperties { Title = "Propton", Bounds = new[] { -0.02, -0.02, 0.02, 0.02 } },
        Features = new List<Feature>
        {
            new Feature { Properties = new FeatureProperties { Kind = "place", Name = "Downtown" },
                          Geometry = new Geometry { Type = "Point", Coordinates = new[] { 0.0, 0.0 } } },
            Poi("Rivertown School", "civic.school", 0.001),          // core → promoted, forms the district
            Poi("Maple Apartments", "residential.apartments", 0.002), // named clustered → minor feature
            Poi("Maple Apartments", "residential.apartments", 0.003), // same name → dedup to ×2
            Poi("Oak Court", "residential.apartments", 0.004),        // named clustered → minor feature
            Poi(null, "residential.house", 0.005),                    // nameless clustered → prop feature
            Poi(null, "residential.house", 0.006),                    // nameless clustered → prop feature
        },
    };

    [Fact]
    public void Model_flags_named_minors_and_splits_the_district_count()
    {
        var m = new MapGenerator().BuildModel(Fixture());
        Assert.Equal(5, m.Minors.Count);                                  // 3 named + 2 nameless clustered
        Assert.Equal(3, m.Minors.Count(mi => mi.Named));
        Assert.Equal(2, m.Minors.Count(mi => !mi.Named));

        var d = Assert.Single(m.Districts);
        Assert.Equal(5, d.ClusteredCount);                               // total (for the toggle-off count)
        Assert.Equal(2, d.PropCount);                                    // nameless remainder
        Assert.Equal(new[] { "Maple Apartments", "Oak Court" }, d.NamedMinors.Select(n => n.Name)); // deduped, sorted
        Assert.Equal(2, d.NamedMinors.First(n => n.Name == "Maple Apartments").Count); // ×2
    }

    [Fact]
    public void Toggle_off_is_the_unchanged_clustered_count()
    {
        var md = new MapGenerator().Generate(Fixture());
        Assert.Contains("clustered: ~5 minor", md);
        Assert.DoesNotContain("minor:", md);
        Assert.DoesNotContain("props:", md);
    }

    [Fact]
    public void Toggle_on_lists_named_minors_deduped_and_relabels_the_count_to_props()
    {
        var md = new MapGenerator(new GeneratorOptions { MinorFeatures = true }).Generate(Fixture());
        Assert.Contains("minor: Maple Apartments ×2 · Oak Court", md);
        Assert.Contains("props: ~2", md);
        Assert.DoesNotContain("clustered: ~", md); // the old count line is gone when listing
    }

    [Fact]
    public void A_comma_in_a_name_is_not_mistaken_for_a_delimiter()
    {
        // The list is `·`-separated, so a name that contains a comma stays one item (ADR-0020).
        var fc = new FeatureCollection
        {
            Properties = new CollectionProperties { Title = "Commaville", Bounds = new[] { -0.02, -0.02, 0.02, 0.02 } },
            Features = new List<Feature>
            {
                new Feature { Properties = new FeatureProperties { Kind = "place", Name = "Downtown" },
                              Geometry = new Geometry { Type = "Point", Coordinates = new[] { 0.0, 0.0 } } },
                Poi("Rivertown School", "civic.school", 0.001),          // core → forms the district
                Poi("Aardvark Flats", "residential.apartments", 0.002),
                Poi("Smith, Jones Court", "residential.apartments", 0.003), // comma in the name
            },
        };
        var md = new MapGenerator(new GeneratorOptions { MinorFeatures = true }).Generate(fc);
        Assert.Contains("minor: Aardvark Flats · Smith, Jones Court", md);
    }
}
