using System.IO;
using System.Linq;
using System.Text;
using MarkdownMap.Contract;
using MarkdownMap.Normalizer;
using Xunit;

namespace MarkdownMap.Normalizer.Tests;

/// <summary>
/// ADR-0019 footprint nudge: campus halls (building=university) score identically at the venue band,
/// so the Normalizer adds a bounded, area-derived bonus that lets the big halls out-sort the annexes
/// within the promotion budget. Synthetic, location-agnostic squares — no committed extract.
/// </summary>
public class AreaNudgeTests
{
    private static FeatureCollection Normalize(string osmXml) =>
        new OsmNormalizer().Normalize(new MemoryStream(Encoding.UTF8.GetBytes(osmXml)));

    // Two named university halls, far apart (so co-location never merges them): a tiny ~80 m² annex
    // (below the 200 m² floor → +0) and a large ~30,000 m² hall (→ +9 cap).
    private const string Osm = """
        <?xml version="1.0" encoding="UTF-8"?>
        <osm version="0.6">
         <node id="1" lat="50.00000" lon="10.00000"/>
         <node id="2" lat="50.00000" lon="10.00010"/>
         <node id="3" lat="50.00010" lon="10.00010"/>
         <node id="4" lat="50.00010" lon="10.00000"/>
         <way id="100"><nd ref="1"/><nd ref="2"/><nd ref="3"/><nd ref="4"/><nd ref="1"/>
           <tag k="building" v="university"/><tag k="name" v="Small Annex"/></way>
         <node id="5" lat="50.01000" lon="10.01000"/>
         <node id="6" lat="50.01000" lon="10.01200"/>
         <node id="7" lat="50.01200" lon="10.01200"/>
         <node id="8" lat="50.01200" lon="10.01000"/>
         <way id="200"><nd ref="5"/><nd ref="6"/><nd ref="7"/><nd ref="8"/><nd ref="5"/>
           <tag k="building" v="university"/><tag k="name" v="Great Hall"/></way>
        </osm>
        """;

    [Fact]
    public void The_bigger_hall_out_sorts_the_annex_but_both_stay_below_the_core_line()
    {
        var halls = Normalize(Osm).Features
            .Where(f => f.Properties.Category == "civic.university_building")
            .ToDictionary(f => f.Properties.Name!, f => f.Properties.Importance!.Value);

        Assert.Equal(65, halls["Small Annex"]);   // 55 base + 10 name, sub-floor footprint → +0
        Assert.Equal(74, halls["Great Hall"]);    // + capped area bonus (+9)
        Assert.True(halls["Great Hall"] > halls["Small Annex"]);
        Assert.All(halls.Values, v => Assert.True(v < 75, "a budgeted hall never crosses the core line"));
    }

    [Fact]
    public void A_point_node_hall_gets_no_area_bonus()
    {
        // Only ways carry a footprint; a node has no area, so it scores the plain venue-band 65.
        var osm = """
            <?xml version="1.0" encoding="UTF-8"?>
            <osm version="0.6">
             <node id="1" lat="50.0000" lon="10.0000"><tag k="building" v="university"/><tag k="name" v="Node Hall"/></node>
            </osm>
            """;
        var hall = Normalize(osm).Features.Single(f => f.Properties.Kind == "poi");
        Assert.Equal(65, hall.Properties.Importance);
    }
}
