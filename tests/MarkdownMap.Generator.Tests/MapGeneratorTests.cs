using System;
using System.IO;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

public class MapGeneratorTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "fixtures", "rivertown.geojson");
    private static string GoldenPath => Path.Combine(AppContext.BaseDirectory, "golden", "rivertown.expected.md");

    private static string Generate()
    {
        var fc = GeoJsonReader.Read(File.ReadAllText(FixturePath));
        return new MapGenerator().Generate(fc);
    }

    private static string Norm(string s) => s.Replace("\r\n", "\n").TrimEnd('\n');

    [Fact]
    public void Matches_committed_golden()
    {
        Assert.Equal(Norm(File.ReadAllText(GoldenPath)), Norm(Generate()));
    }

    [Fact]
    public void Is_deterministic()
    {
        Assert.Equal(Generate(), Generate());
    }

    [Fact]
    public void Token_one_is_the_most_important_feature()
    {
        // Founders Mural (importance 95) must be [01].
        Assert.Contains("[01] Founders Mural", Generate());
    }

    [Fact]
    public void Emits_expected_sections()
    {
        var md = Generate();
        Assert.Contains("<!-- DIRECTIVE PREAMBLE", md);
        Assert.Contains("## How to read", md);
        Assert.Contains("**Bounds:**", md);
        Assert.Contains("## Terrain & barriers", md); // fixture has water/park/barrier
        Assert.Contains("## Districts", md);          // fixture has place anchors
        Assert.Contains("## Connections", md);
    }

    [Fact]
    public void Terrain_block_lists_water_park_and_barrier_with_positions()
    {
        var md = Generate();
        Assert.Contains("- Mill Lake (water) · W · open water", md);
        Assert.Contains("- Riverside Park (park) · N · green space", md);
        Assert.Contains("- Route 9 (barrier:motorway) · runs N–S, E · impassable except at crossings", md);
    }

    [Fact]
    public void Edges_crossing_a_barrier_are_flagged()
    {
        var md = Generate();
        // Riverside School (E of Route 9) ↔ the Main Street strip (W) must cross. With bidirectional
        // off the pair prints once under the lower token; metres + bearing only (no size bucket).
        Assert.Contains("[crosses Route 9]", md);
        Assert.Matches(@"→ \[\d+\] [^\n]*— ~\d+m \w+ \[crosses Route 9\]", md);
    }

    [Fact]
    public void Connection_headers_hoist_the_dominant_street_and_keep_exceptions()
    {
        var md = Generate();
        // [01] sits on Main Street (Old Town's dominant, named at the district/spine) → street dropped.
        Assert.Contains("[01] Founders Mural (landmark.artwork) · Old Town", md);
        Assert.DoesNotContain("[01] Founders Mural (landmark.artwork) · on Main Street", md);
        // Riverside School is off the dominant street → it keeps its own (streetApprox → "near").
        Assert.Contains("· near 10th Street · Harborside", md);
    }

    [Fact]
    public void Minors_are_clustered_not_tokenized()
    {
        var md = Generate();
        Assert.Contains("clustered: ~2 minor", md);
        Assert.DoesNotContain("Maple Apartments", md);  // minor-tier name never rendered
        Assert.DoesNotContain("residential.apartments", md);
    }

    [Fact]
    public void District_block_has_spine_and_promoted_count()
    {
        var md = Generate();
        Assert.Contains("spine: N–S along Main Street: [01],[02],[03],[05],[06]", md);
        Assert.Contains("promoted: 5", md);
    }

    [Fact]
    public void Preamble_can_be_disabled()
    {
        var fc = GeoJsonReader.Read(File.ReadAllText(FixturePath));
        var md = new MapGenerator(new GeneratorOptions { DirectivePreamble = false }).Generate(fc);
        Assert.DoesNotContain("DIRECTIVE PREAMBLE", md);
        Assert.Contains("## Connections", md);
    }
}
