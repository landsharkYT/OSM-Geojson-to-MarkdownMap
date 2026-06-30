using System.Collections.Generic;

namespace MarkdownMap.Normalizer;

/// <summary>
/// Resolves a Feature's display name from raw OSM tags (ADR-0012): <c>name → brand → operator</c>,
/// preferring <c>short_name</c> when the primary <c>name</c> is long. Returns null when nothing
/// usable exists — the Feature is then <em>unnamed</em> and gets a category fallback label in the
/// Generator. This is the only place name parsing can live: the contract carries the resolved
/// name, not the raw tags, so the Explorer/Generator never see them.
/// </summary>
public static class NameResolver
{
    /// <summary>A <c>name</c> longer than this prefers <c>short_name</c> when one exists.</summary>
    private const int LongNameChars = 30;

    public static string? Resolve(IReadOnlyDictionary<string, string> tags)
    {
        string? Get(string k) => tags.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

        var name = Get("name");
        if (name is not null)
        {
            var shortName = Get("short_name");
            return shortName is not null && name.Length > LongNameChars ? shortName : name;
        }
        // No name: a brand/operator is a real name (counts as named for promotion/importance).
        return Get("brand") ?? Get("operator");
    }
}
