using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MarkdownMap.Contract;
using MarkdownMap.Generator;
using Xunit;

namespace MarkdownMap.Generator.Tests;

/// <summary>Render-only settings (ADR-0011): change markdown only, never the model.</summary>
public class SettingsTests
{
    private static FeatureCollection Fc() =>
        GeoJsonReader.Read(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "rivertown.geojson")));

    private static int ArrowLines(string md) =>
        Regex.Matches(md, @"^\s+→ ", RegexOptions.Multiline).Count;

    private static GeneratorOptions With(Action<GeneratorOptions> set)
    {
        var o = GeneratorOptions.Default;
        set(o);
        return o;
    }

    [Fact]
    public void Bidirectional_off_prints_each_pair_once_under_the_lower_token()
    {
        var fc = Fc();
        var on = new MapGenerator(With(o => o.Bidirectional = true)).Generate(fc);
        var off = new MapGenerator(With(o => o.Bidirectional = false)).Generate(fc);

        // Each undirected pair appears under both tokens with "on", once with "off".
        Assert.Equal(ArrowLines(on), ArrowLines(off) * 2);

        // Off keeps only edges whose source token is the lower (more-important) one.
        foreach (Match m in Regex.Matches(off, @"(\[\d+\]) .+?\n((?:\s+→ \[\d+\].*\n?)*)"))
        {
            var from = m.Groups[1].Value;
            foreach (Match arrow in Regex.Matches(m.Groups[2].Value, @"→ (\[\d+\])"))
                Assert.True(string.CompareOrdinal(from, arrow.Groups[1].Value) < 0,
                    $"{from} should only list higher tokens, saw {arrow.Groups[1].Value}");
        }
    }

    [Fact]
    public void InlineNeighborName_off_drops_names_from_connection_lines()
    {
        var fc = Fc();
        var on = new MapGenerator(With(o => o.InlineNeighborName = true)).Generate(fc);
        var off = new MapGenerator(With(o => o.InlineNeighborName = false)).Generate(fc);

        Assert.Matches(@"→ \[\d+\] [A-Za-z]", on);          // "→ [02] Anchor Tavern …"
        Assert.DoesNotMatch(@"→ \[\d+\] [A-Za-z]", off);    // "→ [02] — ~15m N …"
    }

    [Fact]
    public void DirectivePreamble_toggles_the_framing_block()
    {
        var fc = Fc();
        Assert.Contains("DIRECTIVE PREAMBLE", new MapGenerator(With(o => o.DirectivePreamble = true)).Generate(fc));
        Assert.DoesNotContain("DIRECTIVE PREAMBLE", new MapGenerator(With(o => o.DirectivePreamble = false)).Generate(fc));
    }

    [Fact]
    public void RenderModel_reproduces_the_embedded_markdown_for_the_same_options()
    {
        // The invariant the Explorer relies on: re-rendering the cached model with the same
        // options is byte-identical to the markdown the build embedded (ADR-0011).
        var fc = Fc();
        foreach (var opts in new[]
        {
            With(o => { }),
            With(o => o.Bidirectional = false),
            With(o => o.InlineNeighborName = false),
            With(o => o.DirectivePreamble = false),
        })
        {
            var gen = new MapGenerator(opts);
            var model = gen.BuildModel(fc);
            Assert.Equal(model.Markdown, gen.RenderModel(model));
        }
    }

    [Fact]
    public void Render_only_settings_do_not_change_the_model()
    {
        // bidirectional / inlineNeighborName / directivePreamble are render-only: the model
        // (edges incl. both directions, features, districts) is identical regardless.
        var fc = Fc();
        var a = new MapGenerator(With(o => { o.Bidirectional = true; o.DirectivePreamble = true; })).BuildModel(fc);
        var b = new MapGenerator(With(o => { o.Bidirectional = false; o.DirectivePreamble = false; })).BuildModel(fc);

        Assert.Equal(a.Edges.Count, b.Edges.Count);
        Assert.Equal(a.Features.Count, b.Features.Count);
        Assert.Equal(
            a.Edges.Select(e => e.FromToken + e.ToToken),
            b.Edges.Select(e => e.FromToken + e.ToToken));
    }
}
