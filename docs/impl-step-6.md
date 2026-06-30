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
2. **MapModel refactor** (C#) — ✅ **DONE.** `MapGenerator.BuildModel(fc) → MapModel`;
   markdown rendered from it; golden byte-identical (45 Generator tests). See docs/map-model.md.
3. **`MarkdownMap.Wasm`** — ✅ **DONE.** Logic in `MarkdownMap.Bridge` (`MapBuilder.FromOsm`
   / `FromGeoJson` → MapModel JSON; errors as `{"error":…}`; unit-tested). The wasm exe is a
   thin `[JSExport]` shim (`BuildFromOsm(byte[])`, `BuildFromGeoJson(string)`) and builds for
   browser-wasm (full pipeline linked). It is **not in the .sln** (needs the wasm-tools
   workload; build with `dotnet build -c Release` under a wasm-capable SDK).
4. **React app** (`/web`): Vite + React + TS + Tailwind. ← next

## v1 features (the explorer)
- **SVG map** (d3-geo projection, **no basemap tiles**): terrain polygons (water/park) as
  background, barrier lines, district-colored token dots, proximity edges, crossing edges
  marked. Pan/zoom.
- **Interactions:** hover/click a feature → details panel (name, category, importance,
  street, district, neighbours); layer toggles (tokens / edges / districts / terrain /
  crossings).
- **Markdown panel:** the generated MarkdownMap side-by-side, with copy + download.
- Drag-drop / file-pick a local `.osm` or `.geojson`.

## Step 4 — React app (resolved plan)
- **Stack:** Vite + React + TS + Tailwind in `/web`. State = plain `useState`
  (model, selectedFeature, layers, loading, error). Hand-written TS `MapModel` types.
- **WASM bundle:** a **build artifact**, not committed. `npm run build:wasm` runs
  `dotnet publish` on `MarkdownMap.Wasm` and copies `wwwroot/_framework` into
  `web/public/dotnet/` (git-ignored). A singleton `useDotnet()` hook dynamic-imports
  `/dotnet/_framework/dotnet.js`, calls `dotnet.create()` + `getAssemblyExports` once, and
  exposes `buildFromOsm(bytes)` / `buildFromGeoJson(text)`.
- **Input:** drag-drop + file picker. `.osm` → `Uint8Array` → `BuildFromOsm`; `.geojson` →
  text → `BuildFromGeoJson`. Everything client-side; a visible "runs in your browser,
  nothing is uploaded" note. No bundled demo (open a file to begin). Loading spinner;
  `{"error"}` surfaced.
- **Layout:** map fills left ~60%; right sidebar = layer toggles, selected-feature details
  card, then the MarkdownMap (raw monospace, copy + download).
- **Map render:** SVG. Own lon/lat→x/y projection (cos-lat aspect, fit to viewBox);
  **d3-zoom** for pan/zoom on a `<g>` transform. Layers back-to-front: terrain
  (water/park polygons, barrier polylines) → edges (crossing edges styled distinctly) →
  minor dots → promoted token dots + `[NN]` labels. Hover/click a feature → details;
  toggles flip layer visibility.
- **Tests:** Vitest + React Testing Library — the projection function, and rendering a
  small **committed Rivertown MapModel JSON** fixture (fictional; no real data).
- **Build/deploy:** `build:wasm` then `vite build` → static site (GH-Pages-deployable).
  Git-ignore `web/node_modules`, `web/dist`, `web/public/dotnet`.

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
