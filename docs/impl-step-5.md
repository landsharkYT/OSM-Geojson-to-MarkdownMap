# Implementation step 5 — Water/park multipolygon relations

Closes the step-4 gap: terrain areas that live in multipolygon **relations** (lakes, bays,
big parks) were missed by the ways-only Normalizer. Stage 1 only — the Generator already
renders `water`/`park` Polygons (step 4 proved it).

## Scope
Named multipolygon relations tagged `natural=water` | `leisure=park` |
`landuse=recreation_ground`. Barriers stay ways-only. Linear waterway relations out of scope.

## Approach (ADR-0008: extent-only)
- During the stream, **buffer every way's node-id list** (`id → long[]`), in addition to
  the existing node-coord buffer, so relation members resolve afterwards.
- For each in-scope relation: gather coordinates from **all member ways** (role-agnostic;
  inner holes sit inside the extent), via the buffered way → node-coord lookups.
- Emit one `water`/`park` Feature whose geometry is the **convex hull** of those coords
  (NetTopologySuite `ConvexHull`), as a `Polygon`. Skip if fewer than 3 distinct points.
- Reuse the existing `MakeArea` shape; `osmId` = `r<id>`.

## No new dedup
The Generator already merges terrain by `(kind, name)`, so a body appearing as both a way
(step 4) and a relation collapses to one Terrain entry at render time.

## Tests (location-agnostic, committable)
- **Unit:** a pure relation-hull helper — given member coordinate lists, produce the convex
  hull ring; synthetic input (e.g. an L-shaped set → its hull); skip-when-degenerate.
- **Integration over local `.osm`:** strengthen the terrain assertion — at least one
  `water` Feature is now emitted (was zero), with `Polygon` geometry. Still no place names
  asserted.

## Done when
`dotnet test` green; running the CLI on a local `.osm` now shows the defining water
body/bodies in the Terrain block with a sensible position (e.g. a lake on the W/SW edge),
hand-reviewed. No real data committed.

## Outcome
Built and green (81 tests). The sample extract's Terrain block now shows its water:
`a lake (water) · NW edge · open water` and `a bay (water) · NE · open water`,
both from multipolygon relations the previous ways-only pass missed. The step-4 water gap
is closed.
