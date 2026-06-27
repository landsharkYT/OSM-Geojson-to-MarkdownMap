using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MarkdownMap.Contract;

namespace MarkdownMap.Generator;

/// <summary>
/// Stage 2, v0: turns a POI-only contract GeoJSON FeatureCollection into a MarkdownMap of
/// Directive Preamble + How-to-read + Bounds + Connections (see docs/impl-step-2.md).
/// OSM-agnostic — reasons only over the normalized schema.
/// </summary>
public sealed class MapGenerator
{
    private readonly GeneratorOptions _opts;

    public MapGenerator(GeneratorOptions? options = null) => _opts = options ?? GeneratorOptions.Default;

    public string Generate(FeatureCollection fc)
    {
        // 1. POI features only, ordered: importance desc, then osmId (token [01] = most important).
        var features = fc.Features
            .Where(f => f.Properties.Kind == "poi")
            .OrderByDescending(f => f.Properties.Importance ?? 0)
            .ThenBy(f => f.Properties.OsmId, StringComparer.Ordinal)
            .ToList();

        var points = features.Select(GeoJsonReader.PointOf).ToList();
        int pad = Math.Max(2, features.Count.ToString(CultureInfo.InvariantCulture).Length);
        string Token(int i) => "[" + (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(pad, '0') + "]";

        var adjacency = ProximityGraph.Build(points, _opts.NeighborsPerFeature);

        var sb = new StringBuilder();
        sb.Append("# MARKDOWNMAP — ").Append(Title(fc)).Append("\n\n");
        if (_opts.DirectivePreamble) AppendPreamble(sb);
        AppendHowToRead(sb);
        AppendBounds(sb, fc);
        AppendConnections(sb, features, points, adjacency, Token);
        return sb.ToString();
    }

    private static string Title(FeatureCollection fc) =>
        string.IsNullOrWhiteSpace(fc.Properties.Title) ? "Untitled area" : fc.Properties.Title;

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

    private static void AppendHowToRead(StringBuilder sb)
    {
        sb.Append("## How to read this map\n\n");
        sb.Append("- **Feature header** = `[token] Name (category)`.\n");
        sb.Append("- **Connection** = `→ [token] Name — ~<metres>m <DIR>, <size>`, read as: from the current\n");
        sb.Append("  feature, the named one lies `<metres>` away in compass direction `<DIR>` (N = north/up;\n");
        sb.Append("  8-wind: N NE E SE S SW W NW). `<size>`: **adjacent** <25 m · **near** <150 m ·\n");
        sb.Append("  **short walk** <500 m · **far** ≥500 m.\n");
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

    private void AppendConnections(
        StringBuilder sb, List<Feature> features, List<LonLat> points,
        IReadOnlyList<IReadOnlyList<int>> adjacency, Func<int, string> token)
    {
        sb.Append("## Connections\n\n```\n");
        for (int i = 0; i < features.Count; i++)
        {
            var p = features[i].Properties;
            sb.Append(token(i)).Append(' ').Append(Name(p)).Append(" (").Append(p.Category).Append(")\n");

            var neighbours = adjacency[i]
                .Select(j => (j, dist: Geo.HaversineMeters(points[i], points[j])))
                .OrderBy(t => t.dist)
                .ThenBy(t => t.j);
            foreach (var (j, dist) in neighbours)
            {
                sb.Append("   → ").Append(token(j));
                if (_opts.InlineNeighborName) sb.Append(' ').Append(Name(features[j].Properties));
                sb.Append(" — ~").Append(Geo.RoundMeters(dist).ToString(CultureInfo.InvariantCulture)).Append("m ")
                  .Append(Geo.EightWind(points[i], points[j])).Append(", ")
                  .Append(Geo.Bucket(dist, _opts.Buckets)).Append('\n');
            }
            if (i < features.Count - 1) sb.Append('\n');
        }
        sb.Append("```\n");
    }

    private static string Name(FeatureProperties p) =>
        string.IsNullOrEmpty(p.Name) ? "unnamed" : p.Name!;
}
