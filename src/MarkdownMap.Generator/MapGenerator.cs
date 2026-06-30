using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MarkdownMap.Contract;

namespace MarkdownMap.Generator;

/// <summary>
/// Stage 2: turns a contract GeoJSON FeatureCollection into a structured <see cref="MapModel"/>
/// and renders the MarkdownMap from it (one source of truth, two views — docs/map-model.md).
/// Districts, promotion/clustering, and spines are computed from `place` anchors (ADR-0007).
/// OSM-agnostic — reasons only over the normalized schema.
/// </summary>
public sealed class MapGenerator
{
    private readonly GeneratorOptions _opts;

    public MapGenerator(GeneratorOptions? options = null) => _opts = options ?? GeneratorOptions.Default;

    /// <summary>The rendered MarkdownMap.</summary>
    public string Generate(FeatureCollection fc) => BuildModel(fc).Markdown;

    /// <summary>The structured model (incl. the rendered markdown). Used by the Explorer/WASM.</summary>
    public MapModel BuildModel(FeatureCollection fc)
    {
        var anchors = fc.Features
            .Where(f => f.Properties.Kind == "place")
            .Select(f => new Anchor(NameOf(f.Properties), GeoJsonReader.PointOf(f)))
            .ToList();
        bool hasDistricts = anchors.Count > 0;

        var pois = fc.Features.Where(f => f.Properties.Kind == "poi").ToList();
        bool Promoted(Feature f) => !hasDistricts || f.Properties.Tier != "minor";
        var promotedF = pois.Where(Promoted)
            .OrderByDescending(f => f.Properties.Importance ?? 0)
            .ThenBy(f => f.Properties.OsmId, StringComparer.Ordinal)
            .ToList();
        var minorF = pois.Where(f => !Promoted(f)).ToList();

        var points = promotedF.Select(GeoJsonReader.PointOf).ToList();
        int pad = Math.Max(2, promotedF.Count.ToString(CultureInfo.InvariantCulture).Length);
        string Token(int i) => "[" + (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(pad, '0') + "]";

        var adjacency = ProximityGraph.Build(points, _opts.NeighborsPerFeature);
        var district = points.Select(p => Districts.Nearest(p, anchors)).ToList();
        var barriers = fc.Features.Where(f => f.Properties.Kind == "barrier")
            .Select(f => (label: NameOf(f.Properties), line: GeoJsonReader.LineOf(f)))
            .Where(b => b.line.Count >= 2).ToList();

        // --- promoted features ---
        var features = new List<PromotedFeature>(promotedF.Count);
        for (int i = 0; i < promotedF.Count; i++)
        {
            var p = promotedF[i].Properties;
            features.Add(new PromotedFeature
            {
                Token = Token(i),
                Name = NameOf(p),
                Category = p.Category ?? "",
                Importance = p.Importance ?? 0,
                Tier = p.Tier ?? "",
                Street = string.IsNullOrEmpty(p.Street) ? null : p.Street,
                StreetApprox = p.StreetApprox == true,
                District = district[i],
                Lon = points[i].Lon,
                Lat = points[i].Lat,
            });
        }

        // --- minors (clustered in markdown, plotted in the Explorer) ---
        var minors = minorF.Select(m =>
        {
            var mp = GeoJsonReader.PointOf(m);
            return new MinorFeature
            {
                Name = NameOf(m.Properties),
                Category = m.Properties.Category ?? "",
                District = hasDistricts ? Districts.Nearest(mp, anchors) : null,
                Lon = mp.Lon,
                Lat = mp.Lat,
            };
        }).ToList();

        // --- edges (per feature, sorted by distance then token; flat in emission order) ---
        var edges = new List<Edge>();
        for (int i = 0; i < promotedF.Count; i++)
        {
            var neighbours = adjacency[i]
                .Select(j => (j, dist: Geo.HaversineMeters(points[i], points[j])))
                .OrderBy(t => t.dist).ThenBy(t => t.j);
            foreach (var (j, dist) in neighbours)
            {
                edges.Add(new Edge
                {
                    FromToken = Token(i),
                    ToToken = Token(j),
                    ToName = NameOf(promotedF[j].Properties),
                    Meters = Geo.RoundMeters(dist),
                    Dir = Geo.EightWind(points[i], points[j]),
                    Bucket = Geo.Bucket(dist, _opts.Buckets),
                    Crosses = CrossedBarrier(points[i], points[j], barriers),
                });
            }
        }

        var model = new MapModel
        {
            Title = Title(fc),
            Bounds = fc.Properties.Bounds is { Length: 4 } b ? b : new double[0],
            Features = features,
            Minors = minors,
            Edges = edges,
            Districts = BuildDistricts(promotedF, points, district, minorF, anchors, Token),
            Terrain = BuildTerrain(fc, fc.Properties.Bounds),
        };
        model.Markdown = Render(model);
        return model;
    }

    private List<District> BuildDistricts(
        List<Feature> promoted, List<LonLat> points, List<string?> district,
        List<Feature> minors, List<Anchor> anchors, Func<int, string> token)
    {
        if (anchors.Count == 0) return new List<District>();

        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int i = 0; i < promoted.Count; i++)
        {
            var d = district[i]!;
            if (!groups.TryGetValue(d, out var list)) groups[d] = list = new List<int>();
            list.Add(i);
        }
        var minorCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var m in minors)
        {
            var d = Districts.Nearest(GeoJsonReader.PointOf(m), anchors)!;
            minorCounts[d] = minorCounts.TryGetValue(d, out var c) ? c + 1 : 1;
        }

