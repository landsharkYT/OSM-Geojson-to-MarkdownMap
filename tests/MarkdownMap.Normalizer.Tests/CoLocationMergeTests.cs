using System.IO;
using System.Linq;
using System.Text;
using MarkdownMap.Contract;
using MarkdownMap.Normalizer;
using Xunit;

namespace MarkdownMap.Normalizer.Tests;

/// <summary>
/// Co-location merge (ADR-0012). Synthetic, location-agnostic OSM XML — no committed extract.
/// Nodes are placed a few metres apart (0.0001° ≈ 11 m) so they fall inside the merge radius.
/// </summary>
public class CoLocationMergeTests
{
    private static FeatureCollection Normalize(string osmXml) =>
        new OsmNormalizer().Normalize(new MemoryStream(Encoding.UTF8.GetBytes(osmXml)));

    // n1 named church; n2 unnamed church ~11 m away; n3 same-named church ~7 m away;
    // n4 a café 100 m+ away (distinct class + distance → never merges).
    private const string Osm = """
        <?xml version="1.0" encoding="UTF-8"?>
        <osm version="0.6">
         <node id="1" lat="50.0000" lon="10.0000"><tag k="amenity" v="place_of_worship"/><tag k="name" v="St Test"/></node>
         <node id="2" lat="50.0001" lon="10.0000"><tag k="amenity" v="place_of_worship"/></node>
         <node id="3" lat="50.0000" lon="10.0001"><tag k="amenity" v="place_of_worship"/><tag k="name" v="St Test"/></node>
         <node id="4" lat="50.0020" lon="10.0020"><tag k="amenity" v="cafe"/><tag k="name" v="Far Cafe"/></node>
        </osm>
        """;

    [Fact]
    public void Folds_unnamed_and_dedups_same_name_keeping_distinct_features()
    {
        var pois = Normalize(Osm).Features.Where(f => f.Properties.Kind == "poi").ToList();

        // Only the named church (one of n1/n3) and the far café survive.
        Assert.Equal(2, pois.Count);
        Assert.Contains(pois, f => f.Properties.Name == "St Test");
        Assert.Contains(pois, f => f.Properties.Name == "Far Cafe");
        Assert.DoesNotContain(pois, f => string.IsNullOrEmpty(f.Properties.Name)); // unnamed folded away
    }

    [Fact]
    public void Does_not_merge_across_different_category_classes()
    {
        // A café and a church at the SAME spot must not merge (different class).
        var osm = """
            <?xml version="1.0" encoding="UTF-8"?>
            <osm version="0.6">
             <node id="1" lat="50.0000" lon="10.0000"><tag k="amenity" v="place_of_worship"/><tag k="name" v="St Test"/></node>
             <node id="2" lat="50.00001" lon="10.0000"><tag k="amenity" v="cafe"/><tag k="name" v="Nook"/></node>
            </osm>
            """;
        var pois = Normalize(osm).Features.Where(f => f.Properties.Kind == "poi").ToList();
        Assert.Equal(2, pois.Count);
    }

    [Fact]
    public void Does_not_merge_same_name_features_far_apart()
    {
        // Two same-named churches ~220 m apart are distinct places, not duplicates.
        var osm = """
            <?xml version="1.0" encoding="UTF-8"?>
            <osm version="0.6">
             <node id="1" lat="50.0000" lon="10.0000"><tag k="amenity" v="place_of_worship"/><tag k="name" v="St Test"/></node>
             <node id="2" lat="50.0020" lon="10.0000"><tag k="amenity" v="place_of_worship"/><tag k="name" v="St Test"/></node>
            </osm>
            """;
        var pois = Normalize(osm).Features.Where(f => f.Properties.Kind == "poi").ToList();
        Assert.Equal(2, pois.Count);
    }
}
