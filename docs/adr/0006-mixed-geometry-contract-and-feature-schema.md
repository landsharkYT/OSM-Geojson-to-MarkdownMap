# 6. Mixed-geometry GeoJSON contract + typed normalized Feature schema

Date: 2026-06-26
Status: Accepted

## Context

ADR-0001 made GeoJSON the hard contract between the Normalizer (Stage 1) and the
Generator (Stage 2), with a "normalized property schema" left abstract. Two facts force
us to make it concrete before any code:

1. **The contract is not point-only.** Three v1 features need real geometry, not just POI
   points: street-snapping needs **road LineStrings**, barrier-crossing flags need
   **barrier LineStrings** (e.g. a freeway), and terrain positioning needs **water/park polygons**.
2. **Most features are polygons.** Named ways typically outnumber named nodes — Stage 1 must reduce
   ways to representative points, de-duplicate POI-node-inside-building, and *drop* the
   dominant micro-furniture (parking spaces, benches, trees) entirely, or the map is
   noise.

If two pieces of code are written against a vague schema, they will diverge.

## Decision

The contract is a **typed, mixed-geometry GeoJSON `FeatureCollection`**. Every Feature
carries `properties.kind` declaring how Stage 2 must treat it:

- `poi` — a place; geometry is a **representative Point**.
- `road` — geometry is a **LineString**; used for street-snapping (not yet routing).
- `barrier` — geometry is a **LineString/MultiLineString**; used for crossing flags.
- `water` / `park` — geometry is a (simplified) **Polygon**; used for Terrain.
- `place` — district anchor; geometry is a **Point**.

The full field list, the OSM→category **taxonomy**, the **importance formula**, the
**inclusion gate** (what is dropped entirely), and the **polygon→point / de-dup / street-
snap / barrier / terrain-position** rules are specified in
[`docs/feature-schema.md`](../feature-schema.md) — the normative companion to this ADR.

## Consequences

- Stage 1 has an unambiguous output target; Stage 2 ranks/draws purely from `kind` +
  normalized properties, never raw OSM (ADR-0001 preserved).
- The contract is versioned (`schemaVersion`), since both stages depend on it.
- Carrying line/polygon geometry costs size, but it is required for v1 streets/barriers/
  terrain and is the same geometry the deferred roads/transit features will reuse.
- Importance scoring + the drop-list are **tunable defaults** (see settings registry),
  not hardcoded.
