# Implementation step 4 — Barriers + Terrain

Agreed scope from the grill session. Completes the whole-area map's accuracy (crossing
flags) and orientation (terrain). Spans both stages. No contract schema change — `kind`,
`barrierClass`, and LineString/Polygon geometry are already in ADR-0006.

## Taxonomy (split by geometry)
- **Area water** (`natural=water` polygons: lakes/bays) → terrain `water`.
- **Linear waterway** (`waterway=river|canal` lines) → `barrier`, class `water`.
- **Roads** (`highway=motorway|motorway_link|trunk`) → `barrier`, class `motorway`.
- **Rail** (`railway=rail|light_rail`) → `barrier`, class `rail`.
- **Parks** (`leisure=park`, `landuse=recreation_ground`) → terrain `park`.

## Stage 1 (Normalizer) additions
- Emit `barrier` Features (LineString, simplified): label = `name` ?? `ref`; **skip if
  neither**. Carry `barrierClass`.
- Emit `water`/`park` Features (Polygon, simplified): **named only** (skip unnamed = noise).
- **Simplify** all emitted lines/rings with Douglas–Peucker (~5 m) via NetTopologySuite.
- *Deferred this slice:* multipolygon **relations** — ways only for now (may miss some
  large lakes); revisit if the smoke test shows gaps.

## Stage 2 (Generator) additions
- Readers for LineString (`double[][]`) and Polygon (`double[][][]`) — JsonElement-aware,
  like the existing Point reader.
- **Terrain block** (`## Terrain & barriers`): list named water/parks + barriers, each as
  `- <name> (<kind>) · <position> · <note>`. `kind` = `water` | `park` |
  `barrier:<class>`. **Position** (computed in Generator): octant of the feature's centroid
  vs map-bbox centre; `<dir> edge` if it spans >40 % of an axis; linear barriers as
  `runs <dir>, <side>`. **Note**: water→"open water", park→"green space",
  barrier→"impassable except at crossings". Skip unnamed. Deterministic order.
- **Crossing flags**: own segment–segment intersection (no NTS). For each promoted
  Connection edge, test its straight segment against every barrier segment; if it crosses,
  append `[crosses <barrier label>]` (first barrier by label if several). Bidirectional →
  both directions flagged.
- Update the How-to-read key + Terrain appears before Districts (per output-format order).

## Tests (location-agnostic, committable)
- Extend `rivertown.geojson`: add a named lake (water polygon), a park polygon, and a
  `Route 9` motorway barrier (LineString) positioned so two promoted features sit on
  opposite sides (one edge must cross it). Regenerate the golden.
- Unit tests: segment-intersection (crossing vs non-crossing, shared endpoint); terrain
  position octant + edge/run; line/polygon readers.

## Done when
`dotnet test` green (golden + units); running the CLI on a local `.osm` yields a Terrain
block of named features with sensible positions and at least one `[crosses X]` flag, hand-
reviewed. No real data committed.

## Outcome / follow-up
Built and green (78 tests). Smoke test on the sample extract: 44 barriers + 20 parks
emitted and 35 `[crosses …]` flags produced — but **water came back empty**: the lakes/bays
in that extract are multipolygon **relations**, which the ways-only Stage 1 doesn't read.
The Generator renders `water` fine (the fixture proves it); the gap is purely Stage-1
ingestion. **Immediate follow-up: read multipolygon water/park relations** so waterfront
areas show their defining water body.
