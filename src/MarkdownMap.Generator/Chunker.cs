using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MarkdownMap.Generator;

/// <summary>
/// Scene-chunk retrieval (ADR-0016). Partitions a built <see cref="MapModel"/> into self-contained
/// scene-chunks — one per District, spine-split when a District exceeds the scene-size target — and
/// renders each as its own document plus an index manifest. Render-only: reasons purely over the
/// model's features, edges, districts, and terrain (no re-parse, ADR-0011). Global stable tokens are
/// kept as-is so a feature reads `[42]` in its own chunk and in any exit that points at it.
/// </summary>
internal static class Chunker
{
    /// <summary>Populate <paramref name="m"/>.Chunks and .Manifest from the built model.</summary>
    public static void Apply(MapModel m, GeneratorOptions opts)
    {
        m.Chunks = new List<Chunk>();
        m.Manifest = "";
        if (m.Features.Count == 0) return;

        var byToken = m.Features.ToDictionary(f => f.Token, StringComparer.Ordinal);

        // 1. Partition into named groups: District membership when we have anchors, else the
        //    proximity graph's connected components (the no-`place` fallback, ADR-0016 §2).
        var groups = m.Districts.Count > 0
            ? PartitionByDistrict(m)
            : PartitionByComponents(m);

        // 2. Spine-split oversized groups into contiguous segments and materialise chunks.
        var chunks = new List<Chunk>();
        foreach (var (baseName, members) in groups)
            chunks.AddRange(SplitToChunks(baseName, members, opts.SceneSize));
        EnsureUniqueSlugs(chunks);

        // token → chunk, so cross-chunk edges become off-map exits and minors land in a chunk.
        var chunkOf = new Dictionary<string, Chunk>(StringComparer.Ordinal);
        foreach (var c in chunks)
            foreach (var t in c.Tokens) chunkOf[t] = c;

        var exits = ComputeExits(m, chunkOf, byToken);
        var clustered = ClusterMinors(m, chunks, chunkOf);
        var standsApart = StandsApartTokens(m.Edges);

        // 3. Render each chunk.
        foreach (var c in chunks)
            c.Markdown = RenderChunk(c, m, opts, byToken, chunkOf, exits, clustered, standsApart);

        m.Chunks = chunks;
        m.Manifest = RenderManifest(chunks, m.Title);
    }

    // ----- partitioning -----

    private static List<(string name, List<PromotedFeature> members)> PartitionByDistrict(MapModel m)
    {
        var result = new List<(string, List<PromotedFeature>)>();
        foreach (var d in m.Districts) // m.Districts is already in a stable order
        {
            var members = m.Features.Where(f => f.District == d.Name).ToList();
            if (members.Count > 0) result.Add((d.Name, members));
        }
        // Any feature with no district (shouldn't happen when anchors exist) folds into one group.
        var orphans = m.Features.Where(f => f.District is null || !m.Districts.Any(d => d.Name == f.District)).ToList();
        if (orphans.Count > 0) result.Add(("Outlying", orphans));
        return result;
    }

    private static List<(string name, List<PromotedFeature> members)> PartitionByComponents(MapModel m)
    {
        var idx = m.Features.Select((f, i) => (f.Token, i)).ToDictionary(t => t.Token, t => t.i, StringComparer.Ordinal);
        var parent = Enumerable.Range(0, m.Features.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        void Union(int a, int b) { parent[Find(a)] = Find(b); }
        foreach (var e in m.Edges)
            if (idx.TryGetValue(e.FromToken, out var a) && idx.TryGetValue(e.ToToken, out var b)) Union(a, b);

        var comps = new Dictionary<int, List<PromotedFeature>>();
        for (int i = 0; i < m.Features.Count; i++)
        {
            int r = Find(i);
            if (!comps.TryGetValue(r, out var list)) comps[r] = list = new List<PromotedFeature>();
            list.Add(m.Features[i]);
        }
        // Larger components first; name each after its most important feature ("Around <name>").
        return comps.Values
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Min(f => f.Token), StringComparer.Ordinal)
            .Select(g => ("Around " + Top(g).Name, g))
            .ToList();
    }

