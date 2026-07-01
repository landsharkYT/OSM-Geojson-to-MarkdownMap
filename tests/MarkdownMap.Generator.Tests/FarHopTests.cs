using System.Collections.Generic;
using MarkdownMap.Contract;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

/// <summary>
/// A connection at/over the far cutoff (≥500 m) is flagged `(far)` so a long proximity edge in a
/// sparse area doesn't read like a walkable scene-local hop (grill 2026-07-01).
/// </summary>
public class FarHopTests
{
    private static Feature Poi(string name, double lon, double lat) => new Feature
    {
        Properties = new FeatureProperties
        {
            Kind = "poi", Name = name, Category = "civic.hall", Importance = 50,
            Tier = "landmark", Salience = "core", OsmId = "n" + name,
        },
        Geometry = new Geometry { Type = "Point", Coordinates = new[] { lon, lat } },
    };

    private static string Generate(params Feature[] pois) =>
        new MapGenerator().Generate(new FeatureCollection
        {
            Properties = new CollectionProperties { Title = "Sparse", Bounds = new[] { -0.02, -0.02, 0.02, 0.02 } },
            Features = new List<Feature>(pois),
        });

    [Fact]
    public void A_long_connection_is_flagged_far()
    {
        // ~0.006° of latitude ≈ 660 m apart → over the 500 m cutoff.
        var md = Generate(Poi("North Hall", 0.0, 0.006), Poi("South Hall", 0.0, 0.0));
        Assert.Matches(@"→ \[\d+\] — ~\d+m \w+ \(far\)", md);
    }

    [Fact]
    public void A_short_connection_is_not_flagged()
    {
        // ~0.0009° ≈ 100 m apart → well under the cutoff. (The reading key mentions `(far)`, so
        // check no *connection line* carries it, not the whole document.)
        var md = Generate(Poi("Near A", 0.0, 0.0009), Poi("Near B", 0.0, 0.0));
        Assert.DoesNotMatch(@"→ \[\d+\] — ~\d+m \w+ \(far\)", md);
    }
}
