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

    /// <summary>
    /// Re-render markdown from an existing model using this instance's options (ADR-0011).
    /// The render-only settings change only text, never the model — so this never rebuilds.
    /// </summary>
    public string RenderModel(MapModel model) => Render(model);

    /// <summary>
    /// Refresh every rendered view on the model from this instance's options (ADR-0011): the
    /// whole-area markdown, and — when Chunking is on — the scene-chunk set + manifest (ADR-0016).
    /// Reasons only over already-built data, so it never re-parses. Returns the same instance.
    /// </summary>
    public MapModel Compose(MapModel model)
    {
        model.Markdown = Render(model);
        if (_opts.Chunking) Chunker.Apply(model, _opts);
        else { model.Chunks = new List<Chunk>(); model.Manifest = ""; }
        return model;
    }

    /// <summary>The structured model (incl. the rendered markdown). Used by the Explorer/WASM.</summary>
    public MapModel BuildModel(FeatureCollection fc)
    {
        var anchors = fc.Features
            .Where(f => f.Properties.Kind == "place")
            .Select(f => new Anchor(NameOf(f.Properties), GeoJsonReader.PointOf(f)))
            .ToList();
        bool hasDistricts = anchors.Count > 0;

        var pois = fc.Features.Where(f => f.Properties.Kind == "poi").ToList();
        // Tiered unnamed promotion (ADR-0012): an unnamed feature earns its own token only at
        // landmark tier; unnamed destination/lower features fold into a district's clustered count.
        bool Promoted(Feature f)
        {
            if (!hasDistricts) return true;
            if (f.Properties.Tier == "minor") return false;
            bool named = !string.IsNullOrEmpty(f.Properties.Name);
            return named || f.Properties.Tier == "landmark";
        }
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

        // Barriers split by passability (ADR-0015): road/rail produce a named `[crosses <name>]`
        // flag; water (river/canal) instead counts as water-separation.
        var barrierFeatures = fc.Features.Where(f => f.Properties.Kind == "barrier").ToList();
        var namedBarriers = barrierFeatures
            .Where(f => f.Properties.BarrierClass != "water")
            .Select(f => (label: NameOf(f.Properties), line: GeoJsonReader.LineOf(f)))
            .Where(b => b.line.Count >= 2).ToList();

        // Water geometry that makes a straight-line link not directly walkable: water-area polygon
        // rings, bbox-clipped shoreline LineStrings (ADR-0014), and linear river/canal barriers.
        var waterFeatures = fc.Features.Where(f => f.Properties.Kind == "water").ToList();
        var waterRings = waterFeatures.Where(f => f.Geometry.Type == "Polygon")
            .Select(GeoJsonReader.PolygonOuterOf).Where(r => r.Count >= 3).ToList();
        var waterLines = waterFeatures.Where(f => f.Geometry.Type != "Polygon")
            .Select(GeoJsonReader.LineOf)
            .Concat(barrierFeatures.Where(f => f.Properties.BarrierClass == "water")
                .Select(GeoJsonReader.LineOf))
            .Where(l => l.Count >= 2).ToList();

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
                    Crosses = CrossedBarrier(points[i], points[j], namedBarriers),
                    SeparatedByWater = CrossesWater(points[i], points[j], waterRings, waterLines),
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
        return Compose(model);
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
                // Linear = barriers AND bbox-clipped water/park shorelines (ADR-0014), by geometry.
                bool linear = g.First().Geometry.Type == "LineString";
                var partsLonLat = g.Select(f => linear ? GeoJsonReader.LineOf(f) : GeoJsonReader.PolygonOuterOf(f))
                    .Where(pl => pl.Count > 0).ToList();
                var flat = partsLonLat.SelectMany(pl => pl).ToList();
                string cls = g.First().Properties.BarrierClass ?? "";
                return new
                {
                    g.Key.kind,
                    g.Key.name,
                    kindLabel = g.Key.kind == "barrier" ? "barrier:" + cls : g.Key.kind,
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
        AppendConnections(sb, m.Features, m.Edges, m.Districts);
        return sb.ToString();
    }

    private static string Title(FeatureCollection fc) =>
        string.IsNullOrWhiteSpace(fc.Properties.Title) ? "Untitled area" : fc.Properties.Title;

    private static string NameOf(FeatureProperties p) =>
        !string.IsNullOrEmpty(p.Name) ? p.Name! : Humanize(p.Category);

    /// <summary>
    /// Fallback label for an unnamed feature (ADR-0012): its lowercase humanized category subclass
    /// (`landmark.place_of_worship` → `place of worship`). Lowercase signals "type, not a proper
    /// name" to the LLM. The redundant `(category)` is dropped on these lines (see AppendConnections).
    /// </summary>
    private static string Humanize(string? category)
    {
        if (string.IsNullOrEmpty(category)) return "unnamed";
        int dot = category!.IndexOf('.');
        var sub = dot < 0 ? category : category.Substring(dot + 1);
        return sub.Replace('_', ' ');
    }

    internal static bool IsCategoryFallbackName(string name, string category) =>
        name == Humanize(category);

    // True when the feature sits on its district's dominant street (already named once at the
    // district/spine level), so the per-feature repeat can be dropped (grill 2026-06-30).
    private static bool IsDominantStreet(PromotedFeature f, Dictionary<string, string?> domStreet) =>
        f.District is not null
        && domStreet.TryGetValue(f.District, out var dom)
        && dom is not null
        && string.Equals(f.Street, dom, StringComparison.Ordinal);

    // The behavioral directive (toggleable). Compressed to two dense lines (grill 2026-06-30):
    // instruction only — the always-on reading key (AppendHowToRead) carries the parse legend.
    private static void AppendPreamble(StringBuilder sb)
    {
        sb.Append("<!-- DIRECTIVE PREAMBLE · mode=whole-area -->\n\n");
        sb.Append("**Authoritative map — treat as canon.** Do not invent geography or places not listed\n");
        sb.Append("here; if something is not on the map, it is unknown — say so rather than guessing.\n\n");
        sb.Append("<!-- /DIRECTIVE PREAMBLE -->\n\n");
    }

    // The reading key: always emitted (self-containment), compressed to a few dense lines.
    private static void AppendHowToRead(StringBuilder sb, bool hasDistricts, bool hasTerrain)
    {
        sb.Append("## How to read\n\n");
        sb.Append("- `[token] Name (category)`");
        sb.Append(hasDistricts ? " optionally `· on <street> · <district>`.\n" : ".\n");
        sb.Append("- `→ [token] Name — ~<m>m <DIR>`: the named feature lies ~<m> metres away, compass\n");
        sb.Append("  `<DIR>` (N = up; 8-wind). Straight-line closeness, **not to scale** — only the numbers\n");
        sb.Append("  and letters are real.\n");
        if (hasTerrain)
            sb.Append("- Flags: `[crosses <road>]` a road/rail lies between (cross at a crossing); `[separated by\n  water]` open water lies between (not directly walkable); `stands apart` reached only across water.\n");
        if (hasDistricts)
            sb.Append("- `spine:` orders a district along its axis; `clustered:` counts minor features not listed.\n");
        sb.Append('\n');
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

    private void AppendConnections(StringBuilder sb, List<PromotedFeature> features, List<Edge> edges, List<District> districts)
    {
        // Street hoist (grill 2026-06-30): the District header + spine already name the dominant
        // street, so a feature drops its own `· on <street>` when it matches — off-street features keep it.
        var domStreet = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var d in districts) domStreet[d.Name] = d.Street;

        var byFrom = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
        foreach (var e in edges)
        {
            // bidirectional=off: print each undirected pair once, under the lower (more-important)
            // token. Tokens are equal-width zero-padded, so ordinal compare orders them (ADR-0011).
            if (!_opts.Bidirectional && string.CompareOrdinal(e.FromToken, e.ToToken) > 0) continue;
            if (!byFrom.TryGetValue(e.FromToken, out var list)) byFrom[e.FromToken] = list = new List<Edge>();
            list.Add(e);
        }

        // "Stands apart" (ADR-0015): a feature reachable only across water — *every* incident
        // proximity link is water-separated. Derived from the full edge set (both directions are
        // present in the model regardless of the bidirectional render option), so it is stable
        // even when an edge is printed under the other endpoint.
        var incident = new Dictionary<string, (int total, int water)>(StringComparer.Ordinal);
        foreach (var e in edges)
        {
            incident.TryGetValue(e.FromToken, out var c);
            incident[e.FromToken] = (c.total + 1, c.water + (e.SeparatedByWater ? 1 : 0));
        }
        bool StandsApart(string token) =>
            incident.TryGetValue(token, out var c) && c.total > 0 && c.water == c.total;

        sb.Append("## Connections\n\n```\n");
        for (int i = 0; i < features.Count; i++)
        {
            var f = features[i];
            sb.Append(f.Token).Append(' ').Append(f.Name);
            // Drop the redundant "(category)" when the name *is* the humanized category (ADR-0012).
            if (!IsCategoryFallbackName(f.Name, f.Category)) sb.Append(" (").Append(f.Category).Append(')');
            if (f.Street is not null && !IsDominantStreet(f, domStreet))
                sb.Append(f.StreetApprox ? " · near " : " · on ").Append(f.Street);
            if (f.District is not null) sb.Append(" · ").Append(f.District);
            if (StandsApart(f.Token)) sb.Append(" · stands apart — reached only across water");
            sb.Append('\n');

            if (byFrom.TryGetValue(f.Token, out var fedges))
                foreach (var e in fedges)
                {
                    sb.Append("   → ").Append(e.ToToken);
                    if (_opts.InlineNeighborName) sb.Append(' ').Append(e.ToName);
                    // Metres + bearing only; the size bucket is derivable and dropped (grill 2026-06-30).
                    sb.Append(" — ~").Append(e.Meters.ToString(CultureInfo.InvariantCulture)).Append("m ")
                      .Append(e.Dir);
                    if (e.SeparatedByWater) sb.Append(" [separated by water]");
                    if (e.Crosses is not null) sb.Append(" [crosses ").Append(e.Crosses).Append(']');
                    sb.Append('\n');
                }
            if (i < features.Count - 1) sb.Append('\n');
        }
        sb.Append("```\n");
    }

    /// <summary>
    /// True if the straight line a–b passes through water — a water-area polygon, a clipped
    /// shoreline, or a river/canal (ADR-0015). The honest "not directly walkable" signal.
    /// </summary>
    private static bool CrossesWater(
        LonLat a, LonLat b,
        List<IReadOnlyList<LonLat>> rings, List<IReadOnlyList<LonLat>> lines)
    {
        foreach (var ring in rings)
            if (Geo.SegmentCrossesPolygon(a, b, ring)) return true;
        foreach (var line in lines)
            for (int s = 0; s + 1 < line.Count; s++)
                if (Geo.SegmentsCross(a, b, line[s], line[s + 1])) return true;
        return false;
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