    // ----- spine splitting -----

    private static IEnumerable<Chunk> SplitToChunks(string baseName, List<PromotedFeature> members, int sceneSize)
    {
        int target = Math.Max(1, sceneSize);
        if (members.Count <= target)
        {
            yield return MakeChunk(baseName, members);
            yield break;
        }

        var pts = members.Select(f => new LonLat(f.Lon, f.Lat)).ToList();
        var (_, order) = Spine.Compute(pts);
        var ordered = order.Select(i => members[i]).ToList();

        int k = (int)Math.Ceiling(members.Count / (double)target);
        int baseSize = members.Count / k, extra = members.Count % k; // spread the remainder over the first segments
        double cLon = members.Average(f => f.Lon), cLat = members.Average(f => f.Lat);
        var centroid = new LonLat(cLon, cLat);

        var used = new HashSet<string>(StringComparer.Ordinal);
        int pos = 0;
        for (int s = 0; s < k; s++)
        {
            int size = baseSize + (s < extra ? 1 : 0);
            var seg = ordered.Skip(pos).Take(size).ToList();
            pos += size;
            // Segment suffix = compass bearing of the segment's centre from the District centre,
            // so an N–S District reads "· N" / "· S" (ADR-0016). Dedup keeps it unique.
            var segCentroid = new LonLat(seg.Average(f => f.Lon), seg.Average(f => f.Lat));
            string suffix = Geo.EightWind(centroid, segCentroid);
            string label = suffix;
            for (int n = 2; !used.Add(label); n++) label = suffix + " " + n.ToString(CultureInfo.InvariantCulture);
            yield return MakeChunk(baseName + " · " + label, seg);
        }
    }

    private static Chunk MakeChunk(string name, List<PromotedFeature> members)
    {
        var anchor = Top(members);
        return new Chunk
        {
            Name = name,
            Slug = Slugify(name),
            AnchorToken = anchor.Token,
            AnchorName = anchor.Name,
            Bounds = new[]
            {
                members.Min(f => f.Lon), members.Min(f => f.Lat),
                members.Max(f => f.Lon), members.Max(f => f.Lat),
            },
            Tokens = members.Select(f => f.Token).ToList(),
        };
    }

    private static PromotedFeature Top(IEnumerable<PromotedFeature> g) =>
        g.OrderByDescending(f => f.Importance).ThenBy(f => f.Token, StringComparer.Ordinal).First();

    // The chunk's most common street (ties broken by name), or null if none — mirrors how a
    // District picks its dominant street, so the hoist is consistent between the two modes.
    private static string? DominantStreet(IEnumerable<PromotedFeature> members) =>
        members.Select(f => f.Street).Where(s => !string.IsNullOrEmpty(s))
            .GroupBy(s => s!, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key).FirstOrDefault();

    // ----- off-map exits -----

    // For each chunk, the best (shortest) edge to each *other* chunk: the concrete feature you'd
    // walk to just across the boundary, with its bearing and distance (ADR-0016 §3).
    private sealed class Exit { public string Neighbour = ""; public string Dir = ""; public int Meters; public string ViaToken = ""; public string ViaName = ""; }

