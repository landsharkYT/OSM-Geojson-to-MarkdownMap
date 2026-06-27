using System;
using System.IO;
using System.Linq;
using MarkdownMap.Contract;
using MarkdownMap.Normalizer;
using Xunit;

namespace MarkdownMap.Normalizer.Tests;

/// <summary>
/// Location-agnostic invariant checks over whatever local `.osm` happens to be present
/// (none are committed). No hardcoded place names, coordinates, or extract-specific
/// counts — only structural properties that must hold for ANY OSM extract.
///
/// When no `.osm` is present (e.g. a fresh clone), each test returns early and passes
/// trivially — xUnit 2.x has no runtime skip, and we keep the test project dependency-free.
/// </summary>
public class OsmNormalizerIntegrationTests
{
    private static readonly string? OsmPath = FindAnyOsm();
    private static FeatureCollection? _cached;

    private static FeatureCollection Load() =>
        _cached ??= new OsmNormalizer().NormalizeFile(OsmPath!);

    private static string? FindAnyOsm()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var hit = Directory.GetFiles(dir.FullName, "*.osm")
                .OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();
            if (hit is not null) return hit;
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public void Produces_only_pois_in_a_conformant_envelope()
    {
        if (OsmPath is null) return;
        var fc = Load();
        Assert.NotEmpty(fc.Features);
        Assert.Contains(fc.Features, f => f.Properties.Kind == "poi");
        Assert.All(fc.Features, f => Assert.Contains(f.Properties.Kind, new[] { "poi", "place" }));
        Assert.Equal(1, fc.Properties.SchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(fc.Properties.Title));
    }

    [Fact]
    public void No_rp_noise_leaks_through()
    {
        if (OsmPath is null) return;
        var pois = Load().Features.Where(f => f.Properties.Kind == "poi");
        string[] noise = { "parking", "bench", "tree", "waste", "bicycle_parking" };
        Assert.DoesNotContain(pois, f => noise.Any(n => f.Properties.Category!.Contains(n)));
    }

    [Fact]
    public void Place_anchors_and_street_attribution_are_emitted()
    {
        if (OsmPath is null) return;
        var fc = Load();
        Assert.Contains(fc.Features, f => f.Properties.Kind == "place");           // district anchors
        Assert.Contains(fc.Features, f => f.Properties.Kind == "poi" && !string.IsNullOrEmpty(f.Properties.Street));
    }

    [Fact]
    public void Every_feature_is_schema_conformant()
    {
        if (OsmPath is null) return;
        Assert.All(Load().Features, f =>
        {
            Assert.Matches(@"^[nw]\d+$", f.Properties.OsmId!);
            Assert.Equal("Point", f.Geometry.Type);
            var coords = Assert.IsType<double[]>(f.Geometry.Coordinates);
            Assert.Equal(2, coords.Length);

            if (f.Properties.Kind == "poi")
            {
                Assert.False(string.IsNullOrEmpty(f.Properties.Category));
                Assert.NotNull(f.Properties.Importance);
                Assert.InRange(f.Properties.Importance!.Value, 0, 100);
                Assert.Contains(f.Properties.Tier, new[] { "landmark", "destination", "minor", "structure" });
            }
            else // place
            {
                Assert.False(string.IsNullOrEmpty(f.Properties.Name));
                Assert.Null(f.Properties.Category);
            }
        });
    }

    [Fact]
    public void Output_is_deterministic_and_sorted()
    {
        if (OsmPath is null) return;
        var a = new OsmNormalizer().NormalizeFile(OsmPath).Features.Select(f => f.Properties.OsmId).ToList();
        var b = new OsmNormalizer().NormalizeFile(OsmPath).Features.Select(f => f.Properties.OsmId).ToList();
        Assert.Equal(a, b);
        Assert.Equal(a.OrderBy(x => x, StringComparer.Ordinal), a);
    }
}
