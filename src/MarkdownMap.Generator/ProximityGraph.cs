using System.Collections.Generic;
using System.Linq;

namespace MarkdownMap.Generator;

/// <summary>
/// Geometric proximity graph (ADR-0003, amended): each feature links to its k nearest
/// neighbours, symmetrized (an edge exists if either endpoint chose the other), giving a
/// sparse undirected adjacency. No planar pruning — structured text needs no planarity.
/// </summary>
public static class ProximityGraph
{
    /// <summary>
    /// Returns index-aligned adjacency: <c>result[i]</c> is the set of feature indices
    /// connected to feature <c>i</c>. Undirected (symmetric).
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> Build(IReadOnlyList<LonLat> points, int k)
    {
        int n = points.Count;
        var adj = new HashSet<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new HashSet<int>();
        if (n < 2 || k < 1) return adj.Select(s => (IReadOnlyList<int>)s.ToList()).ToList();

        for (int i = 0; i < n; i++)
        {
            var nearest = Enumerable.Range(0, n)
                .Where(j => j != i)
                .OrderBy(j => Geo.HaversineMeters(points[i], points[j]))
                .ThenBy(j => j) // deterministic tie-break
                .Take(k);
            foreach (int j in nearest)
            {
                adj[i].Add(j); // symmetrize: union of both endpoints' choices
                adj[j].Add(i);
            }
        }

        return adj.Select(s => (IReadOnlyList<int>)s.OrderBy(x => x).ToList()).ToList();
    }
}
