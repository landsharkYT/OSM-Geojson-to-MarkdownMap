# Minor vs prop features: the named-clustered tier and map/markdown honesty

Status: Accepted
Date: 2026-07-01

Everything that isn't a promoted [[token]] currently folds into one undifferentiated
`clustered` tail — shown in the markdown as a bare count (`clustered: ~817 minor`) and, in the
Explorer, as anonymous dots. But that tail is two very different things: dots with a **real name**
(`Serafina`, `Maple Apartments`, a named grave) that a DM could reference, and dots that are **just
their category** (`pier · landmark.pier`, `house` — an unnamed pier that clustered because only
unnamed *worship* promotes). The named ones are mildly useful set-dressing, especially on a sparse
extract; the nameless ones are noise. This splits the tail and gives the named half an opt-in home.

We decided:

- **Two tiers, split by one test — does the feature carry a real resolved name** (`name → brand →
  operator`, not the humanized-category fallback, not a bare label):
  - **Minor feature** = clustered **+ real name**. Opt-in: a render-only MarkdownMap setting
    ("Minor features", default **off**) lists them as a compact **per-District names list**, deduped
    by name within the District with an `×N` count, `·`-separated (a middle dot, not a comma — a place
    name may itself contain a comma) (`minor: Serafina · Corner Coffee ×3 · …`). When shown,
    they **leave the clustered count**, which relabels to what it now counts (`props: ~N`).
  - **Prop feature** = clustered **+ name is just its category**. Anonymous background scenery. **Never**
    listed in the markdown; stays in the count.
- **Decoupled from the engine.** The promotion/[[promotion-budget|budget]]/[[narrative-salience|salience]]
  machinery is untouched — this is purely a render-time partition of the existing clustered set plus an
  opt-in projection. The toggle is **render-only** (ADR-0011): flip it and the cached model re-renders,
  no re-parse. With the toggle **off the markdown is byte-identical to today** (the golden fixture holds).
  We deliberately do **not** re-promote budgeted-losers (cafés/shops the budget set aside) — that would
  re-create the exact flood the budget exists to prevent; the ones worth showing already won tokens.
- **Explorer honesty (bidirectional).** The single "Minor features" map layer splits into two — *Minor
  features* (named) and *Prop features* (nameless) — both default **off**, so the fresh map still shows
  only what the AI sees. Because named minor features can now be *in the markdown* while independently
  *drawn or not on the map*, the honest-map warning (ADR-0013 lineage) becomes an **adaptive banner**:
  - **Surplus** (`map.minors` or `map.props` while props aren't in the markdown, or minors drawn but the
    markdown toggle off) — the map draws things the AI can't see.
  - **Deficit** (named minor features are listed in the markdown but their map layer is off) — the map
    hides features the AI sees.
  - Both, or neither (silent).

## Consequences

- A third tier enters the mental model: **token → minor feature → prop feature**. `MinorFeature` in the
  model must distinguish a real name from the humanized fallback (today `NameOf` collapses them), so the
  contract/model gains the named/nameless split (and the deduped per-District projection).
- It is *inoptimal by design* — great on a sparse extract (a waterfront neighbourhood ≈ 120 named),
  noisy on a dense one (a university campus ≈ 630 named, mostly commodity food/shops). That is precisely
  why it is a **toggle**, not a default.
- **Rejected:** tokenizing props (adds hundreds of legend lines, muddies the token space we're separating
  out); re-promoting budgeted-losers (re-floods); a purely-additive count (double-counts a feature as both
  listed and counted). No hardcoded names — the split is driven only by "has a real name".
