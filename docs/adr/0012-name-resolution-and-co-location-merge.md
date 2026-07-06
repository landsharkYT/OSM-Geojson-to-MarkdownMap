# 12. Name resolution and co-location merge live in the Normalizer

Date: 2026-06-30
Status: Accepted

Refines the feature schema (`docs/feature-schema.md` §3 inclusion gate, §5 importance).

## Context

Real OSM extracts produce three kinds of map clutter, visible on a real import:

1. Nameless POIs rendered as `[142] unnamed`, promoted as their own tokens.
2. Verbose names, e.g. a long institutional name well over the token budget.
3. Co-located duplicates: one place mapped as both an `amenity` node and a `building` way
   yields two POIs (`[013]` and `[015]`, the same church mapped twice), often with stray
   unnamed fragments (a hall, a chapel outline) nearby.

The contract carries only the resolved `name` (plus `category`/`tier`), not the raw
`operator`/`brand`/other tags. So anything that needs tags can only happen in the Normalizer
(Stage 1); the Explorer/Generator physically can't parse names.

## Decision

In the Normalizer, name resolution resolves a Feature's name as `name → brand → operator`,
preferring `short_name` when a `name` is long. A name derived from `brand`/`operator` counts
as named for promotion and gets the importance `+10`. `ref`/`addr:housename` are excluded,
since they're codes, not friendly names. There's no length-based truncation: chopping proper
names trades real ambiguity for trivial token savings.

Also in the Normalizer, a co-location merge pass collapses duplicates within a conservative
radius (~30–50 m) under a same-category-class guard. An unnamed Feature near a named one of
compatible category folds into it; two Features with the same name collapse to one
(preferring the point, or the higher-importance one).

Unnamed features get tiered promotion: a Feature still unnamed after resolution promotes to
its own token only at landmark tier, while unnamed destination/lower Features are demoted to
a District's clustered count. This is a fixed default, not a toggle, since it's
model-affecting, unlike the render-only settings (ADR-0011).

> Refined 2026-07-01, tightened to its intent: an unnamed Feature promotes only if it's
> worship (the "the church" case above); every other unnamed Feature clusters, since real
> extracts promoted piles of nameless `swimming pool` / `maritime` category-label tokens.
> See CONTEXT under *Unnamed promotion (worship-only)*.

Finally, the Generator handles the unnamed fallback label, render-only: whatever remains
unnamed renders as its humanized category subclass, lowercase (`place of worship`), with the
redundant `(category)` suppressed on those lines. It needs only `category`, so it re-renders
live and keeps the SVG token labels and the markdown consistent.

## Consequences

- Far fewer, more meaningful tokens. The notable unnamed landmark survives (as `church`),
  and the noise folds into "+N minor".
- Trade-off: the merge can over-collapse genuinely distinct neighbours. The small radius and
  same-category guard bound the risk, accepting rare false merges to kill common duplication.
- Golden output and feature counts change; golden fixtures are regenerated, and tests assert
  the new behaviour (dedup, fold, tiered promotion, fallback label).
- Naming and merging stay OSM-aware (Normalizer), while the render-only fallback stays in
  the Generator, so the contract boundary (ADR-0001) is preserved.
