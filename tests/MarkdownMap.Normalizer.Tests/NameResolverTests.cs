using System.Collections.Generic;
using MarkdownMap.Normalizer;
using Xunit;

namespace MarkdownMap.Normalizer.Tests;

/// <summary>Name resolution chain (ADR-0012): name → brand → operator, short_name preference.</summary>
public class NameResolverTests
{
    private static Dictionary<string, string> Tags(params (string k, string v)[] kv)
    {
        var d = new Dictionary<string, string>();
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }

    [Fact]
    public void Prefers_name_over_brand_and_operator() =>
        Assert.Equal("Bluebird Coffee",
            NameResolver.Resolve(Tags(("name", "Bluebird Coffee"), ("brand", "Chain"), ("operator", "Op"))));

    [Fact]
    public void Falls_back_to_brand_then_operator()
    {
        Assert.Equal("Pharmacy Co", NameResolver.Resolve(Tags(("brand", "Pharmacy Co"), ("operator", "Op"))));
        Assert.Equal("County Health", NameResolver.Resolve(Tags(("operator", "County Health"))));
    }

    [Fact]
    public void Returns_null_when_nothing_usable() =>
        Assert.Null(NameResolver.Resolve(Tags(("amenity", "cafe"))));

    [Fact]
    public void Prefers_short_name_only_when_name_is_long()
    {
        var longName = "Rivertown Society of the Western Hills"; // 38 chars (synthetic)
        Assert.Equal("Rivertown Society",
            NameResolver.Resolve(Tags(("name", longName), ("short_name", "Rivertown Society"))));
        // A short primary name wins over short_name.
        Assert.Equal("Corner Cafe",
            NameResolver.Resolve(Tags(("name", "Corner Cafe"), ("short_name", "CC"))));
    }

    [Fact]
    public void Ignores_blank_tag_values() =>
        Assert.Equal("Op", NameResolver.Resolve(Tags(("name", "   "), ("brand", ""), ("operator", "Op"))));
}
