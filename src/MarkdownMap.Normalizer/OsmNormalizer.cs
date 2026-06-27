using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MarkdownMap.Contract;
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
                    if (Classifier.Classify(ntags) is not null)
                        poiNodes.Add((nid, ntags, lon, lat));
                    break;

                case Way w when w.Id is long wid && w.Nodes is { Length: > 0 }:
                    var wtags = ToDict(w.Tags);
                    if (Classifier.Classify(wtags) is not null)
                        poiWays.Add((wid, wtags, w.Nodes));
                    break;
            }
        }

        var features = new List<Feature>();

        foreach (var (id, tags, lon, lat) in poiNodes)
            features.Add(MakePoi("n" + id, tags, lon, lat));

        foreach (var (id, tags, nodes) in poiWays)
        {
            if (TryRepresentativePoint(nodes, nodeCoords, out var lon, out var lat))
                features.Add(MakePoi("w" + id, tags, lon, lat));
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

    private static Feature MakePoi(string osmId, IReadOnlyDictionary<string, string> tags, double lon, double lat)
    {
        var c = Classifier.Classify(tags)!;
        tags.TryGetValue("name", out var name);
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
                Street = null,        // street attribution lands in a later step
                StreetApprox = null,
            },
            Geometry = new ContractGeometry { Type = "Point", Coordinates = new[] { Round(lon), Round(lat) } },
        };
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
