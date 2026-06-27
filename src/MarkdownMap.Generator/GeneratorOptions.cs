namespace MarkdownMap.Generator;

/// <summary>Distance bucket cutoffs in metres (schema §5 / settings `distance.buckets`).</summary>
public readonly struct BucketCutoffs
{
    public readonly double Adjacent;   // < this = "adjacent"
    public readonly double Near;       // < this = "near"
    public readonly double ShortWalk;  // < this = "short walk"; else "far"
    public BucketCutoffs(double adjacent, double near, double shortWalk)
    { Adjacent = adjacent; Near = near; ShortWalk = shortWalk; }

    public static BucketCutoffs Default => new BucketCutoffs(25, 150, 500);
}

/// <summary>
/// Generator v0 knobs. Values are the documented defaults from `docs/settings.md`; the
/// tunable settings surface is deferred (settings land after the prototype), so this just
/// carries defaults for now.
/// </summary>
public sealed class GeneratorOptions
{
    /// <summary>Nearest-neighbours per feature before symmetrization (`connections.neighborsPerFeature`).</summary>
    public int NeighborsPerFeature { get; set; } = 3;

    /// <summary>Print each edge under both endpoints (`connections.bidirectional`).</summary>
    public bool Bidirectional { get; set; } = true;

    /// <summary>Print the neighbour's name on each connection line (`connections.inlineNeighborName`).</summary>
    public bool InlineNeighborName { get; set; } = true;

    /// <summary>Emit the LLM framing block (`render.directivePreamble`).</summary>
    public bool DirectivePreamble { get; set; } = true;

    public BucketCutoffs Buckets { get; set; } = BucketCutoffs.Default;

    public static GeneratorOptions Default => new GeneratorOptions();
}
