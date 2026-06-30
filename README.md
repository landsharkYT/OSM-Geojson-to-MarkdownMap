# OSM → MarkdownMap

Turn OpenStreetMap data into an **LLM-digestible textual map**, so an AI running a
roleplay / D&D scene can reason about *where things are* relative to each other and
*how far apart* they are.

The map is a **topological schematic** (not drawn to scale): features are compact
`[NN]` tokens, and spatial truth is carried by **distance + 8-wind bearing annotations**
on connections plus a small **terrain/legend** — the form an LLM parses most reliably.

```
[05] The Anchor Tavern (bar) · on Main Street · Old Town
   → [06] Corner Cafe — ~10m N, adjacent
   → [08] Rivertown Market — ~50m SE, near
   → [11] Bluebird Coffee — ~430m E, short walk [crosses Route 9]
```

(Example uses a fictional area; real OSM exports are kept local, never committed.)

## Pipeline

Two decoupled stages with **GeoJSON as the hard contract** between them:

```
.osm export ──▶ [Stage 1: Normalizer] ──▶ GeoJSON ──▶ [Stage 2: Generator] ──▶ MarkdownMap
                (OSM-aware)              (contract)    (OSM-agnostic)
```

- **Normalizer** (`MarkdownMap.Normalizer`, net10 + OsmSharp/NetTopologySuite) — streams an
  OSM extract, applies an inclusion gate (drops noise), normalizes tags into a typed
  schema, and emits GeoJSON.
- **Generator** (`MarkdownMap.Generator`, netstandard2.0) — consumes GeoJSON, builds a
  structured `MapModel`, and renders the MarkdownMap from it. Depends only on the contract.
- **Contract** (`MarkdownMap.Contract`, netstandard2.0) — the shared GeoJSON/schema POCOs.
- **Explorer** (`web/`) — a React app that runs the whole pipeline **in the browser** via a
  .NET WASM module (`MarkdownMap.Wasm` / `MarkdownMap.Bridge`) and draws the map (tokens,
  edges, districts, terrain, crossings) beside the MarkdownMap. Nothing is uploaded (ADR-0009).

## Status

| Part | State |
|---|---|
| Stage 1 Normalizer — POIs, street-snap, place anchors, barriers, terrain (ways + relations) | **built** |
| Stage 2 Generator — MarkdownMap + structured MapModel (districts, spines, crossing flags) | **built** |
| Stage 1 — de-duplication | planned |
| WASM module (browser pipeline) | **built** |
| React Explorer (`web/`) — visual map + markdown, client-side | **built (v1)** |
| Settings playground, scene chunking | deferred |

## Build & test

```bash
dotnet build
dotnet test          # location-agnostic; integration tests skip if no local .osm
```

Run the full pipeline on a local export (you supply the `.osm`); output extension picks
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

> **Sample data is not committed.** Bring your own `.osm` (OSM Export / Geofabrik /
> Overpass). Files matching `*.osm`, `samples/`, and `ProposedMap.md` are git-ignored. The
> Explorer parses everything client-side — your data never leaves the browser.

## Design docs

The design is captured up front:

- [`CONTEXT.md`](CONTEXT.md) — glossary of domain terms.
- [`docs/adr/`](docs/adr/) — Architecture Decision Records (the founding decisions).
- [`docs/feature-schema.md`](docs/feature-schema.md) — the GeoJSON contract / normalized schema.
- [`docs/output-format.md`](docs/output-format.md) — the MarkdownMap grammar.
- [`docs/settings.md`](docs/settings.md) — tunable knobs (post-prototype).
- [`docs/impl-step-1.md`](docs/impl-step-1.md) — the first implementation slice.

## License

See repository.
