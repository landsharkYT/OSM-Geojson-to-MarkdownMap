# OSM → MarkdownMap

Turns OpenStreetMap data into a text map an LLM can actually use. Run a roleplay or D&D
scene and the AI can tell where things are and how far apart.

It's a schematic, not drawn to scale. Each place gets a short `[NN]` token, and the lines
between them carry the real distance and compass direction. There's also terrain, districts,
and a reading key so nothing has to be guessed.

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

Connections skip the size bucket (you can work it out from the metres) and only call out
what actually matters: `(far)` for anything past a walkable 500m, `[crosses <road>]`,
`[separated by water]`, and `stands apart` on a feature that's cut off from its neighbors.
Names resolve in order: `name`, then `brand`, then `operator`.

The example above is a fictional area. Real OSM exports stay local, never in the repo.

## Pipeline

Two stages, connected only by GeoJSON:

```
.osm export ──▶ [Stage 1: Normalizer] ──▶ GeoJSON ──▶ [Stage 2: Generator] ──▶ MarkdownMap
                (OSM-aware)              (contract)    (OSM-agnostic)          (+ scene-chunks)
```

- **Normalizer** streams an OSM extract, drops noise, classifies tags into categories and
  importance scores, resolves names, snaps streets to nearby POIs, and builds terrain shapes.
  It's the only part of the pipeline that knows OSM's tag schema.
- **Generator** takes that GeoJSON and builds the actual map: which features get promoted,
  how they connect, how districts are grouped, where terrain sits. It renders the MarkdownMap
  from that model, so changing a display setting doesn't require re-parsing anything.
- **Contract** is the shared GeoJSON schema both stages agree on.
- **Explorer** (`web/`) is a React app that runs the whole pipeline client-side in the browser,
  using a .NET-compiled-to-WASM build of the Normalizer and Generator, and draws the map next
  to the MarkdownMap text.

## What it does

- Extracts POIs, streets, place names, barriers, and terrain from an OSM export, filtering
  out map clutter (benches, parking, etc.) and dropping junk/unnamed noise.
- Merges duplicate features, like a shop mapped as both a point and a building outline.
- Ranks features by narrative importance rather than raw density, so a hospital doesn't get
  buried under fifty benches, and a dense block of near-identical restaurants doesn't flood
  the map with all of them.
- Flags distance and terrain honestly: what's far, what's cut off by a road or water, and
  what's just plain isolated.
- Splits a big map into self-contained scene chunks, so an LLM can load one area without the
  whole map.
- Lets you show minor/background features as a deduplicated list instead of individual tokens.
- The Explorer's map view is limited to what the markdown text actually says, and warns you
  if the two ever disagree.

The reasoning behind each of these choices is recorded in `docs/adr/` (20 ADRs so far); the
shared vocabulary is in `CONTEXT.md`.

## Build & test

```bash
dotnet build
dotnet test          # location-agnostic; integration tests skip if no local .osm

cd web && npm install && npm test && npm run test:e2e
```

Run the full pipeline on a local export (you supply the `.osm`). The output extension picks
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

> Sample data isn't committed. Bring your own `.osm` (OSM Export / Geofabrik / Overpass).
> Files matching `*.osm` / `*.pbf` / `samples/` / `ProposedMap.md` are git-ignored, and the
> Explorer parses everything client-side, so nothing you drop in leaves the browser.

## Design docs

- [`CONTEXT.md`](CONTEXT.md): glossary of domain terms (start here).
- [`docs/adr/`](docs/adr/): Architecture Decision Records, 0001–0020.
- [`docs/feature-schema.md`](docs/feature-schema.md): the GeoJSON contract / normalized schema.
- [`docs/output-format.md`](docs/output-format.md): the MarkdownMap grammar.
- [`docs/map-model.md`](docs/map-model.md): the Generator's structured model.
- [`docs/settings.md`](docs/settings.md): tunable knobs.

## License

See repository.
