# Implementation step 2 — Generator v0 (GeoJSON → Connections map)

Agreed scope from the grill session. First slice of Stage 2. Builds the algorithmic core
(proximity graph + geometry) and emits a real, readable map from the POI-only GeoJSON the
Normalizer produces today. Districts/Terrain/barriers wait for Stage 1 to feed them.

## Goal
Consume a contract GeoJSON `FeatureCollection` of `poi` Features and emit a MarkdownMap of
**Directive Preamble + How-to-read + Bounds + Connections** (per `docs/output-format.md`).
OSM-agnostic — reasons only over the normalized schema.

## In scope
- Read GeoJSON via **System.Text.Json**, reusing `MarkdownMap.Contract` POCOs (plus a small
  reader for `Geometry.Coordinates`, which deserializes as `JsonElement`).
- **Proximity graph:** nearest-k (k=3 default), symmetrized for bidirectional edges. **No**
  Gabriel/RNG pruning (ADR-0003 amendment — structured text needs no planarity).
- **Geometry (own, no NTS):** haversine distance (m); initial-bearing → **8-wind**; bucket
  (`adjacent`<25 · `near`<150 · `short walk`<500 · `far`≥500).
- **Token assignment:** importance desc, then `osmId` — `[01]` = most important.
- **Render** the four sections. Feature header = `[NN] <name> (<category>)` (street/district
  segments optional, omitted in v0). Connection = `→ [NN] <name> — ~<m>m <DIR>, <size>`.
- Defaults **hardcoded as named constants** matching `docs/settings.md` (k, buckets,
  `bidirectional`=on, `inlineNeighborName`=on, `directivePreamble`=on). The settings object
  is deferred — settings land after the prototype.

## Out of scope (later)
Districts, Terrain, barrier-crossing flags, street labels (all need Stage-1 feature kinds
not emitted yet); Gabriel pruning + the drawn ASCII view; scene-chunking; the tunable
settings surface.

## Projects (created this step)
- `MarkdownMap.Generator` (**netstandard2.0**, lean — no NTS, own geometry) → references Contract.
- `MarkdownMap.Generator.Tests` (net10).

## Tests (location-agnostic, all committable)
- **Committed fictional fixture:** `tests/.../fixtures/rivertown.geojson` (hand-authored,
  ~8–12 `poi`, placeholder coords near 45,-120).
- **Golden snapshot:** Generator output → committed `rivertown.expected.md`
  (generator-canonical: generate, eyeball, freeze; diff fails on regression).
- **Unit tests** (pure, synthetic): haversine; bearing→8-wind; bucket cutoffs; token order;
  nearest-k graph + symmetrization.

## Done when
`dotnet test` green (golden + units), and running the Generator on a local Stage-1 POI
GeoJSON yields a readable Connections map hand-reviewed as sensible. No real-location data
committed.
