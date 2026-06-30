using System.IO;
using System.Linq;
using System.Text;
using MarkdownMap.Contract;
using MarkdownMap.Normalizer;
using Xunit;

namespace MarkdownMap.Normalizer.Tests;

/// <summary>
/// Real multipolygon ring assembly (ADR-0014). Synthetic, location-agnostic OSM XML — a named
/// water/park relation whose outer ring is split across member ways, stitched back to a real
/// concave ring (not a convex hull).
/// </summary>
public class RelationRingsTests
{
    private static FeatureCollection Normalize(string osmXml) =>
        new OsmNormalizer().Normalize(new MemoryStream(Encoding.UTF8.GetBytes(osmXml)));

    // An L-shaped (concave) water body whose boundary is two member ways. A convex hull would
    // fill the notch; real assembly keeps it.
    private const string LShapedWater = """
        <?xml version="1.0" encoding="UTF-8"?>
        <osm version="0.6">
         <node id="1" lat="50.0000" lon="10.0000"/>
         <node id="2" lat="50.0000" lon="10.0040"/>
         <node id="3" lat="50.0020" lon="10.0040"/>
         <node id="4" lat="50.0020" lon="10.0020"/>
         <node id="5" lat="50.0040" lon="10.0020"/>
         <node id="6" lat="50.0040" lon="10.0000"/>
         <way id="101"><nd ref="1"/><nd ref="2"/><nd ref="3"/><nd ref="4"/></way>
         <way id="102"><nd ref="4"/><nd ref="5"/><nd ref="6"/><nd ref="1"/></way>
         <relation id="900">
          <member type="way" ref="101" role="outer"/>
          <member type="way" ref="102" role="outer"/>
          <tag k="type" v="multipolygon"/>
          <tag k="natural" v="water"/>
          <tag k="name" v="Bent Lake"/>
         </relation>
        </osm>
        """;

    [Fact]
    public void Stitches_member_ways_into_a_real_ring_smaller_than_the_convex_hull()
    {
        var water = Normalize(LShapedWater).Features.Single(f => f.Properties.Kind == "water");
        Assert.Equal("Bent Lake", water.Properties.Name);
        Assert.Equal("Polygon", water.Geometry.Type);
        Assert.StartsWith("r900", water.Properties.OsmId);

        // The real L-shape has area 3 cells (of a 2×2 grid in 0.002° units); the convex hull
        // would be the full 2×2 square (4 cells). So the assembled area is < hull area.
        var ring = ((double[][][])water.Geometry.Coordinates)[0];
        Assert.True(ShoelaceArea(ring) < FullSquareArea, "assembled ring should be smaller than the convex hull");
    }

    private const double FullSquareArea = 0.0040 * 0.0040; // bounding square of the points

    private static double ShoelaceArea(double[][] ring)
    {
        double a = 0;
        for (int i = 0; i < ring.Length - 1; i++)
            a += ring[i][0] * ring[i + 1][1] - ring[i + 1][0] * ring[i][1];
        return System.Math.Abs(a) / 2;
    }

    [Fact]
    public void Clipped_water_becomes_a_shoreline_line_not_a_filled_hull()
    {
        // A single open way (bbox-clipped boundary) cannot form a ring → emit the shoreline as
        // a LineString so nothing is falsely filled (ADR-0014, option A).
        var osm = """
            <?xml version="1.0" encoding="UTF-8"?>
            <osm version="0.6">
             <node id="1" lat="50.0000" lon="10.0000"/>
             <node id="2" lat="50.0000" lon="10.0040"/>
             <node id="3" lat="50.0030" lon="10.0040"/>
             <way id="101"><nd ref="1"/><nd ref="2"/><nd ref="3"/></way>
             <relation id="900">
              <member type="way" ref="101" role="outer"/>
              <tag k="type" v="multipolygon"/>
              <tag k="natural" v="water"/>
              <tag k="name" v="Clipped Bay"/>
             </relation>
            </osm>
            """;
        var water = Normalize(osm).Features.Single(f => f.Properties.Kind == "water");
        Assert.Equal("Clipped Bay", water.Properties.Name);
        Assert.Equal("LineString", water.Geometry.Type); // shoreline, not a filled polygon
        Assert.StartsWith("r900-L", water.Properties.OsmId);
    }
}
