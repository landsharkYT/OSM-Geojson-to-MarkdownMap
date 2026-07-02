# OSM → MarkdownMap

Turn OpenStreetMap data into an **LLM-digestible textual map**, so an AI running a
roleplay / D&D scene can reason about *where things are* relative to each other and
*how far apart* they are — **location persistence** for an LLM Dungeon Master.

The map is a **topological schematic** (not drawn to scale): features are compact `[NN]`
tokens, and spatial truth is carried by **rounded metres + 8-wind bearings** on connections,
a **terrain/barrier** block, per-area **districts** with a spine, and an always-present
**reading key** — the form an LLM parses most reliably.

```
## Terrain & barriers
- Mill Lake (water) · W edge
- Route 9 (barrier:motorway) · runs N–S, E · impassable except at crossings

## Districts
### Old Town — Main Street area
spine: N–S along Main Street: [01],[02],[03],[05],[06]
promoted: 5
clustered: ~2 minor

## Connections
[01] Founders Mural (landmark.artwork) · Old Town
   → [02] — ~25m SE
   → [05] — ~80m S
```

Connections drop the size bucket (it's derivable from the metres) and flag only what carries
real information: `(far)` (≥500 m, not a walkable local hop), `[crosses <road>]`,
`[separated by water]`, and a per-feature `stands apart`. Names come from `name → brand → operator`.

*(Example uses a fictional area. Real OSM exports are kept local, never committed.)*

## Pipeline

Two decoupled stages with **GeoJSON as the hard contract** between them:

```
.osm export ──▶ [Stage 1: Normalizer] ──▶ GeoJSON ──▶ [Stage 2: Generator] ──▶ MarkdownMap
                (OSM-aware)              (contract)    (OSM-agnostic)          (+ scene-chunks)
```

- **Normalizer** (`MarkdownMap.Normalizer`, .NET 10 + OsmSharp/NetTopologySuite) — streams an OSM
  extract, applies an inclusion gate (drops noise), classifies tags into a typed schema
  (`category`, `importance`, `tier`, `salience`), resolves names, snaps streets, and assembles
  terrain geometry (way rings + relation multipolygons). The only OSM-aware component.
- **Generator** (`MarkdownMap.Generator`, netstandard2.0) — consumes GeoJSON, builds a structured
  `MapModel` (promoted features, proximity edges, districts, terrain, minors), and renders the
  MarkdownMap as a *view over the model*, so render-only settings re-render with no re-parse.
- **Contract** (`MarkdownMap.Contract`, netstandard2.0) — the shared GeoJSON/schema POCOs + the
  narrative-salience classifier.
- **Explorer** (`web/`) — a React 19 + Vite + Tailwind app that runs the whole pipeline **in the
  browser** via a .NET WASM module (`MarkdownMap.Wasm` / `MarkdownMap.Bridge`) and draws the map
  (tokens, edges, districts, terrain, crossings) beside the MarkdownMap. Nothing is uploaded (ADR-0009).

## What it does

| Capability | Notes |
|---|---|
| Stage 1 extraction | POIs, street-snap, place anchors, barriers, terrain (ways + relations); institutional buildings, piers/dorms, natural terrain, cemeteries; junk-name gate |
| Co-location merge | collapses a place mapped as both a node and a building way (ADR-0012) |
| Narrative salience + promotion budget | per-district `core / budgeted / clustered` with a top-K budget; chain penalty; civic allowlist (ADR-0018) |
| Honest distance/terrain signals | `(far)`, `[crosses]`, `[separated by water]`, `stands apart`, orienting-scale terrain filter |
| Scene chunking | self-contained per-area chunks + manifest for retrieval (ADR-0016/0017) |
| Minor vs prop features | opt-in per-district list of named set-dressing, deduped `×N` (ADR-0020) |
| Explorer | client-side WASM pipeline; **honest-by-default** SVG (shows only what the AI sees) with an adaptive map-vs-markdown warning; hover-inspectable dots; layer toggles |

Decisions are recorded in **ADRs 0001–0020**; the ubiquitous language lives in `CONTEXT.md`.

## Build & test

```bash
dotnet build
dotnet test          # location-agnostic; integration tests skip if no local .osm

cd web && npm install && npm test && npm run test:e2e
```

Run the full pipeline on a local export (you supply the `.osm`); the output extension picks
MarkdownMap (`.md`) or the Stage-1 GeoJSON (`.geojson`):

```bash
dotnet run --project src/MarkdownMap.Cli -- your-area.osm out.md
dotnet run --project src/MarkdownMap.Cli -- your-area.osm out.geojson
```

### Explorer (web)

```bash
cd web
npm install
npm run build:wasm     # publishes MarkdownMap.Wasm → web/public/dotnet (needs the wasm-tools .NET workload)
npm run dev            # then open the dev URL and drop in a .osm / .geojson
```

> **Sample data is not committed.** Bring your own `.osm` (OSM Export / Geofabrik / Overpass).
> Files matching `*.osm` / `*.pbf` / `samples/` / `ProposedMap.md` are git-ignored. The Explorer
> parses everything client-side — your data never leaves the browser.

## Design docs

- [`CONTEXT.md`](CONTEXT.md) — glossary of domain terms (start here).
- [`docs/adr/`](docs/adr/) — Architecture Decision Records, 0001–0020.
- [`docs/feature-schema.md`](docs/feature-schema.md) — the GeoJSON contract / normalized schema.
- [`docs/output-format.md`](docs/output-format.md) — the MarkdownMap grammar.
- [`docs/map-model.md`](docs/map-model.md) — the Generator's structured model.
- [`docs/settings.md`](docs/settings.md) — tunable knobs.

## License

See repository.
