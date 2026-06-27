using System.Linq;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

public class ProximityGraphTests
{
    // Four points on a line: A(0) B(1) C(2) D(3), spaced ~1 unit apart.
    private static readonly LonLat[] Line =
    {
        new(0.000, 0), new(0.001, 0), new(0.002, 0), new(0.003, 0),
    };

    [Fact]
    public void Adjacency_is_symmetric()
    {
        var adj = ProximityGraph.Build(Line, k: 1);
        for (int i = 0; i < Line.Length; i++)
            foreach (var j in adj[i])
                Assert.Contains(i, adj[j]); // if i links j, j links i
    }

    [Fact]
    public void K1_links_each_to_its_nearest_neighbour()
    {
        var adj = ProximityGraph.Build(Line, k: 1);
        // A's nearest is B; B's nearest is A or C; symmetrized union gives chain A-B-C-D.
        Assert.Contains(1, adj[0]);          // A-B
        Assert.Contains(2, adj[1]);          // B-C (C's nearest is B)
        Assert.Contains(3, adj[2]);          // C-D
        Assert.DoesNotContain(3, adj[0]);    // A not linked to far D
    }

    [Fact]
    public void Degenerate_inputs_dont_throw()
    {
        Assert.Empty(ProximityGraph.Build(System.Array.Empty<LonLat>(), 3));
        Assert.Single(ProximityGraph.Build(new[] { new LonLat(0, 0) }, 3));
        Assert.All(ProximityGraph.Build(new[] { new LonLat(0, 0) }, 3), s => Assert.Empty(s));
    }

    [Fact]
    public void No_self_edges()
    {
        var adj = ProximityGraph.Build(Line, k: 3);
        for (int i = 0; i < Line.Length; i++)
            Assert.DoesNotContain(i, adj[i]);
    }
}
