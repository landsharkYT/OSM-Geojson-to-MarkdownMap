# Implementation step 3 — Streets + Districts + Spine

Agreed scope from the grill session. Turns the flat v0 Connections list into a grouped,
street-labeled, globally-framed map. Spans both stages.

## Stage 1 (Normalizer) additions
- **Street attribution** on each `poi`: `street` = `addr:street` if present, else nearest
  named-road **segment** within ~30 m → `streetApprox=true`. Loads named-`highway` geometry
  in-memory (point-to-polyline distance). No `road` features emitted to the contract.
- **`place` anchor features**: emit `place=neighbourhood|quarter` nodes as `kind:"place"`
  Point Features (name + coordinates). These are the district anchors.

## Stage 2 (Generator) additions — the grouping layer (ADR-0007)
- **District assignment:** each promoted POI → nearest `place` anchor (Voronoi, no radius
  cap). If zero anchors, omit Districts (fall back to flat v0).
- **Promotion/clustering:** tier `landmark`+`destination` → promoted (own token);
  tier `minor` → per-district **"+N minor"** count. **Proximity graph runs over promoted
  features only** (minors are not graphed).
- **Spine:** per district, PCA principal axis of its promoted points → project + order;
  direction labeled by nearest cardinal (N–S / E–W / NE–SW); street name = mode of members'
  `street`. E.g. `spine: N–S along Main Street: [01],[03],[05]`.
- **Tokens** stay global (importance desc); districts reference global tokens.
- **Render** the Districts section (`### name — blurb` + `spine:`/`promoted:`/`clustered:`)
  ordered by promoted-count desc (name tiebreak); Connection headers now carry the optional
  `· on <street>` (`near` if approx) and `· <district>` segments.

## Out of scope (later)
`across:` cross-district lines (need barriers), Terrain (water/park), barrier-crossing
flags, de-duplication, the tunable settings surface.

## Tests (location-agnostic, committable)
- Extend `rivertown.geojson` fixture with 1–2 `place` anchors + `street` on POIs (and a few
  `minor`-tier features to exercise clustering). Regenerate the golden.
- Unit tests: street-snap (synthetic POI + road segment); Voronoi assignment; PCA spine
  direction + ordering; promotion/clustering counts.

## Done when
`dotnet test` green (golden + units); running the CLI on a local `.osm` yields a grouped,
street-labeled map hand-reviewed as sensible; minors clustered, promoted graphed. No real
data committed.
