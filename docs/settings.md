# Tunable Settings Registry

Knobs the Generator (and Normalizer) should expose. v1 ships the **default**; these
become user settings after the first prototype. None are hardcoded constants buried in
logic; they live here and flow from config.

| Setting | Default | Notes |
|---|---|---|
| `connections.bidirectional` | **on** | Print each edge under *both* endpoints. Off = print each pair once (under the lower token) to ~halve connection tokens, at the cost of cross-referencing. Off is friendlier to large maps / tight context budgets; on is friendlier to weak LLMs. |
| `connections.neighborsPerFeature` | 3 | Nearest-N kept per feature before planar pruning. |
| `connections.inlineNeighborName` | **on** | Print the neighbour's name on each connection line. Off = `→ [03] — ~15m N, adjacent` (resolve token via header), saving ~25-30% of connection tokens at the cost of cross-referencing. Dumb-LLM insurance, same trade-off family as `bidirectional`. |
| `distance.showWalkTime` | off | Append `~Nmin` walk time (derived from metres at a configurable pace). Off by default: the bucket word already proxies effort, and once transit lands travel time is mode-dependent. |
| `distance.buckets` | adjacent<25m · near<150m · short walk<500m · far≥500m | Cutoffs + labels both editable. |
| `distance.showMeters` | on | If off, show only the bucket word. |
| `bearing.winds` | 8 | 4 / 8 / 16-wind. |
| `importance.ranking` | landmark/civic/amenity/shop > named building > minor/unnamed | Tier weights + which tiers get **promoted** vs **clustered**. Formula in `docs/feature-schema.md` §5. |
| `features.dropList` | parking/bench/bicycle_parking/waste_basket/tree/… | Inclusion-gate drop set (RP noise). `docs/feature-schema.md` §3. |
| `features.categoryMap` | built-in OSM→class table | The taxonomy in `docs/feature-schema.md` §4; overridable. |
| `features.dedupRadius` | ~5 m | Merge a POI node into a containing/near POI building. |
| `features.streetSnapRadius` | ~30 m | Max distance to snap a feature to a road for `street`. |
| `districts.source` | place-nodes (nearest-wins) | Fallback: geometric clustering where no place coverage. |
| `districts.spine` | on | Emit the per-District ordered-axis spine line. |
| `features.streetLabels` | on | Label each Feature with its street (`addr:street` else nearest-road snap). |
| `connections.barrierFlags` | on | Flag edges whose straight line crosses a barrier way (e.g. `[crosses Route 9]`). |
| `render.terrain` | on | Emit the Terrain & barriers context block (water, parks, barriers). |
| `tokens.scheme` | sequential `[NN]` | Stable within one generation. |
| `render.directivePreamble` | **on** | Emit the optional LLM framing block (authoritative-ground-truth + reading key + anchor). Off when the user supplies their own system prompt or a non-LLM consumes the map. Mode-aware (whole-area vs scene-chunk). See ADR-0005. |
| `render.mode` | whole-area | `whole-area` (v1) or `scene-chunk` (deferred). Drives the preamble template, anchor handling, and off-map edges. |
| `render.asciiDiagram` | off | Deferred drawn-ASCII view; only viable for small maps. |
