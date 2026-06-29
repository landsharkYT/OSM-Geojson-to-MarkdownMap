# Implementation step 6 — React helper: visual Map Explorer

Agreed scope from the grill session. A browser app that runs the C# pipeline client-side
(ADR-0009) and **visualizes the Generator's full structure** at true geographic positions,
alongside the MarkdownMap it produces.

## Architecture
- **Client-side .NET WASM** (ADR-0009): `.osm`/GeoJSON parsed + generated entirely in the
  browser; nothing uploaded.
- **MapModel** (new): the Generator builds a structured model — features (token, lon/lat,
  category, importance, street, district), edges (i→j, metres, bearing, bucket, crosses?),
  districts (name, anchor, spine, promoted/clustered counts), terrain (kind, geometry,
  position) — and renders the markdown *from* it. The WASM module returns MapModel JSON
  (incl. the rendered markdown) to React.

## Build order
1. **WASM feasibility spike** — ✅ **DONE / GO.** A `wasmconsole` project ran the real
   Normalizer (OsmSharp XML parse + NTS way-centroid) + Generator on a synthetic `.osm`
   inside the browser-wasm runtime, invoked via `[JSExport]` from JS, and returned a
   correct MarkdownMap. OsmSharp + NetTopologySuite are viable in WASM; the client-side
   architecture (ADR-0009) holds. (Prereq done: Normalizer is now a *library*, not an exe,
   so it can be referenced by the wasm module; its `.geojson` debug dump moved into the CLI.)
2. **MapModel refactor** (C#) — extract the Generator's computed structures into a
   serializable `MapModel`; markdown renderer reads it. No behaviour change (golden holds).
3. **`MarkdownMap.Wasm`** — `[JSExport] buildMap(inputBytesOrText, optionsJson) → MapModel
   JSON`. Accepts `.osm` (full pipeline) and `.geojson` (Generator only).
4. **React app** (`/web`): Vite + React + TS + Tailwind.

## v1 features (the explorer)
- **SVG map** (d3-geo projection, **no basemap tiles**): terrain polygons (water/park) as
  background, barrier lines, district-colored token dots, proximity edges, crossing edges
  marked. Pan/zoom.
- **Interactions:** hover/click a feature → details panel (name, category, importance,
  street, district, neighbours); layer toggles (tokens / edges / districts / terrain /
  crossings).
- **Markdown panel:** the generated MarkdownMap side-by-side, with copy + download.
- Drag-drop / file-pick a local `.osm` or `.geojson`.

## Out of scope (v1)
Settings tuning (v2 — the playground), chunking, topological/force-directed view, basemap
tiles, persistence/accounts, server.

## Tests
- C#: MapModel refactor keeps the existing golden green (markdown unchanged); a small test
  that MapModel JSON round-trips and matches the markdown's tokens/edges.
- Web: component tests for projection + render of a small fixture MapModel (the fictional
  Rivertown model — committable); no real data.

## Done when
The spike proves WASM viability; the app loads a local `.osm`, shows the map + markdown,
and interactions work — all client-side, hand-reviewed. No real data committed.
