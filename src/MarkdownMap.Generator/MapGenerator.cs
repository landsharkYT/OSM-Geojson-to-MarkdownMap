using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MarkdownMap.Contract;

namespace MarkdownMap.Generator;

/// <summary>
/// Stage 2: turns a contract GeoJSON FeatureCollection into a MarkdownMap. Renders
/// Directive Preamble + How-to-read + Bounds + (Districts) + Connections. Districts,
/// promotion/clustering, and spines are computed here from `place` anchors (ADR-0007);
/// see docs/impl-step-3.md. OSM-agnostic — reasons only over the normalized schema.
/// </summary>
public sealed class MapGenerator
{
    private readonly GeneratorOptions _opts;

    public MapGenerator(GeneratorOptions? options = null) => _opts = options ?? GeneratorOptions.Default;

    public string Generate(FeatureCollection fc)
    {
        var anchors = fc.Features
            .Where(f => f.Properties.Kind == "place")
            .Select(f => new Anchor(NameOf(f.Properties), GeoJsonReader.PointOf(f)))
            .ToList();
        bool hasDistricts = anchors.Count > 0;

        var pois = fc.Features.Where(f => f.Properties.Kind == "poi").ToList();

        // Promotion: with districts, minor tier is clustered; without, everything is a token.
        bool Promoted(Feature f) => !hasDistricts || f.Properties.Tier != "minor";
        var promoted = pois.Where(Promoted)
            .OrderByDescending(f => f.Properties.Importance ?? 0)
            .ThenBy(f => f.Properties.OsmId, StringComparer.Ordinal)
            .ToList();
        var minors = pois.Where(f => !Promoted(f)).ToList();

        var points = promoted.Select(GeoJsonReader.PointOf).ToList();
        int pad = Math.Max(2, promoted.Count.ToString(CultureInfo.InvariantCulture).Length);
        string Token(int i) => "[" + (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(pad, '0') + "]";

        var adjacency = ProximityGraph.Build(points, _opts.NeighborsPerFeature);
        var district = points.Select(p => Districts.Nearest(p, anchors)).ToList(); // aligned to promoted

        var sb = new StringBuilder();
        sb.Append("# MARKDOWNMAP — ").Append(Title(fc)).Append("\n\n");
        if (_opts.DirectivePreamble) AppendPreamble(sb);
        AppendHowToRead(sb, hasDistricts);
        AppendBounds(sb, fc);
        if (hasDistricts) AppendDistricts(sb, promoted, points, district, minors, anchors, Token);
        AppendConnections(sb, promoted, points, district, adjacency, Token);
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

    private static void AppendHowToRead(StringBuilder sb, bool hasDistricts)
    {
        sb.Append("## How to read this map\n\n");
        sb.Append("- **Feature header** = `[token] Name (category)`");
        sb.Append(hasDistricts ? ", optionally `· on <street> · <district>`.\n" : ".\n");
        sb.Append("- **Connection** = `→ [token] Name — ~<metres>m <DIR>, <size>`, read as: from the current\n");
        sb.Append("  feature, the named one lies `<metres>` away in compass direction `<DIR>` (N = north/up;\n");
        sb.Append("  8-wind: N NE E SE S SW W NW). `<size>`: **adjacent** <25 m · **near** <150 m ·\n");
        sb.Append("  **short walk** <500 m · **far** ≥500 m.\n");
        if (hasDistricts)
            sb.Append("- **Districts** group features; `spine:` lists them in order along the district's axis;\n  `clustered:` counts minor features not shown individually.\n");
        sb.Append("- Layout is **not to scale** — only the numbers and compass letters are real. Neighbours\n");
        sb.Append("  are straight-line closeness, not road distance.\n\n");
    }

    private static void AppendBounds(StringBuilder sb, FeatureCollection fc)
    {
        var b = fc.Properties.Bounds;
        if (b is { Length: 4 })
        {
            string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
            sb.Append("**Bounds:** ").Append(F(b[1])).Append(" N,").Append(F(b[0])).Append(" W → ")
              .Append(F(b[3])).Append(" N,").Append(F(b[2])).Append(" E · **North is up.**\n\n");
        }
    }

    private void AppendDistricts(
        StringBuilder sb, List<Feature> promoted, List<LonLat> points, List<string?> district,
        List<Feature> minors, List<Anchor> anchors, Func<int, string> token)
    {
        // promoted indices grouped by district
        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int i = 0; i < promoted.Count; i++)
        {
            var d = district[i]!;
            if (!groups.TryGetValue(d, out var list)) groups[d] = list = new List<int>();
            list.Add(i);
        }
        // minor counts per district
        var minorCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var m in minors)
        {
            var d = Districts.Nearest(GeoJsonReader.PointOf(m), anchors)!;
            minorCounts[d] = minorCounts.TryGetValue(d, out var c) ? c + 1 : 1;
        }

        sb.Append("## Districts\n\n");
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

            // Spine is a skeleton: the top-importance "key" features, listed in axis order.
            var keyLocal = new HashSet<int>(idxs
                .Select((gi, local) => (local, imp: promoted[gi].Properties.Importance ?? 0))
                .OrderByDescending(t => t.imp).ThenBy(t => t.local)
                .Take(_opts.SpineKeyCap).Select(t => t.local));
            var spineTokens = order.Where(o => keyLocal.Contains(o)).Select(o => token(idxs[o]));

            sb.Append("### ").Append(g.Key);
            if (domStreet is not null) sb.Append(" — ").Append(domStreet).Append(" area");
            sb.Append('\n');

            sb.Append("spine: ").Append(dir);
            if (domStreet is not null) sb.Append(" along ").Append(domStreet);
            sb.Append(": ").Append(string.Join(",", spineTokens)).Append('\n');

            sb.Append("promoted: ").Append(idxs.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');

            if (minorCounts.TryGetValue(g.Key, out var mc) && mc > 0)
                sb.Append("clustered: ~").Append(mc.ToString(CultureInfo.InvariantCulture)).Append(" minor\n");
            sb.Append('\n');
        }
    }

    private void AppendConnections(
        StringBuilder sb, List<Feature> promoted, List<LonLat> points, List<string?> district,
        IReadOnlyList<IReadOnlyList<int>> adjacency, Func<int, string> token)
    {
        sb.Append("## Connections\n\n```\n");
        for (int i = 0; i < promoted.Count; i++)
        {
            var p = promoted[i].Properties;
            sb.Append(token(i)).Append(' ').Append(NameOf(p)).Append(" (").Append(p.Category).Append(')');
            if (!string.IsNullOrEmpty(p.Street))
                sb.Append(p.StreetApprox == true ? " · near " : " · on ").Append(p.Street);
            if (district[i] is not null)
                sb.Append(" · ").Append(district[i]);
            sb.Append('\n');

            var neighbours = adjacency[i]
                .Select(j => (j, dist: Geo.HaversineMeters(points[i], points[j])))
                .OrderBy(t => t.dist).ThenBy(t => t.j);
            foreach (var (j, dist) in neighbours)
            {
                sb.Append("   → ").Append(token(j));
                if (_opts.InlineNeighborName) sb.Append(' ').Append(NameOf(promoted[j].Properties));
                sb.Append(" — ~").Append(Geo.RoundMeters(dist).ToString(CultureInfo.InvariantCulture)).Append("m ")
                  .Append(Geo.EightWind(points[i], points[j])).Append(", ")
                  .Append(Geo.Bucket(dist, _opts.Buckets)).Append('\n');
            }
            if (i < promoted.Count - 1) sb.Append('\n');
        }
        sb.Append("```\n");
    }
}
