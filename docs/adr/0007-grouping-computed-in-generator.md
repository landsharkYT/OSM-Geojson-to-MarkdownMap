# 7. District grouping is computed in the Generator, not stamped in the contract

Date: 2026-06-27
Status: Accepted

## Context

Districts group Features by their nearest `place` anchor (Voronoi); within a district,
features are promoted (own token) or clustered (a "+N minor" count), and ordered along a
spine. This grouping has to be computed somewhere. The obvious alternative to computing it
in the Generator is to stamp a `district` field onto each POI in Stage 1 (the Normalizer)
and have the Generator merely group by that field.

## Decision

Stage 1 emits only `place` anchor features (and `street` on POIs). All grouping, meaning
Voronoi district assignment, tier-based promotion/clustering, and spine ordering, happens in
the Generator (Stage 2) at render time, reasoning purely over the `place` anchors and POI
points already in the contract.

## Consequences

- The contract stays lean: no `district` field on POIs (ADR-0001/0006 schema unchanged).
  The contract carries facts (anchors, coordinates), not layout choices.
- Promotion/clustering is tunable in one place. It was always meant to be a user setting;
  keeping it in the Generator means the knob lives where the other render knobs live, not
  split across stages.
- The Generator does more geometry (Voronoi, PCA spine), but it already owns geometry
  (haversine, proximity graph), so this is cohesive.
- Cost: assignment is recomputed every render. Cheap at neighborhood scale, so acceptable.
- Reversible only with effort (adding a `district` field touches the contract and both
  stages), which is why it's recorded here.