    private static Dictionary<string, List<Exit>> ComputeExits(
        MapModel m, Dictionary<string, Chunk> chunkOf, Dictionary<string, PromotedFeature> byToken)
    {
        var result = new Dictionary<string, List<Exit>>(StringComparer.Ordinal);
        // best edge per (sourceChunk, neighbourChunk)
        var best = new Dictionary<(string src, string nb), Exit>();
        foreach (var e in m.Edges)
        {
            if (!chunkOf.TryGetValue(e.FromToken, out var from) || !chunkOf.TryGetValue(e.ToToken, out var to)) continue;
            if (ReferenceEquals(from, to)) continue; // intra-chunk
            var key = (from.Name, to.Name);
            if (best.TryGetValue(key, out var cur) && cur.Meters <= e.Meters) continue;
            best[key] = new Exit
            {
                Neighbour = to.Name, Dir = e.Dir, Meters = e.Meters,
                ViaToken = e.ToToken, ViaName = byToken.TryGetValue(e.ToToken, out var f) ? f.Name : e.ToName,
            };
        }
        foreach (var kv in best)
        {
            if (!result.TryGetValue(kv.Key.src, out var list)) result[kv.Key.src] = list = new List<Exit>();
            list.Add(kv.Value);
        }
        foreach (var list in result.Values)
            list.Sort((a, b) => string.CompareOrdinal(a.Neighbour, b.Neighbour));
        return result;
    }

    // ----- minors -----

