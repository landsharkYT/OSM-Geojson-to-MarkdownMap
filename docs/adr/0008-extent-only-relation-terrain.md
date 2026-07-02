# 8. Multipolygon terrain emitted as extent-only convex hulls

Date: 2026-06-27
Status: Superseded by ADR-0014 (real ring assembly). The convex hull is retained only as the
per-relation fallback when ring assembly fails.

## Context

Water bodies and large parks live in OSM multipolygon relations (e.g. a lake is `relation
natural=water type=multipolygon`), not ways, which is why the ways-only Stage 1 (step 4)
emitted no water. A relation's boundary is split across member ways that must be stitched
into rings, and large bodies are clipped at the export bbox, so a true ring may not even
close. Faithful ring assembly is fragile exactly where it matters.

Crucially, the Generator uses a terrain polygon only for its position (centroid / bbox →
octant / "edge" / "runs"). It never renders the polygon's shape.

## Decision

A named multipolygon `water`/`park` relation is emitted as the convex hull of all its
member-way coordinates: a single valid representative `Polygon`. No ring assembly, no
inner holes, no role logic (inner members sit inside the hull anyway).

## Consequences

- Robust to bbox-clipped/open relations: a coordinate cloud always has a hull.
- Simple: no multipolygon stitching, just resolve member ways to points to hull.
- Position-accurate: centroid/bbox of the hull give the right octant/edge.
- Loses true shape: concave bodies are over-filled (a long lake's bend disappears) and area
  is overstated. That's acceptable since shape is unused. If a later feature renders
  terrain shape (ASCII map, React), revisit with real ring assembly then.
- Barriers remain ways-only (linear); only area terrain uses relations.
