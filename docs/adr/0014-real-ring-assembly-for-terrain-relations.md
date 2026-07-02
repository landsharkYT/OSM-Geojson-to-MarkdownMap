# 14. Real ring assembly for terrain relations (supersedes ADR-0008)

Date: 2026-06-30
Status: Accepted

Supersedes ADR-0008 (extent-only convex hulls).

## Context

ADR-0008 emitted water/park multipolygon relations as their convex hull, because the
Generator used terrain only for a coarse position and never rendered its shape. The
Explorer now renders terrain shape, the exact trigger 0008 named for revisiting ("if a
later feature renders terrain shape ... revisit with real ring assembly then"). On a real
extract the hull is actively wrong: it fills concavities, so ~27% of POIs fall
geometrically inside a large lake's hull ("underwater"), and long thin ravine parks balloon
over the map.

Crucially, this is an Explorer/human problem, not an LLM one: terrain reaches the
MarkdownMap only as a coarse position line (name · octant · note), never as shape. So the
fix is scoped to Explorer accuracy, and the markdown is essentially unaffected.

## Decision

The Normalizer assembles real outer rings for terrain relations instead of a convex hull:

1. Keep member roles; assemble outer (and untagged) member ways, and ignore `inner`, so
   holes are dropped (markdown-irrelevant, and visually rare here).
2. Stitch outer ways into rings with NTS `LineMerger` (order-independent); Douglas–Peucker
   simplify (~5 m, as for way-polygons). Each closed ring with area becomes one `Polygon`
   feature, sharing the relation's `name`/`kind` (unique `osmId` suffix).
3. Per-relation convex-hull fallback: if assembly yields no usable closed ring
   (bbox-clipped/broken data), emit the convex hull, i.e. ADR-0008's behavior, retained as a
   safety net. Never regress on bad data.

Contract unchanged (still `Polygon`; a multi-outer relation is several same-named features).
The Generator already groups terrain by `(kind, name)` and flattens parts for position, so
this needs no Stage-2 change.

Markdown guardrail: the terrain block stays coarse (name, octant, note) forever. Real
geometry flows into the contract and SVG, never into the markdown text, since
vertices/shorelines in the text would bloat tokens and degrade LLM reasoning. This keeps
the markdown provably safe as geometry accuracy grows.

## Consequences

- Explorer terrain is accurate; features stop appearing underwater.
- Markdown is essentially unchanged: bbox (→ "edge") is identical to the hull's, only the
  centroid octant may shift, toward truer. Crossings (barrier LineStrings) and bounds are
  independent.
- Robustness preserved via the hull fallback; `RelationGeometry` (hull) is kept for it.
- Golden output unchanged, since the Stage-2 fixture uses ways, not relations.
- Holes deferred; revisit only if an island-in-water case actually matters.