    private static Dictionary<string, int> ClusterMinors(MapModel m, List<Chunk> chunks, Dictionary<string, Chunk> chunkOf)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (m.Minors.Count == 0 || chunks.Count == 0) return counts;
        var feat = m.Features;
        foreach (var minor in m.Minors)
        {
            // assign to the chunk holding the promoted feature nearest the minor
            var mp = new LonLat(minor.Lon, minor.Lat);
            string? bestTok = null; double bestD = double.MaxValue;
            foreach (var f in feat)
            {
                double d = Geo.HaversineMeters(mp, new LonLat(f.Lon, f.Lat));
                if (d < bestD) { bestD = d; bestTok = f.Token; }
            }
            if (bestTok is not null && chunkOf.TryGetValue(bestTok, out var c))
                counts[c.Name] = counts.TryGetValue(c.Name, out var n) ? n + 1 : 1;
        }
        return counts;
    }

    private static HashSet<string> StandsApartTokens(List<Edge> edges)
    {
        var incident = new Dictionary<string, (int total, int water)>(StringComparer.Ordinal);
        foreach (var e in edges)
        {
            incident.TryGetValue(e.FromToken, out var c);
            incident[e.FromToken] = (c.total + 1, c.water + (e.SeparatedByWater ? 1 : 0));
        }
        return new HashSet<string>(
            incident.Where(kv => kv.Value.total > 0 && kv.Value.water == kv.Value.total).Select(kv => kv.Key),
            StringComparer.Ordinal);
    }

    // ----- chunk rendering -----

    private static string RenderChunk(
        Chunk c, MapModel m, GeneratorOptions opts,
        Dictionary<string, PromotedFeature> byToken, Dictionary<string, Chunk> chunkOf,
        Dictionary<string, List<Exit>> exits, Dictionary<string, int> clustered, HashSet<string> standsApart)
    {
        var sb = new StringBuilder();
        sb.Append("# SCENE-CHUNK — ").Append(c.Name).Append(" · ").Append(m.Title).Append("\n\n");

        // Behavioral directive (toggleable), compressed to two lines (grill 2026-06-30).
        if (opts.DirectivePreamble)
        {
            sb.Append("<!-- SCENE-CHUNK · area=").Append(c.Name).Append(" -->\n\n");
            sb.Append("**You are in ").Append(c.Name).Append("** (anchored on ").Append(c.AnchorToken)
              .Append(' ').Append(c.AnchorName).Append("), one area of *").Append(m.Title)
              .Append("* — canon. Reach\n");
            sb.Append("other areas only via **Ways out** below; do not invent what lies beyond these bounds.\n\n");
            sb.Append("<!-- /SCENE-CHUNK -->\n\n");
        }

        // Reading key: always on (self-containment), compressed.
        sb.Append("## How to read\n\n");
        sb.Append("- `[token] Name (category)` a local feature; `→ [token] — ~<m>m <DIR>` a straight-line\n");
        sb.Append("  hop (N = up; 8-wind). `[crosses <road>]` road/rail between; `[separated by water]` water\n");
        sb.Append("  between. **Not to scale.** **Ways out** are the only routes to other areas.\n\n");

        AppendBounds(sb, c.Bounds);
        AppendLocalTerrain(sb, m.Terrain, c.Bounds);

        // Features + intra-chunk connections.
        var inChunk = new HashSet<string>(c.Tokens, StringComparer.Ordinal);
        var byFrom = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
        foreach (var e in m.Edges)
        {
            if (!inChunk.Contains(e.FromToken) || !inChunk.Contains(e.ToToken)) continue; // intra-chunk only
            if (!opts.Bidirectional && string.CompareOrdinal(e.FromToken, e.ToToken) > 0) continue;
            if (!byFrom.TryGetValue(e.FromToken, out var list)) byFrom[e.FromToken] = list = new List<Edge>();
            list.Add(e);
        }

        // Street hoist (grill 2026-06-30): name the chunk's dominant street once on the heading; a
        // feature drops its own `· on <street>` when it matches, off-street features keep theirs.
        var members = c.Tokens.Select(t => byToken[t]);
        string? dom = DominantStreet(members);
        sb.Append("## Features");
        if (dom is not null) sb.Append(" · ").Append(dom).Append(" area");
        sb.Append("\n\n```\n");
        for (int i = 0; i < c.Tokens.Count; i++)
        {
            var f = byToken[c.Tokens[i]];
            sb.Append(f.Token).Append(' ').Append(f.Name);
            if (!MapGenerator.IsCategoryFallbackName(f.Name, f.Category)) sb.Append(" (").Append(f.Category).Append(')');
            if (f.Street is not null && !string.Equals(f.Street, dom, StringComparison.Ordinal))
                sb.Append(f.StreetApprox ? " · near " : " · on ").Append(f.Street);
            if (standsApart.Contains(f.Token)) sb.Append(" · stands apart — reached only across water");
            sb.Append('\n');

            if (byFrom.TryGetValue(f.Token, out var fedges))
                foreach (var e in fedges)
                {
                    sb.Append("   → ").Append(e.ToToken);
                    if (opts.InlineNeighborName) sb.Append(' ').Append(e.ToName);
                    // Metres + bearing only; size bucket dropped (grill 2026-06-30).
                    sb.Append(" — ~").Append(e.Meters.ToString(CultureInfo.InvariantCulture)).Append("m ")
                      .Append(e.Dir);
                    if (e.SeparatedByWater) sb.Append(" [separated by water]");
                    if (e.Crosses is not null) sb.Append(" [crosses ").Append(e.Crosses).Append(']');
                    sb.Append('\n');
                }
            if (i < c.Tokens.Count - 1) sb.Append('\n');
        }
        sb.Append("```\n");
        if (clustered.TryGetValue(c.Name, out var minors) && minors > 0)
            sb.Append("\nclustered: ~").Append(minors.ToString(CultureInfo.InvariantCulture)).Append(" minor\n");

        // Ways out.
        sb.Append("\n## Ways out\n\n");
        if (exits.TryGetValue(c.Name, out var es) && es.Count > 0)
        {
            c.Neighbours = es.Select(e => e.Neighbour).ToList();
            foreach (var e in es)
                sb.Append("- → ").Append(e.Dir).Append(" toward ").Append(e.Neighbour)
                  .Append(" (off-map): via ").Append(e.ViaToken).Append(' ').Append(e.ViaName)
                  .Append(", ~").Append(e.Meters.ToString(CultureInfo.InvariantCulture)).Append("m\n");
        }
        else
        {
            c.Neighbours = new List<string>();
            sb.Append("- (none — this area is self-contained in the extract.)\n");
        }
        return sb.ToString();
    }

    private static void AppendBounds(StringBuilder sb, double[] b)
    {
        if (b is not { Length: 4 }) return;
        string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        sb.Append("**Bounds:** ").Append(F(b[1])).Append(" N,").Append(F(b[0])).Append(" W → ")
          .Append(F(b[3])).Append(" N,").Append(F(b[2])).Append(" E · **North is up.**\n\n");
    }

    private static void AppendLocalTerrain(StringBuilder sb, List<TerrainEntry> terrain, double[] bounds)
    {
        if (terrain.Count == 0 || bounds is not { Length: 4 }) return;
        var local = terrain.Where(t => TerrainTouches(t, bounds)).ToList();
        if (local.Count == 0) return;
        sb.Append("## Terrain & barriers\n\n");
        foreach (var e in local)
            sb.Append("- ").Append(e.Name).Append(" (").Append(e.KindLabel).Append(") · ")
              .Append(e.Position).Append(" · ").Append(e.Note).Append('\n');
        sb.Append('\n');
    }

    // A terrain feature is "local" to a chunk if its bounding box overlaps the chunk's bounds.
    private static bool TerrainTouches(TerrainEntry t, double[] b)
    {
        double minLon = double.MaxValue, minLat = double.MaxValue, maxLon = double.MinValue, maxLat = double.MinValue;
        foreach (var part in t.Parts)
            foreach (var p in part)
            {
                if (p[0] < minLon) minLon = p[0];
                if (p[0] > maxLon) maxLon = p[0];
                if (p[1] < minLat) minLat = p[1];
                if (p[1] > maxLat) maxLat = p[1];
            }
        if (minLon > maxLon) return false; // no points
        return minLon <= b[2] && maxLon >= b[0] && minLat <= b[3] && maxLat >= b[1];
    }

    // ----- manifest -----

    private static string RenderManifest(List<Chunk> chunks, string title)
    {
        var sb = new StringBuilder();
        sb.Append("# SCENE-CHUNK MAP — ").Append(title).Append("\n\n");
        sb.Append("*").Append(title).Append("* is split into ").Append(chunks.Count.ToString(CultureInfo.InvariantCulture))
          .Append(" self-contained scene-chunks for focused, low-token retrieval. Load only the chunk the\n");
        sb.Append("party is in; each lists concrete **ways out** to its neighbours. The files are in this archive.\n\n");
        sb.Append("| Area | File | Anchor | Bounds (S,W → N,E) | Neighbours |\n");
        sb.Append("|------|------|--------|--------------------|------------|\n");
        string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        foreach (var c in chunks)
        {
            string bounds = c.Bounds is { Length: 4 }
                ? F(c.Bounds[1]) + " N," + F(c.Bounds[0]) + " W → " + F(c.Bounds[3]) + " N," + F(c.Bounds[2]) + " E"
                : "—";
            string neighbours = c.Neighbours.Count > 0 ? string.Join(", ", c.Neighbours) : "—";
            sb.Append("| ").Append(c.Name).Append(" | ").Append(c.Slug).Append(".md | ")
              .Append(c.AnchorToken).Append(' ').Append(c.AnchorName).Append(" | ")
              .Append(bounds).Append(" | ").Append(neighbours).Append(" |\n");
        }
        return sb.ToString();
    }

    // ----- slugs -----

    private static string Slugify(string name)
    {
        var sb = new StringBuilder(name.Length);
        bool prevHyphen = false;
        foreach (char ch in name.ToLowerInvariant())
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) { sb.Append(ch); prevHyphen = false; }
            else if (!prevHyphen && sb.Length > 0) { sb.Append('-'); prevHyphen = true; }
        }
        var s = sb.ToString().TrimEnd('-');
        return s.Length == 0 ? "chunk" : s;
    }

    private static void EnsureUniqueSlugs(List<Chunk> chunks)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in chunks)
        {
            if (seen.TryGetValue(c.Slug, out var n))
            {
                seen[c.Slug] = ++n;
                c.Slug = c.Slug + "-" + n.ToString(CultureInfo.InvariantCulture);
            }
            else seen[c.Slug] = 1;
        }
    }
}
