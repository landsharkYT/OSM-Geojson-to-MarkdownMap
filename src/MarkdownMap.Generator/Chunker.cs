using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MarkdownMap.Generator;

/// <summary>
/// Scene-chunk retrieval (ADR-0016). Partitions a built <see cref="MapModel"/> into self-contained
/// scene-chunks — one per District, or a sub-area when a District exceeds the scene-size target,
/// subdivided by <b>density-gap bisection</b> (ADR-0017) — and renders each as its own document plus
/// an index manifest. Render-only: reasons purely over the model's features, edges, districts, and
/// terrain (no re-parse, ADR-0011). Global stable tokens are kept as-is so a feature reads `[42]` in
/// its own chunk and in any exit that points at it.
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

        // 2. Subdivide oversized groups (density-gap bisection, ADR-0017) into sub-areas.
        var chunks = new List<Chunk>();
        foreach (var (baseName, members) in groups)
            chunks.AddRange(SplitGroup(baseName, members, opts.SceneSize, m.Edges));
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

    // ----- subdivision (density-gap bisection, ADR-0017) -----

    private static IEnumerable<Chunk> SplitGroup(
        string baseName, List<PromotedFeature> members, int sceneSize, List<Edge> allEdges)
    {
        int target = Math.Max(1, sceneSize);
        if (members.Count <= target) return new[] { MakeChunk(baseName, members) };

        // Recursively cut at the widest density gap on the longer cardinal axis until pieces fit.
        var pieces = new List<List<PromotedFeature>>();
        Bisect(members, target, pieces);

        // Repair orphans: a feature with no same-district proximity neighbour in its own piece is
        // merged into the piece holding its nearest same-district neighbour (ADR-0017).
        RepairOrphans(pieces, members, allEdges);
        pieces = pieces.Where(p => p.Count > 0).ToList();

        // Name each piece by its octant within the District; disambiguate collisions with the
        // piece's key feature. Order pieces stably first so naming is deterministic.
        var districtCentroid = new LonLat(members.Average(f => f.Lon), members.Average(f => f.Lat));
        pieces = pieces.OrderBy(p => Top(p).Token, StringComparer.Ordinal).ToList();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var chunks = new List<Chunk>(pieces.Count);
        foreach (var piece in pieces)
        {
            var c = new LonLat(piece.Average(f => f.Lon), piece.Average(f => f.Lat));
            string octant = Geo.EightWind(districtCentroid, c);
            string label = used.Add(octant) ? octant : octant + " — " + Top(piece).Name;
            used.Add(label);
            chunks.Add(MakeChunk(baseName + " · " + label, piece));
        }
        return chunks;
    }

    // Recursive axis-aligned binary space partition. Each level splits the subset at its widest gap
    // along the longer cardinal axis (metric extent); with no dominant gap it splits at the median,
    // so uniform blobs balance instead of shedding one feature at a time. Leaves have disjoint bboxes.
    private static void Bisect(List<PromotedFeature> pts, int target, List<List<PromotedFeature>> outp)
    {
        if (pts.Count <= target) { outp.Add(pts); return; }

        double clat = pts.Average(p => p.Lat);
        double mPerLon = 111_320.0 * Math.Cos(clat * Math.PI / 180.0);
        double spanX = (pts.Max(p => p.Lon) - pts.Min(p => p.Lon)) * mPerLon;
        double spanY = (pts.Max(p => p.Lat) - pts.Min(p => p.Lat)) * 110_540.0;
        bool byLon = spanX >= spanY;

        var sorted = (byLon
            ? pts.OrderBy(p => p.Lon).ThenBy(p => p.Lat).ThenBy(p => p.Token, StringComparer.Ordinal)
            : pts.OrderBy(p => p.Lat).ThenBy(p => p.Lon).ThenBy(p => p.Token, StringComparer.Ordinal)).ToList();
        double Coord(PromotedFeature f) => byLon ? f.Lon : f.Lat;

        var gaps = new double[sorted.Count - 1];
        for (int i = 1; i < sorted.Count; i++) gaps[i - 1] = Coord(sorted[i]) - Coord(sorted[i - 1]);
        double median = gaps.OrderBy(g => g).ToArray()[gaps.Length / 2];

        // Balance guard (ADR-0017): only consider cuts that leave BOTH sides substantial (≥ a
        // quarter-ish of the piece), so dense data can't shed size-1/2 slivers. Among those, take the
        // widest gap — but only if it's a real seam (≥2× median); otherwise split at the median.
        int minSide = Math.Max(2, sorted.Count / 5);
        int bestIdx = -1; double bestGap = -1;
        for (int i = minSide; i <= sorted.Count - minSide; i++)
            if (gaps[i - 1] > bestGap) { bestGap = gaps[i - 1]; bestIdx = i; }
        int split = (bestIdx > 0 && median > 0 && bestGap >= 2.0 * median) ? bestIdx : sorted.Count / 2;

        Bisect(sorted.GetRange(0, split), target, outp);
        Bisect(sorted.GetRange(split, sorted.Count - split), target, outp);
    }

    private static void RepairOrphans(
        List<List<PromotedFeature>> pieces, List<PromotedFeature> members, List<Edge> allEdges)
    {
        var memberTokens = new HashSet<string>(members.Select(f => f.Token), StringComparer.Ordinal);
        var pieceOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < pieces.Count; i++)
            foreach (var f in pieces[i]) pieceOf[f.Token] = i;

        // Same-district proximity neighbours (both directions), nearest first.
        var neighbours = new Dictionary<string, List<(string tok, int m)>>(StringComparer.Ordinal);
        void Add(string a, string b, int m)
        {
            if (!memberTokens.Contains(a) || !memberTokens.Contains(b)) return;
            if (!neighbours.TryGetValue(a, out var l)) neighbours[a] = l = new List<(string, int)>();
            l.Add((b, m));
        }
        foreach (var e in allEdges) { Add(e.FromToken, e.ToToken, e.Meters); Add(e.ToToken, e.FromToken, e.Meters); }

        foreach (var f in members)
        {
            if (!neighbours.TryGetValue(f.Token, out var nbrs) || nbrs.Count == 0) continue; // no repair possible
            int here = pieceOf[f.Token];
            if (nbrs.Any(n => pieceOf.TryGetValue(n.tok, out var p) && p == here)) continue; // not an orphan
            var nearest = nbrs.OrderBy(n => n.m).First(n => pieceOf.ContainsKey(n.tok));
            int dest = pieceOf[nearest.tok];
            pieces[here].Remove(f);
            pieces[dest].Add(f);
            pieceOf[f.Token] = dest;
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

    private static Dictionary<string, (int Total, int Props, List<NamedMinor> Named)> ClusterMinors(
        MapModel m, List<Chunk> chunks, Dictionary<string, Chunk> chunkOf)
    {
        if (m.Minors.Count == 0 || chunks.Count == 0)
            return new Dictionary<string, (int, int, List<NamedMinor>)>(StringComparer.Ordinal);
        var feat = m.Features;
        // Assign each minor to the chunk holding its nearest promoted feature, then reuse the shared
        // named/prop split (ADR-0020) keyed by chunk name.
        var chunkNameOf = new Dictionary<MinorFeature, string>();
        foreach (var minor in m.Minors)
        {
            var mp = new LonLat(minor.Lon, minor.Lat);
            string? bestTok = null; double bestD = double.MaxValue;
            foreach (var f in feat)
            {
                double d = Geo.HaversineMeters(mp, new LonLat(f.Lon, f.Lat));
                if (d < bestD) { bestD = d; bestTok = f.Token; }
            }
            if (bestTok is not null && chunkOf.TryGetValue(bestTok, out var c))
                chunkNameOf[minor] = c.Name;
        }
        return MapGenerator.ClusterSplit(chunkNameOf.Keys, mf => chunkNameOf[mf]);
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
        Dictionary<string, List<Exit>> exits,
        Dictionary<string, (int Total, int Props, List<NamedMinor> Named)> clustered, HashSet<string> standsApart)
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
        sb.Append("  hop (N = up; 8-wind); `(far)` = ≥500 m, not a local hop. `[crosses <road>]` road/rail\n");
        sb.Append("  between; `[separated by water]` water between. **Not to scale.** **Ways out** are the only routes out.\n\n");

        AppendBounds(sb, c.Bounds);
        AppendLocalTerrain(sb, m.Terrain, c.Bounds, opts.MinTerrainAreaM2);

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
                    if (MapGenerator.IsFarHop(e.Meters, opts.Buckets)) sb.Append(" (far)");
                    if (e.SeparatedByWater) sb.Append(" [separated by water]");
                    if (e.Crosses is not null) sb.Append(" [crosses ").Append(e.Crosses).Append(']');
                    sb.Append('\n');
                }
            if (i < c.Tokens.Count - 1) sb.Append('\n');
        }
        sb.Append("```\n");
        if (clustered.TryGetValue(c.Name, out var cs))
            MapGenerator.AppendClusteredLines(sb, opts.MinorFeatures, cs.Total, cs.Named, cs.Props, "\n");

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

    private static void AppendLocalTerrain(StringBuilder sb, List<TerrainEntry> terrain, double[] bounds, double minAreaM2)
    {
        if (terrain.Count == 0 || bounds is not { Length: 4 }) return;
        // Local to the chunk AND orienting-scale (ADR-0017): pocket parks are dropped from the text.
        var local = terrain.Where(t => TerrainTouches(t, bounds)
                                    && MapGenerator.TerrainShownInMarkdown(t, minAreaM2)).ToList();
        if (local.Count == 0) return;
        sb.Append("## Terrain & barriers\n\n");
        foreach (var e in local) sb.Append("- ").Append(MapGenerator.TerrainLine(e)).Append('\n');
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
