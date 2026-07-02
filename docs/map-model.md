# MapModel: the Generator's structured output (steps 1–2)

The Generator builds a MapModel (structured data). The MarkdownMap is rendered from it, and
the WASM module serializes it to JSON for the Explorer: one source of truth, two views.
Lives in `MarkdownMap.Generator`. Serialized camelCase via System.Text.Json.

## Shape

```
MapModel
  title        : string
  bounds       : [minLon, minLat, maxLon, maxLat]
  features     : PromotedFeature[]    // tokenized, graphed
  minors       : MinorFeature[]       // clustered in markdown; plotted in the Explorer
  edges        : Edge[]               // proximity graph over promoted
  districts    : District[]
  terrain      : TerrainEntry[]
  markdown     : string               // the rendered MarkdownMap (so JS need not re-render)

PromotedFeature : { token, name, category, importance, tier, street?, streetApprox?, district?, lon, lat }
MinorFeature    : { name, category, district?, lon, lat }
Edge            : { fromToken, toToken, meters, dir (8-wind), bucket, crosses? (barrier label) }
District        : { name, spineDir, spineTokens[], promotedCount, clusteredCount, anchorLon, anchorLat }
TerrainEntry    : { name, kind ("water"|"park"|"barrier:<class>"), position, note,
                    geometry: { type ("LineString"|"Polygon"), coords: [[lon,lat],...] } }
```

Notes:
- `markdown` is byte-identical to today's Generator output. The renderer reads MapModel,
  but the string must not change (the golden test enforces this).
- Static prose (Directive Preamble, How-to-read) is produced by the renderer, not stored
  in the model. The renderer derives section presence from the data (`districts.length`,
  `terrain.length`).
- `edges` are emitted per the bidirectional default (both directions), matching markdown.

## WASM module (`MarkdownMap.Wasm`, browser-wasm)

Two `[JSExport]` methods, each returning MapModel JSON (errors as `{"error": "..."}`):
```
string BuildFromOsm(byte[] osmBytes)      // full pipeline: Normalizer → Generator
string BuildFromGeoJson(string geojson)   // Generator only
```
No options parameter (settings deferred to v2). The build produces an AppBundle the
Explorer (`/web`, step 3) imports.

## Build steps (1–2)
1. Add `MapModel` POCOs + `MapGenerator.BuildModel(fc)`; refactor the section renderers to
   read MapModel; `Generate(fc) = Render(BuildModel(fc))`. **Golden stays byte-identical.**
2. `MarkdownMap.Wasm` project with the two `[JSExport]` methods; `MarkdownMap.Wasm.Tests`
   calls them as plain C# against the Rivertown fixture and asserts MapModel JSON shape.

## Tests
- Existing Generator golden unchanged (markdown identical after refactor).
- New: `BuildModel(rivertown)` has the right token/edge/district/terrain/minor counts;
  MapModel JSON round-trips; `BuildFromGeoJson` returns JSON whose `markdown` equals the
  golden.
