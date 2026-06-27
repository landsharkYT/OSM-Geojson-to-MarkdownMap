using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MarkdownMap.Contract;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using OsmSharp;
using OsmSharp.Streams;
using OsmSharp.Tags;
using ContractGeometry = MarkdownMap.Contract.Geometry;

namespace MarkdownMap.Normalizer;

/// <summary>
/// Step 1 (Normalizer v0): streams a static OSM export and emits a contract-conformant
/// GeoJSON FeatureCollection of <c>poi</c> Features only. See docs/impl-step-1.md.
/// </summary>
public sealed class OsmNormalizer
{
    private static readonly string[] TitlePlaces = { "neighbourhood", "quarter", "suburb" };
    private static readonly HashSet<string> AnchorPlaces = new(StringComparer.Ordinal) { "neighbourhood", "quarter" };
    private const double StreetSnapRadiusMeters = 30.0;

    public FeatureCollection NormalizeFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Normalize(stream);
    }

    public FeatureCollection Normalize(Stream osmStream)
    {
        var nodeCoords = new Dictionary<long, (double lon, double lat)>();
        var poiNodes = new List<(long id, Dictionary<string, string> tags, double lon, double lat)>();
        var poiWays = new List<(long id, Dictionary<string, string> tags, long[] nodes)>();
        var roadWays = new List<(string name, long[] nodes)>();
        var placeNodes = new List<(long id, string name, double lon, double lat)>();
        var barrierWays = new List<(long id, string label, string cls, long[] nodes)>();
        var areaWays = new List<(long id, string kind, string name, long[] nodes)>();
        var titleNames = new List<string>();

        double minLon = double.MaxValue, minLat = double.MaxValue;
        double maxLon = double.MinValue, maxLat = double.MinValue;

        var source = new XmlOsmStreamSource(osmStream);
        foreach (var element in source)
        {
            switch (element)
            {
                case Node n when n.Id is long nid && n.Longitude is double lon && n.Latitude is double lat:
                    nodeCoords[nid] = (lon, lat);
                    if (lon < minLon) minLon = lon;
                    if (lat < minLat) minLat = lat;
                    if (lon > maxLon) maxLon = lon;
                    if (lat > maxLat) maxLat = lat;

                    var ntags = ToDict(n.Tags);
                    CollectTitle(ntags, titleNames);
                    if (ntags.TryGetValue("place", out var place) && AnchorPlaces.Contains(place)
                        && ntags.TryGetValue("name", out var pname))
                        placeNodes.Add((nid, pname, lon, lat));
                    if (Classifier.Classify(ntags) is not null)
                        poiNodes.Add((nid, ntags, lon, lat));
                    break;

                case Way w when w.Id is long wid && w.Nodes is { Length: > 0 }:
                    var wtags = ToDict(w.Tags);
                    if (wtags.ContainsKey("highway") && wtags.TryGetValue("name", out var rname))
                        roadWays.Add((rname, w.Nodes));
                    if (TerrainClassifier.Barrier(wtags) is var (blabel, bcls) && blabel is not null)
                        barrierWays.Add((wid, blabel, bcls!, w.Nodes));
                    if (TerrainClassifier.Area(wtags) is var (akind, aname) && akind is not null)
                        areaWays.Add((wid, akind, aname!, w.Nodes));
                    if (Classifier.Classify(wtags) is not null)
                        poiWays.Add((wid, wtags, w.Nodes));
                    break;
            }
        }

        var roads = ResolveRoads(roadWays, nodeCoords);
        var features = new List<Feature>();

        foreach (var (id, tags, lon, lat) in poiNodes)
            features.Add(MakePoi("n" + id, tags, lon, lat, roads));

        foreach (var (id, tags, nodes) in poiWays)
        {
            if (TryRepresentativePoint(nodes, nodeCoords, out var lon, out var lat))
                features.Add(MakePoi("w" + id, tags, lon, lat, roads));
        }

        foreach (var (id, name, lon, lat) in placeNodes)
            features.Add(MakePlace("n" + id, name, lon, lat));

        foreach (var (id, label, cls, nodes) in barrierWays)
        {
            var line = BuildSimplifiedLine(nodes, nodeCoords);
            if (line is not null) features.Add(MakeBarrier("w" + id, label, cls, line));
        }

        foreach (var (id, kind, name, nodes) in areaWays)
        {
            var ring = BuildSimplifiedRing(nodes, nodeCoords);
            if (ring is not null) features.Add(MakeArea("w" + id, kind, name, ring));
        }

        // Deterministic ordering for stable diffs.
        features.Sort((a, b) => string.CompareOrdinal(a.Properties.OsmId, b.Properties.OsmId));

        return new FeatureCollection
        {
            Properties = new CollectionProperties
            {
                SchemaVersion = 1,
                Title = BuildTitle(titleNames),
                Bounds = features.Count == 0 && nodeCoords.Count == 0
                    ? new double[4]
                    : new[] { minLon, minLat, maxLon, maxLat },
            },
            Features = features,
        };
    }

    private static Feature MakePoi(
        string osmId, IReadOnlyDictionary<string, string> tags, double lon, double lat,
        IReadOnlyList<Road> roads)
    {
        var c = Classifier.Classify(tags)!;
        tags.TryGetValue("name", out var name);
        var (street, approx) = StreetSnapper.Assign(tags, (lon, lat), roads, StreetSnapRadiusMeters);
        return new Feature
        {
            Properties = new FeatureProperties
            {
                Kind = "poi",
                Name = name,
                OsmId = osmId,
                Category = c.Category,
                Importance = c.Importance,
                Tier = c.Tier,
                Street = street,
                StreetApprox = street is not null && approx ? true : (bool?)null,
            },
            Geometry = new ContractGeometry { Type = "Point", Coordinates = new[] { Round(lon), Round(lat) } },
        };
    }

    private static Feature MakePlace(string osmId, string name, double lon, double lat) => new Feature
    {
        Properties = new FeatureProperties { Kind = "place", Name = name, OsmId = osmId },
        Geometry = new ContractGeometry { Type = "Point", Coordinates = new[] { Round(lon), Round(lat) } },
    };

    private static Feature MakeBarrier(string osmId, string label, string cls, double[][] line) => new Feature
    {
        Properties = new FeatureProperties { Kind = "barrier", Name = label, OsmId = osmId, BarrierClass = cls },
        Geometry = new ContractGeometry { Type = "LineString", Coordinates = line },
    };

    private static Feature MakeArea(string osmId, string kind, string name, double[][][] poly) => new Feature
    {
        Properties = new FeatureProperties { Kind = kind, Name = name, OsmId = osmId },
        Geometry = new ContractGeometry { Type = "Polygon", Coordinates = poly },
    };

    private const double SimplifyToleranceDeg = 0.000045; // ~5 m

    private static double[][]? BuildSimplifiedLine(
        long[] nodeIds, IReadOnlyDictionary<long, (double lon, double lat)> coords)
    {
        var seq = ResolveSeq(nodeIds, coords);
        if (seq.Count < 2) return null;
        var line = GeometryFactory.Default.CreateLineString(seq.ToArray());
        var simp = DouglasPeuckerSimplifier.Simplify(line, SimplifyToleranceDeg);
        var c = simp.Coordinates.Length >= 2 ? simp.Coordinates : line.Coordinates;
        return c.Select(p => new[] { Round(p.X), Round(p.Y) }).ToArray();
    }

    private static double[][][]? BuildSimplifiedRing(
        long[] nodeIds, IReadOnlyDictionary<long, (double lon, double lat)> coords)
    {
        var seq = ResolveSeq(nodeIds, coords);
        if (seq.Count >= 1 && !seq[0].Equals2D(seq[seq.Count - 1])) seq.Add(seq[0].Copy());
        if (seq.Count < 4) return null;
        try
        {
            var poly = GeometryFactory.Default.CreatePolygon(GeometryFactory.Default.CreateLinearRing(seq.ToArray()));
            var simp = DouglasPeuckerSimplifier.Simplify(poly, SimplifyToleranceDeg);
            var ring = simp is Polygon p && !p.IsEmpty ? p.ExteriorRing.Coordinates : poly.ExteriorRing.Coordinates;
            return new[] { ring.Select(c => new[] { Round(c.X), Round(c.Y) }).ToArray() };
        }
        catch { return null; }
    }

    private static List<Coordinate> ResolveSeq(
        long[] nodeIds, IReadOnlyDictionary<long, (double lon, double lat)> coords)
    {
        var seq = new List<Coordinate>(nodeIds.Length);
        foreach (var id in nodeIds)
            if (coords.TryGetValue(id, out var c))
                seq.Add(new Coordinate(c.lon, c.lat));
        return seq;
    }

    private static List<Road> ResolveRoads(
        List<(string name, long[] nodes)> roadWays,
        IReadOnlyDictionary<long, (double lon, double lat)> coords)
    {
        var roads = new List<Road>(roadWays.Count);
        foreach (var (name, nodes) in roadWays)
        {
            var pts = new List<(double lon, double lat)>(nodes.Length);
            foreach (var id in nodes)
                if (coords.TryGetValue(id, out var c))
                    pts.Add(c);
            if (pts.Count >= 2) roads.Add(new Road(name, pts));
        }
        return roads;
    }

    private static bool TryRepresentativePoint(
        long[] nodeIds, IReadOnlyDictionary<long, (double lon, double lat)> coords,
        out double lon, out double lat)
    {
        var pts = new List<(double lon, double lat)>(nodeIds.Length);
        foreach (var id in nodeIds)
            if (coords.TryGetValue(id, out var c))
                pts.Add(c);
        return RepresentativePoint.TryCompute(pts, out lon, out lat);
    }

    private static double Round(double v) => Math.Round(v, 7);

    private static Dictionary<string, string> ToDict(TagsCollectionBase? tags)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        if (tags is not null)
            foreach (var t in tags)
                d[t.Key] = t.Value;
        return d;
    }

    private static void CollectTitle(IReadOnlyDictionary<string, string> tags, List<string> sink)
    {
        if (tags.TryGetValue("place", out var place) && TitlePlaces.Contains(place)
            && tags.TryGetValue("name", out var name) && !sink.Contains(name))
            sink.Add(name);
    }

    private static string BuildTitle(List<string> names) =>
        names.Count == 0 ? "Untitled area" : string.Join(" & ", names.Take(2));
}
