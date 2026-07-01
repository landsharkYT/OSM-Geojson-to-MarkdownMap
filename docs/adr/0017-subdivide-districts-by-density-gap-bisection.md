# 17. Subdivide oversized Districts by density-gap bisection (supersedes ADR-0016 §1)

Date: 2026-06-30
Status: Accepted

Supersedes decision **#1** of [ADR-0016](0016-scene-chunk-retrieval.md) ("partition by District,
**spine-split** to scene size"). The rest of ADR-0016 stands.

## Context

ADR-0016 chunked an oversized District by **spine-splitting** it — ordering its features along a
1-D PCA axis and cutting into contiguous segments. That assumed a District is roughly **linear**
(strung along a street). Run against a real extract it wasn't: the source gave only **two**
`place` anchors for the whole area, so Voronoi lumped ~110 features into **one** District, a 2-D
blob. Spine-splitting a blob produced junk:

- **Overlapping bounds.** Thin slices perpendicular to the spine share the other axis's whole
  range, so chunk bboxes overlap heavily — breaking the [[manifest]]'s "pick the chunk by bbox".
- **Collapsed names.** Every slice's centroid sat on the same side of the (spine-dragged) District
  centroid, so the octant-from-centroid label degenerated to `E`, `E 2`, `E 3`, `E 4`, `E 5` —
  unnarratable.
- **Arbitrary seams.** Cuts fell *through* walking-distance clusters, so "ways out" were ~20 m —
  the chunk edge ran down the middle of a block.
- **Orphans.** Slicing the proximity graph left features whose every neighbour was in another
  piece, so they floated with no connection — defeating the symmetrized-kNN "never disconnected"
  guarantee.

The District *idea* was fine (a named region is a real scene boundary); the **1-D split** was
wrong for 2-D neighbourhoods under coarse `place` coverage.

## Decision

1. **District stays the chunk unit.** A small District (≤ scene size) is one whole chunk. Only an
   **oversized** District subdivides. Good `place` coverage still yields district-shaped chunks.

2. **Subdivide by density-gap bisection.** Recursively split the District at the **widest gap**
   along its **longer cardinal axis** (lon or lat, whichever spans more metres) until each piece
   fits the scene size. Half-plane cuts are contiguous and **non-overlapping** by construction;
   choosing the widest gap puts the cut in a **sparse seam**, so exits are real transitions and
   dense blocks are rarely sliced. Deterministic, no dependencies (stays in the lean `Geo.cs`
   Generator). Cardinal (not PCA) axis keeps pieces tidy and the octant names honest.

3. **Repair orphans.** After bisection, any feature with **no in-piece proximity neighbour** is
   merged into the piece holding its nearest neighbour — guaranteeing every feature connects to
   something in its chunk, at the cost of a small scene-size overflow and bbox nudge (manifest
   bounds are advisory).

4. **Name pieces `District · <octant>`**, the octant taken from the piece centroid relative to the
   District; on collision or deep recursion, fall back to the piece's [[anchor]] key feature
   (`District · <octant> — <Anchor>`). Never bare ordinals.

5. **Terrain markdown lists only orienting-scale features.** Parks/water below an **area
   threshold** (shoelace area of the rings already held) are dropped from the *text* — geometry
   still flows to the contract + Explorer SVG — so a park-dense extract isn't drowned in pocket
   parks. The redundant per-line note (`green space` / `open water`) is dropped (the kind label
   says it); the barrier note (`impassable except at crossings`) stays.

## Consequences

- Chunks become spatially coherent, non-overlapping, meaningfully named, with real (~100 m+) exits
  and no orphans — the manifest's bbox lookup and the "one chunk = one place" story hold on real,
  coarse-coverage data.
- **Render-only** (ADR-0011): all of this reasons over the built MapModel; no re-parse, reversible
  in code. The output *shape* is unchanged from ADR-0016 (chunk set + manifest).
- The **[[spine]]** is unaffected as a concept — it still orders a District's features on the
  whole-area `spine:` line. It is simply no longer the chunk-splitting mechanism.
- The area threshold and scene size are **heuristics with defaults**, deferred as tunable knobs.
- Trade-offs accepted: bisection doesn't hard-guarantee balanced piece sizes (a lone outlier past
  a wide gap can form a small piece); orphan-repair can push a piece slightly over scene size; the
  area threshold is a single global default, not per-extract adaptive. Revisit only if a concrete
  extract shows these failing the way spine-split did.
- Not chosen: **proximity-graph partition** (strongest coherence but unbalanced sizes and hard to
  make deterministic) and **k-means / grid** (compact but seam-agnostic, still slices blocks).
