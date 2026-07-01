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

    /// <summary>Print each edge under both endpoints (`connections.bidirectional`). Default off:
    /// each link once, since a capable LLM infers the reverse — the terser default (grill 2026-06-30).</summary>
    public bool Bidirectional { get; set; } = false;

    /// <summary>Max key features listed on a district spine (skeleton, not full membership).</summary>
    public int SpineKeyCap { get; set; } = 8;

    /// <summary>
    /// Per-District promotion budget (ADR-0018): how many **budgeted** features earn their own token
    /// beyond the always-promoted salience core; the rest fold into the clustered count. Model-affecting.
    /// </summary>
    public int PromotionBudget { get; set; } = 20;

    /// <summary>Print the neighbour's name on each connection line (`connections.inlineNeighborName`).</summary>
    public bool InlineNeighborName { get; set; } = true;

    /// <summary>Emit the LLM framing block (`render.directivePreamble`).</summary>
    public bool DirectivePreamble { get; set; } = true;

    /// <summary>
    /// Render the map as a set of self-contained scene-chunks instead of one document (ADR-0016).
    /// Render-only over the MapModel; the whole-area markdown is still produced alongside.
    /// </summary>
    public bool Chunking { get; set; } = false;

    /// <summary>
    /// Target promoted-feature count per scene-chunk (ADR-0016). A District over this size is
    /// subdivided by density-gap bisection (ADR-0017). Tight ≈ 8 · Standard ≈ 14 · Wide ≈ 22.
    /// </summary>
    public int SceneSize { get; set; } = 14;

    /// <summary>
    /// Minimum area (m²) for a park/water polygon to be listed in the **markdown** terrain block
    /// (ADR-0017). Smaller ones (pocket parks, street-ends) are dropped from the text only — their
    /// geometry still reaches the contract + Explorer. Barriers and linear shorelines are unaffected.
    /// </summary>
    public double MinTerrainAreaM2 { get; set; } = 5000;

    public BucketCutoffs Buckets { get; set; } = BucketCutoffs.Default;

    public static GeneratorOptions Default => new GeneratorOptions();

    /// <summary>Map the Tight/Standard/Wide label (ADR-0016) to its <see cref="SceneSize"/> target.</summary>
    public static int SceneSizeFor(string? label) => label switch
    {
        "tight" => 8,
        "wide" => 22,
        _ => 14, // standard / unknown
    };
}
