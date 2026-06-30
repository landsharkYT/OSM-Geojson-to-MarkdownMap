# Feature schema & normalization rules (the Stage 1 ↔ Stage 2 contract)

Normative companion to ADR-0006. This is what the Normalizer emits and the Generator
consumes. `schemaVersion: 1`.

## 1. The GeoJSON envelope

A single `FeatureCollection`:

```jsonc
{
  "type": "FeatureCollection",
  "properties": {
    "schemaVersion": 1,
    "title": "Old Town & Harborside, Rivertown",
    "bounds": [-120.5100, 45.5200, -120.4800, 45.5400]   // [minlon,minlat,maxlon,maxlat]  (illustrative)
  },
  "features": [ /* typed Features, see below */ ]
}
```

## 2. Feature properties (all kinds)

| field | type | notes |
|---|---|---|
| `kind` | enum | `poi` \| `road` \| `barrier` \| `water` \| `park` \| `place` |
| `name` | string \| null | resolved `name → brand → operator`, `short_name` preferred when `name` is long (ADR-0012); null = unnamed (rendered as a humanized category label) |
| `osmId` | string | provenance, e.g. `n29445653`, `w12345`, `r123-0` (a relation split into outer rings suffixes `-k`, ADR-0014); Stage 2 ignores for ranking |
| `category` | string | normalized class.subclass (see §4); poi/place only |
| `importance` | int 0–100 | computed score (see §5); poi only |
| `tier` | enum | `landmark` \| `destination` \| `minor` \| `structure`; derived from `importance` |
| `street` | string \| null | poi only; `addr:street` or snapped road name (§6) |
| `streetApprox` | bool | poi only; true = snapped (render as `near` not `on`) |
| `barrierClass` | string | barrier only: `motorway` \| `rail` \| `water` |

Geometry per kind: `poi`/`place` → `Point`; `road`/`barrier` → `LineString`/`MultiLineString`;
`water`/`park` → `Polygon`/`MultiPolygon` (Douglas–Peucker simplified, tolerance ~5 m).

## 3. What becomes a Feature — the inclusion gate

Stage 1 emits a Feature **only** if it passes the gate; everything else is discarded
(not clustered):

- **Dropped entirely (RP noise):** `amenity` ∈ {parking, parking_space, parking_entrance,
  bench, bicycle_parking, waste_basket, post_box, drinking_water, recycling, toilets};
  `natural` ∈ {tree, scrub, wood, stone}; building furniture. These dominate raw counts
  and carry no scene value.
- **Kept as `poi`:** anything matching the taxonomy (§4), whether node or way.
- **Kept as context:** roads (named `highway`), barriers (§7), water/park (§4 terrain),
  place anchors (`place=neighbourhood|quarter`).
- Unnamed buildings that pass nothing are **not** Features; they survive only as a
  per-District **clustered count** ("+~40 minor/residential").

## 4. Category taxonomy (OSM → normalized)

Top-level class drives base importance. Subclass = the OSM value (passed through).

| class | OSM source (examples) | base tier |
|---|---|---|
| `landmark` | `tourism`=artwork/viewpoint; `amenity`=place_of_worship; `building`=church; `historic`=* | landmark |
| `civic` | `amenity`=school/library/post_office; `healthcare`/`amenity`=dentist/clinic/pharmacy | landmark |
| `food` | `amenity`=restaurant/cafe/bar/pub/fast_food; `shop`=deli | destination |
| `shop` | `shop`=* (convenience, hairdresser, beauty, bicycle, …) | destination |
| `leisure` | `leisure`=marina/fitness_centre/swimming_pool/playground/pitch/slipway | destination |
| `lodging` | `tourism`=hotel/hostel/guest_house | destination |
| `residential` | named `building`=house/apartments/**houseboat**/floating_home/detached | minor |
| `transit` | `highway`=bus_stop / `public_transport`=platform (mostly deferred) | minor |
| terrain → `water` | `natural`=water/coastline; `waterway`=canal/river; named bays/lakes | n/a |
| terrain → `park` | `leisure`=park; `landuse`=recreation_ground | n/a |

> Note: locally-characteristic building types (e.g. `houseboat`/`floating_home` in a
> waterfront area) are kept as `residential` so such colonies cluster with flavor rather
> than being dropped.

## 5. Importance formula (v1 default — tunable)

```
base   = { landmark:80, destination:55, minor:30 }[classBaseTier]
score  = base
       + (resolvedName != null ? 10 : 0)  // name|brand|operator (ADR-0012)
       + (class==landmark    ?  5 : 0)
       - (isChainNoise        ?  5 : 0)   // e.g. generic fast_food; optional
score  = clamp(score, 0, 100)

tier   = score>=75 ? landmark
       : score>=50 ? destination
       : score>=25 ? minor
       :             structure
```

Render policy (also tunable, see settings):
- `landmark` + `destination` → **Promoted** (own token).
- `minor` → **Clustered** by default; promoted only where a District is sparse.
- `structure` → count only.
- **Tiered unnamed promotion (ADR-0012):** a feature still **unnamed** after resolution is
  promoted only at `landmark` tier; unnamed `destination`/lower features are demoted to the
  District clustered count, so nameless noise never gets its own token.

## 6. Polygon → point, de-duplication, street snap

- **Representative point** (ways): polygon **centroid**; if the centroid lies outside the
  polygon, fall back to pole-of-inaccessibility (label point). v1 may ship centroid-only
  and upgrade later.
- **Co-location merge (ADR-0012):** after features are built, collapse co-located
  duplicates/fragments within **~40 m** of the same **category class**: an **unnamed** poi
  folds into a **named** one; two **same-name** pois collapse to one (survivor preference:
  named, then higher importance, then stable id). This catches the common case of one place
  mapped as both an `amenity` node *and* a `building` way, plus stray unnamed fragments around
  it. The radius + same-class guard keep distinct neighbours from being merged.
- **Street snap:** `street = addr:street` if present; else nearest `road` LineString
  within ~30 m → set `streetApprox=true`. Beyond that, `street=null`.

## 7. Barriers & terrain positioning

- **Barrier features** = ways with `highway` ∈ {motorway, motorway_link, trunk} (→
  `barrierClass=motorway`), `railway` ∈ {rail, light_rail} (→ `rail`), or
  `natural=water`/`waterway` ∈ {river, canal} (→ `water`). A POI–POI Connection whose
  straight segment intersects any barrier geometry gets `[crosses <name|ref>]`.
- **Terrain position** (for the Terrain block): compute the terrain feature's bbox centre
  vs the map bbox centre → 8-wind octant. If the feature spans >40 % of an axis, describe
  it as an **edge/run** ("W/SW edge", "runs N–S") rather than a point octant.

## 8. Worked mini-example (envelope abridged)

```jsonc
{ "type":"Feature", "properties":{
    "kind":"poi","name":"The Anchor Tavern","osmId":"n...",
    "category":"food.bar","importance":65,"tier":"destination",
    "street":"Main Street","streetApprox":false },
  "geometry":{"type":"Point","coordinates":[-120.5012,45.5231]} }

{ "type":"Feature", "properties":{
    "kind":"barrier","name":"Route 9","barrierClass":"motorway","osmId":"w..." },
  "geometry":{"type":"LineString","coordinates":[[...],[...]]} }
```
