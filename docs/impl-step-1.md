# Implementation step 1 — Normalizer v0 (POIs → conformant GeoJSON)

Agreed scope from the grill session. This is the first code. It deliberately attacks the
sourcing/parsing risk first and produces inspectable GeoJSON, without boiling the ocean.

## Goal
Stream a static OSM export and emit a contract-conformant GeoJSON `FeatureCollection` of
**`poi`** Features only. Visible, validatable output on real data.

## Sourcing
Static `.osm` export, supplied by the user, processed offline (see CONTEXT "Source").
Input for step 1 = any locally-supplied `.osm` export (kept out of the repo). No API,
no network.

## In scope
- Stream OSM nodes + ways with **OsmSharp**.
- **Inclusion gate** (schema §3): drop RP-noise (parking, bench, bicycle_parking,
  waste_basket, tree, …) entirely.
- **Taxonomy** (schema §4): map OSM tags → normalized `category` (class.subclass).
- **Importance** (schema §5): compute `importance` + derived `tier`.
- **Way → representative point** (schema §6): polygon centroid (NetTopologySuite).
- Emit `poi` Features with `kind,name,osmId,category,importance,tier` + `Point` geometry,
  inside the schema §1 envelope (`schemaVersion`, `title`, `bounds`).
- **Deterministic** feature ordering (e.g. by `osmId`) for stable diffs.

## Out of scope (later steps)
De-duplication, street-snap, `road`/`barrier`/`water`/`park`/`place` Features, terrain,
districts. And all of Stage 2 (the Generator).

## Projects (created this step)
- `MarkdownMap.Contract` (netstandard2.0) — GeoJSON/schema POCOs + enums.
- `MarkdownMap.Normalizer` (net10) — OsmSharp + NetTopologySuite.
- `MarkdownMap.Normalizer.Tests` (net10).
- `MarkdownMap.sln`. (Generator projects deferred to its step.)

## Acceptance / tests
Tests are **location-agnostic** — no hardcoded place names, coordinates, or extract-
specific counts (a standing rule):
- **Unit:** tag→category map; importance formula; way→centroid (point in/near polygon) —
  driven by synthetic tag dicts and synthetic geometry, no real data.
- **Integration over any local `.osm`:** every feature is `poi` + schema-conformant; noise
  categories absent; way-derived features present; output is valid GeoJSON; ordering
  deterministic. **Skips** when no `.osm` is present (so a fresh clone stays green).
- Generator-canonical rule (from the grill) applies later when Stage 2 exists.

## Done when
`dotnet test` green, and running the Normalizer on a local `.osm` yields GeoJSON whose
`poi` set is hand-reviewed as sensible (noise gone, categories right, points plausible).
Sample data and its output stay local (git-ignored), never committed.
