# 12. Name resolution and co-location merge live in the Normalizer

Date: 2026-06-30
Status: Accepted

Refines the feature schema (`docs/feature-schema.md` §3 inclusion gate, §5 importance).

## Context

Real OSM extracts produce three kinds of map clutter, visible on a real import:

1. **Nameless POIs** rendered as `[142] unnamed`, promoted as their own tokens.
2. **Verbose names** (a long institutional name well over the token budget).
3. **Co-located duplicates** — one place mapped as both an `amenity` **node** and a
   `building` **way** yields two POIs (`[013]` and `[015]`, the same church mapped twice),
   often with stray unnamed fragments (a hall, a chapel outline) nearby.

The contract carries only the resolved `name` (plus `category`/`tier`) — **not** the raw
`operator`/`brand`/other tags. So anything that needs tags can only happen in the
**Normalizer (Stage 1)**; the Explorer/Generator physically cannot parse names.

## Decision

**Name resolution (Normalizer).** Resolve a Feature's name as `name → brand → operator`,
preferring `short_name` when a `name` is long. A name derived from `brand`/`operator` counts
as **named** for promotion and the importance `+10`. `ref`/`addr:housename` are excluded
(codes, not friendly names). No length-based truncation — chopping proper names trades real
ambiguity for trivial token savings.

**Co-location merge (Normalizer).** One pass collapses duplicates within a conservative
radius (~30–50 m) under a same-category-class guard:
- an **unnamed** Feature near a **named** one of compatible category folds into it;
- two Features with the **same name** collapse to one (prefer the point / higher importance).

**Tiered unnamed promotion.** A Feature still unnamed after resolution promotes to its own
token only at **landmark tier**; unnamed destination/lower Features are demoted to a
District's clustered count. Fixed default, not a toggle — it is model-affecting, unlike the
render-only settings (ADR-0011).

> **Refined (2026-07-01):** tightened to its intent — an unnamed Feature promotes **only if it is
> worship** (the "the church" case above); every other unnamed Feature clusters, since real extracts
> promoted piles of nameless `swimming pool` / `maritime` category-label tokens. See CONTEXT →
> *Unnamed promotion (worship-only)*.

**Unnamed fallback label (Generator, render-only).** Whatever remains unnamed renders as its
humanized **category** subclass, lowercase (`place of worship`), with the redundant
`(category)` suppressed on those lines. Needs only `category`, so it re-renders live and keeps
the SVG token labels and the markdown consistent.

## Consequences

- Far fewer, more meaningful tokens; the notable unnamed landmark survives (as `church`),
  the noise folds into "+N minor".
- **Trade-off:** the merge can over-collapse genuinely distinct neighbours; the small radius +
  same-category guard bound the risk, accepting rare false merges to kill common duplication.
- Golden output and feature counts change — golden fixtures are regenerated; tests assert the
  new behaviour (dedup, fold, tiered promotion, fallback label).
- Naming/merge stay OSM-aware (Normalizer); the render-only fallback stays in the Generator —
  the contract boundary (ADR-0001) is preserved.