        var result = new List<District>();
        foreach (var g in groups.OrderByDescending(g => g.Value.Count).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            var idxs = g.Value;
            var (dir, order) = Spine.Compute(idxs.Select(i => points[i]).ToList());
            string? domStreet = idxs
                .Select(i => promoted[i].Properties.Street)
                .Where(s => !string.IsNullOrEmpty(s))
                .GroupBy(s => s!, StringComparer.Ordinal)
                .OrderByDescending(grp => grp.Count()).ThenBy(grp => grp.Key, StringComparer.Ordinal)
                .Select(grp => grp.Key).FirstOrDefault();

            var keyLocal = new HashSet<int>(idxs
                .Select((gi, local) => (local, imp: promoted[gi].Properties.Importance ?? 0))
                .OrderByDescending(t => t.imp).ThenBy(t => t.local)
                .Take(_opts.SpineKeyCap).Select(t => t.local));
            var spineTokens = order.Where(o => keyLocal.Contains(o)).Select(o => token(idxs[o])).ToList();

            var anchor = anchors.First(a => a.Name == g.Key).Point;
            result.Add(new District
            {
                Name = g.Key,
                Street = domStreet,
                SpineDir = dir,
                SpineTokens = spineTokens,
                PromotedCount = idxs.Count,
                ClusteredCount = minorCounts.TryGetValue(g.Key, out var mc) ? mc : 0,
                AnchorLon = anchor.Lon,
                AnchorLat = anchor.Lat,
            });
        }
        return result;
    }

    private static readonly string[] TerrainKindOrder = { "water", "park", "barrier" };

    private static List<TerrainEntry> BuildTerrain(FeatureCollection fc, double[] bounds)
    {
        var terrain = fc.Features.Where(f => f.Properties.Kind is "water" or "park" or "barrier").ToList();
        if (terrain.Count == 0) return new List<TerrainEntry>();

        return terrain
            .GroupBy(f => (kind: f.Properties.Kind, name: NameOf(f.Properties)))
            .Select(g =>
            {
                bool linear = g.Key.kind == "barrier";
                var partsLonLat = g.Select(f => linear ? GeoJsonReader.LineOf(f) : GeoJsonReader.PolygonOuterOf(f))
                    .Where(pl => pl.Count > 0).ToList();
                var flat = partsLonLat.SelectMany(pl => pl).ToList();
                string cls = g.First().Properties.BarrierClass ?? "";
                return new
                {
                    g.Key.kind,
                    g.Key.name,
                    kindLabel = linear ? "barrier:" + cls : g.Key.kind,
                    note = g.Key.kind switch { "water" => "open water", "park" => "green space", _ => "impassable except at crossings" },
                    pos = TerrainPosition.Describe(flat, bounds, linear),
                    empty = flat.Count == 0,
                    geomType = linear ? "LineString" : "Polygon",
                    parts = partsLonLat.Select(pl => pl.Select(p => new[] { p.Lon, p.Lat }).ToArray()).ToList(),
                };
            })
            .Where(e => !e.empty)
            .OrderBy(e => Array.IndexOf(TerrainKindOrder, e.kind))
            .ThenBy(e => e.name, StringComparer.Ordinal)
            .Select(e => new TerrainEntry
            {
                Name = e.name, Kind = e.kind, KindLabel = e.kindLabel, Note = e.note,
                Position = e.pos, GeometryType = e.geomType, Parts = e.parts,
            })
            .ToList();
    }

    // ----- rendering (a view over MapModel; output is byte-identical to before the refactor) -----

    private string Render(MapModel m)
    {
        var sb = new StringBuilder();
        sb.Append("# MARKDOWNMAP — ").Append(m.Title).Append("\n\n");
        if (_opts.DirectivePreamble) AppendPreamble(sb);
        AppendHowToRead(sb, m.Districts.Count > 0, m.Terrain.Count > 0);
        AppendBounds(sb, m.Bounds);
        AppendTerrain(sb, m.Terrain);
        if (m.Districts.Count > 0) AppendDistricts(sb, m.Districts);
        AppendConnections(sb, m.Features, m.Edges);
        return sb.ToString();
    }

    private static string Title(FeatureCollection fc) =>
        string.IsNullOrWhiteSpace(fc.Properties.Title) ? "Untitled area" : fc.Properties.Title;

    private static string NameOf(FeatureProperties p) => string.IsNullOrEmpty(p.Name) ? "unnamed" : p.Name!;

    private static void AppendPreamble(StringBuilder sb)
    {
        sb.Append("<!-- DIRECTIVE PREAMBLE · mode=whole-area -->\n\n");
        sb.Append("**TO THE ASSISTANT — READ FIRST.** The document below is the **authoritative map** of\n");
        sb.Append("this world. Every position, distance, and direction is canon. **Do not invent\n");
        sb.Append("geography or places that are not listed here.** When a scene involves *where* something\n");
        sb.Append("is or *how far*, consult this map and reason from it. If something is not on the map, it\n");
        sb.Append("is unknown — say so rather than guessing.\n\n");
        sb.Append("<!-- /DIRECTIVE PREAMBLE -->\n\n");
    }

    private static void AppendHowToRead(StringBuilder sb, bool hasDistricts, bool hasTerrain)
    {
        sb.Append("## How to read this map\n\n");
        sb.Append("- **Feature header** = `[token] Name (category)`");
        sb.Append(hasDistricts ? ", optionally `· on <street> · <district>`.\n" : ".\n");
        sb.Append("- **Connection** = `→ [token] Name — ~<metres>m <DIR>, <size>`, read as: from the current\n");
        sb.Append("  feature, the named one lies `<metres>` away in compass direction `<DIR>` (N = north/up;\n");
        sb.Append("  8-wind: N NE E SE S SW W NW). `<size>`: **adjacent** <25 m · **near** <150 m ·\n");
        sb.Append("  **short walk** <500 m · **far** ≥500 m.\n");
        if (hasTerrain)
            sb.Append("- A `[crosses <barrier>]` flag means a barrier (freeway, rail, river) lies between the\n  two features — **not** a walkable hop. **Terrain & barriers** lists water, parks, and\n  barriers with their rough position for orientation.\n");
        if (hasDistricts)
            sb.Append("- **Districts** group features; `spine:` lists them in order along the district's axis;\n  `clustered:` counts minor features not shown individually.\n");
        sb.Append("- Layout is **not to scale** — only the numbers and compass letters are real. Neighbours\n");
        sb.Append("  are straight-line closeness, not road distance.\n\n");
    }

    private static void AppendBounds(StringBuilder sb, double[] b)
    {
        if (b is { Length: 4 })
        {
            string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
            sb.Append("**Bounds:** ").Append(F(b[1])).Append(" N,").Append(F(b[0])).Append(" W → ")
              .Append(F(b[3])).Append(" N,").Append(F(b[2])).Append(" E · **North is up.**\n\n");
        }
    }

    private static void AppendTerrain(StringBuilder sb, List<TerrainEntry> terrain)
    {
        if (terrain.Count == 0) return;
        sb.Append("## Terrain & barriers\n\n");
        foreach (var e in terrain)
            sb.Append("- ").Append(e.Name).Append(" (").Append(e.KindLabel).Append(") · ")
              .Append(e.Position).Append(" · ").Append(e.Note).Append('\n');
        sb.Append('\n');
    }

    private static void AppendDistricts(StringBuilder sb, List<District> districts)
    {
        sb.Append("## Districts\n\n");
        foreach (var d in districts)
        {
            sb.Append("### ").Append(d.Name);
            if (d.Street is not null) sb.Append(" — ").Append(d.Street).Append(" area");
            sb.Append('\n');

            sb.Append("spine: ").Append(d.SpineDir);
            if (d.Street is not null) sb.Append(" along ").Append(d.Street);
            sb.Append(": ").Append(string.Join(",", d.SpineTokens)).Append('\n');

            sb.Append("promoted: ").Append(d.PromotedCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (d.ClusteredCount > 0)
                sb.Append("clustered: ~").Append(d.ClusteredCount.ToString(CultureInfo.InvariantCulture)).Append(" minor\n");
            sb.Append('\n');
        }
    }

    private void AppendConnections(StringBuilder sb, List<PromotedFeature> features, List<Edge> edges)
    {
        var byFrom = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
        foreach (var e in edges)
        {
            if (!byFrom.TryGetValue(e.FromToken, out var list)) byFrom[e.FromToken] = list = new List<Edge>();
            list.Add(e);
        }

        sb.Append("## Connections\n\n```\n");
        for (int i = 0; i < features.Count; i++)
        {
            var f = features[i];
            sb.Append(f.Token).Append(' ').Append(f.Name).Append(" (").Append(f.Category).Append(')');
            if (f.Street is not null) sb.Append(f.StreetApprox ? " · near " : " · on ").Append(f.Street);
            if (f.District is not null) sb.Append(" · ").Append(f.District);
            sb.Append('\n');

            if (byFrom.TryGetValue(f.Token, out var fedges))
                foreach (var e in fedges)
                {
                    sb.Append("   → ").Append(e.ToToken);
                    if (_opts.InlineNeighborName) sb.Append(' ').Append(e.ToName);
                    sb.Append(" — ~").Append(e.Meters.ToString(CultureInfo.InvariantCulture)).Append("m ")
                      .Append(e.Dir).Append(", ").Append(e.Bucket);
                    if (e.Crosses is not null) sb.Append(" [crosses ").Append(e.Crosses).Append(']');
                    sb.Append('\n');
                }
            if (i < features.Count - 1) sb.Append('\n');
        }
        sb.Append("```\n");
    }

    private static string? CrossedBarrier(
        LonLat a, LonLat b, List<(string label, IReadOnlyList<LonLat> line)> barriers)
    {
        string? best = null;
        foreach (var (label, line) in barriers)
            for (int s = 0; s + 1 < line.Count; s++)
                if (Geo.SegmentsCross(a, b, line[s], line[s + 1]))
                {
                    if (best is null || string.CompareOrdinal(label, best) < 0) best = label;
                    break;
                }
        return best;
    }
}
