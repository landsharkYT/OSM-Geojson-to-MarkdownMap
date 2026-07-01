using System;
using System.IO;
using System.Linq;
using MarkdownMap.Contract;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

/// <summary>
/// Scene-chunk retrieval (ADR-0016). Chunking is render-only over the MapModel: off → identical
/// whole-area output; on → a self-contained chunk per District (spine-split when oversized) plus a
/// manifest, with concrete off-map exits and globally stable tokens.
/// </summary>
public class ChunkingTests
{
    private static FeatureCollection Fc() =>
        GeoJsonReader.Read(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "rivertown.geojson")));

    private static GeneratorOptions Chunked(int sceneSize = 14) =>
        new GeneratorOptions { Chunking = true, SceneSize = sceneSize };

    private static Feature Poi(string name, double lon, double lat, int importance, string? district = null) => new Feature
    {
        Properties = new FeatureProperties
        {
            Kind = "poi", Name = name, Category = "civic.hall",
            Importance = importance, Tier = "landmark", OsmId = "n" + Math.Abs(name.GetHashCode()),
        },
        Geometry = new Geometry { Type = "Point", Coordinates = new[] { lon, lat } },
    };

    private static Feature Place(string name, double lon, double lat) => new Feature
    {
        Properties = new FeatureProperties { Kind = "place", Name = name },
        Geometry = new Geometry { Type = "Point", Coordinates = new[] { lon, lat } },
    };

    // Two neighbourhoods either side of a gap: West (3 POIs near anchor) and East (3 POIs near anchor).
    private static FeatureCollection TwoDistricts() => new FeatureCollection
    {
        Properties = new CollectionProperties { Title = "Rivertown", Bounds = new[] { -0.01, -0.01, 0.01, 0.01 } },
        Features = new System.Collections.Generic.List<Feature>
        {
            Place("Westbank", -0.005, 0.0),
            Place("Eastbank", 0.005, 0.0),
            Poi("West Hall", -0.005, 0.0000, 90),
            Poi("West Cafe", -0.0048, 0.0006, 70),
            Poi("West Mill", -0.0052, -0.0006, 60),
            Poi("East Hall", 0.005, 0.0000, 95),
            Poi("East Cafe", 0.0048, 0.0006, 75),
            Poi("East Dock", 0.0052, -0.0006, 65),
        },
    };

    [Fact]
    public void Off_by_default_no_chunks_and_unchanged_markdown()
    {
        var fc = Fc();
        var plain = new MapGenerator().BuildModel(fc);
        Assert.Empty(plain.Chunks);
        Assert.Equal("", plain.Manifest);

        // Turning chunking on must not touch the whole-area markdown (it is produced alongside).
        var chunked = new MapGenerator(Chunked()).BuildModel(fc);
        Assert.Equal(plain.Markdown, chunked.Markdown);
    }

    [Fact]
    public void One_chunk_per_district_with_a_manifest()
    {
        var m = new MapGenerator(Chunked()).BuildModel(TwoDistricts());
        Assert.Equal(2, m.Chunks.Count);
        Assert.NotEqual("", m.Manifest);

        var names = m.Chunks.Select(c => c.Name).ToHashSet();
        Assert.Contains("Westbank", names);
        Assert.Contains("Eastbank", names);

        // Every promoted token lands in exactly one chunk.
        var tokens = m.Chunks.SelectMany(c => c.Tokens).ToList();
        Assert.Equal(m.Features.Count, tokens.Count);
        Assert.Equal(m.Features.Count, tokens.Distinct().Count());

        // The manifest lists each chunk's file and anchor.
        foreach (var c in m.Chunks)
        {
            Assert.Contains(c.Slug + ".md", m.Manifest);
            Assert.Contains(c.AnchorToken, m.Manifest);
        }
    }

    [Fact]
    public void Each_chunk_is_self_contained_and_lists_concrete_exits()
    {
        var m = new MapGenerator(Chunked()).BuildModel(TwoDistricts());
        var west = m.Chunks.Single(c => c.Name == "Westbank");

        // Self-contained: its own header, reading key, its features, and a way out.
        Assert.Contains("# SCENE-CHUNK — Westbank · Rivertown", west.Markdown);
        Assert.Contains("## How to read", west.Markdown);
        Assert.Contains("## Ways out", west.Markdown);

        // The exit points at a real, globally-tokenised feature in the *other* chunk.
        var east = m.Chunks.Single(c => c.Name == "Eastbank");
        Assert.Contains("Eastbank", west.Neighbours);
        Assert.Matches(@"→ \w+ toward Eastbank \(off-map\): via \[\d+\] [A-Za-z]", west.Markdown);

        // Anchor is the most important feature in the chunk.
        Assert.Equal("East Hall", east.AnchorName);
    }

    [Fact]
    public void Tokens_are_global_and_stable_across_chunks_and_exits()
    {
        var m = new MapGenerator(Chunked()).BuildModel(TwoDistricts());
        // A token names the same feature everywhere: its chunk's Features block and any exit to it.
        var eastHall = m.Features.Single(f => f.Name == "East Hall");
        var west = m.Chunks.Single(c => c.Name == "Westbank");
        // If West's nearest East feature is East Hall, the exit token equals the global token.
        if (west.Markdown.Contains("East Hall"))
            Assert.Contains(eastHall.Token + " East Hall", west.Markdown);
    }

    [Fact]
    public void A_large_district_splits_along_its_spine()
    {
        // One district, 6 POIs strung north–south, scene size 3 → two contiguous segments.
        var feats = new System.Collections.Generic.List<Feature> { Place("Spine City", 0, 0) };
        for (int i = 0; i < 6; i++) feats.Add(Poi("F" + i, 0.0, -0.003 + i * 0.001, 90 - i));
        var fc = new FeatureCollection
        {
            Properties = new CollectionProperties { Title = "Long Town", Bounds = new[] { -0.01, -0.01, 0.01, 0.01 } },
            Features = feats,
        };

        var m = new MapGenerator(Chunked(sceneSize: 3)).BuildModel(fc);
        Assert.Equal(2, m.Chunks.Count);
        // Segments are suffixed by compass position of the segment within the district (N / S).
        Assert.All(m.Chunks, c => Assert.StartsWith("Spine City · ", c.Name));
        // Contiguous, non-overlapping partition of all six tokens.
        var tokens = m.Chunks.SelectMany(c => c.Tokens).ToList();
        Assert.Equal(6, tokens.Distinct().Count());
        // Distinct file slugs.
        Assert.Equal(2, m.Chunks.Select(c => c.Slug).Distinct().Count());
    }

    [Fact]
    public void No_place_anchors_falls_back_to_proximity_components()
    {
        // No `place` features → fallback partition. Two far-apart clusters become two chunks.
        // Each cluster has 4 members so the k=3 proximity graph never bridges across the gap.
        var feats = new System.Collections.Generic.List<Feature>
        {
            Poi("A1", -0.02, 0.0, 90), Poi("A2", -0.0202, 0.0003, 80),
            Poi("A3", -0.0198, -0.0003, 70), Poi("A4", -0.0203, -0.0006, 60),
            Poi("B1", 0.02, 0.0, 95), Poi("B2", 0.0202, 0.0003, 85),
            Poi("B3", 0.0198, -0.0003, 75), Poi("B4", 0.0203, -0.0006, 65),
        };
        var fc = new FeatureCollection
        {
            Properties = new CollectionProperties { Title = "Anchorless", Bounds = new[] { -0.03, -0.01, 0.03, 0.01 } },
            Features = feats,
        };

        var m = new MapGenerator(Chunked()).BuildModel(fc);
        Assert.Empty(m.Districts);
        Assert.Equal(2, m.Chunks.Count);
        Assert.All(m.Chunks, c => Assert.StartsWith("Around ", c.Name));
    }

    [Fact]
    public void Compose_reproduces_chunks_on_re_render_from_the_model()
    {
        // The Explorer re-render invariant (ADR-0011/0016): composing the cached model with the
        // same options reproduces the same chunk set the build embedded.
        var gen = new MapGenerator(Chunked());
        var built = gen.BuildModel(TwoDistricts());

        // Round-trip the model and re-compose, as the WASM re-render path does.
        var again = gen.Compose(built);
        Assert.Equal(built.Chunks.Count, again.Chunks.Count);
        Assert.Equal(
            built.Chunks.Select(c => c.Name + "|" + c.Markdown),
            again.Chunks.Select(c => c.Name + "|" + c.Markdown));
    }
}
