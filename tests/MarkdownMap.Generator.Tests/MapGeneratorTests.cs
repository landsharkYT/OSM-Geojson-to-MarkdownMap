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
    public void Emits_expected_sections_and_omits_unavailable_ones()
    {
        var md = Generate();
        Assert.Contains("<!-- DIRECTIVE PREAMBLE", md);
        Assert.Contains("## How to read this map", md);
        Assert.Contains("**Bounds:**", md);
        Assert.Contains("## Connections", md);
        Assert.DoesNotContain("## Districts", md);   // no place data yet
        Assert.DoesNotContain("## Terrain", md);     // no water/park data yet
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
