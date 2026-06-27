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
- **Generator** (planned) — consumes GeoJSON and renders the MarkdownMap. Depends only on
  the contract, so it stays portable.
- **Contract** (`MarkdownMap.Contract`, netstandard2.0) — the shared GeoJSON/schema POCOs.

## Status

| Part | State |
|---|---|
| Stage 1 Normalizer — POIs → GeoJSON (taxonomy, importance, inclusion gate, centroids) | **built** |
| Stage 1 — dedup, street-snap, barriers, terrain, districts | planned |
| Stage 2 Generator (GeoJSON → MarkdownMap) | designed, not built |
| React webapp helper | deferred |

## Build & test

```bash
dotnet build
dotnet test          # location-agnostic; integration tests skip if no local .osm
```

Run the Normalizer on a local export (you supply the `.osm`):

```bash
dotnet run --project src/MarkdownMap.Normalizer -- your-area.osm out.geojson
```

> **Sample data is not committed.** Bring your own `.osm` (OSM Export / Geofabrik /
> Overpass). Files matching `*.osm`, `samples/`, and `ProposedMap.md` are git-ignored.

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
