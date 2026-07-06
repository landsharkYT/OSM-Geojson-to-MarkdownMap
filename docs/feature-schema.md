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
| `name` | string \| null | resolved `name → brand → operator`, `short_name` preferred when `name` is long (ADR-0012); a bare label (single char or all digits, e.g. a dorm wing `A`, building number `12`) is rejected to null; null renders as a humanized category label |
| `osmId` | string | provenance, e.g. `n29445653`, `w12345`, `r123-0` (a relation split into outer rings suffixes `-k`, ADR-0014); Stage 2 ignores for ranking |
| `category` | string | normalized class.subclass (see §4); poi/place only |
| `importance` | int 0–100 | computed score (see §5); poi only |
| `tier` | enum | `landmark` \| `destination` \| `minor` \| `structure`; derived from `importance` |
| `salience` | enum | `core` \| `budgeted` \| `clustered`; drives promotion (ADR-0018); poi only |
| `street` | string \| null | poi only; `addr:street` or snapped road name (§6) |
| `streetApprox` | bool | poi only; true = snapped (render as `near` not `on`) |
| `barrierClass` | string | barrier only: `motorway` \| `rail` \| `water` |

Geometry per kind: `poi`/`place` → `Point`; `road`/`barrier` → `LineString`/`MultiLineString`;
`water`/`park` → `Polygon`/`MultiPolygon` (Douglas–Peucker simplified, tolerance ~5 m).

## 3. What becomes a Feature: the inclusion gate

Stage 1 emits a Feature only if it passes the gate. Everything else is discarded, not
clustered:

- Dropped entirely as RP noise: `amenity` ∈ {parking, parking_space, parking_entrance,
  bench, bicycle_parking, waste_basket, post_box, drinking_water, recycling, toilets};
  `natural` ∈ {tree, scrub, wood, stone}; building furniture. These dominate raw counts
  and carry no scene value.
- Kept as `poi`: anything matching the taxonomy (§4), whether node or way.
- Kept as context: roads (named `highway`), barriers (§7), water/park (§4 terrain),
  place anchors (`place=neighbourhood|quarter`).
- Unnamed buildings that pass nothing are not Features. They survive only as a per-District
  clustered count ("+~40 minor/residential").

## 4. Category taxonomy (OSM → normalized)

Top-level class drives base importance. Subclass = the OSM value (passed through).

| class | OSM source (examples) | base tier |
|---|---|---|
| `landmark` | `tourism`=artwork/viewpoint; `amenity`=place_of_worship; `building`=church; `historic`=*; `man_made`=pier (core, since a moorage is the waterfront's address) / tower/lighthouse/water_tower/bridge/obelisk/windmill/communications_tower (set-dressing, base 45) | landmark |
| `civic` | Core is an allowlist of public institutions (`amenity`=school/college/university/library/hospital/post_office/townhall/police/fire_station/courthouse/community_centre/kindergarten, a named station, and the institution buildings `building`=school/hospital/civic/fire_station), base 80. Everything else civic defaults to budgeted at the venue band (base 55, 65 with a name): private `amenity`=dentist/clinic/doctors/pharmacy/veterinary/social_facility plus `healthcare`=* passthrough, and the campus hall `building`=university/college → `civic.<t>_building` (plus a footprint-area nudge, ADR-0019); `office`=government/educational_institution/research/research_institute/diplomatic sits below venues (base 45) | landmark / destination |
| `food` | `amenity`=restaurant/cafe/bar/pub/fast_food; `shop`=deli | destination |
| `shop` | `shop`=* (convenience, hairdresser, beauty, bicycle, …); `craft`=* (brewery/distillery/repair); `amenity`=bank/fuel/marketplace (commercial services, chain-penalised) | destination |
| `leisure` | `leisure`=marina/fitness_centre/playground/pitch/slipway (pool/rink are budgeted facilities); `amenity`=theatre/cinema/arts_centre/events_venue/nightclub/boat_rental/spa/makerspace/studio/coworking_space (entertainment/recreation venues) | destination |
| `lodging` | `tourism`=hotel/hostel/guest_house (budgeted); `amenity`=student_accommodation or `building`=dormitory (dorms are core, a residential address like a moorage) | destination / core |
| `residential` | named `building`=house/apartments/houseboat/floating_home/detached | minor |
| `transit` | `highway`=bus_stop / `public_transport`=platform (mostly deferred) | minor |
| terrain → `water` | `natural`=water/bay/coastline; `waterway`=canal/river; named lakes | n/a |
| terrain → `park` | `leisure`=park; `landuse`=recreation_ground/cemetery or `amenity`=grave_yard (a named graveyard is a walkable area you go to); `natural`=beach/wetland (coarse shore/marsh, area-filtered) | n/a |

> Note: locally-characteristic building types (e.g. `houseboat`/`floating_home` in a
> waterfront area) are kept as `residential` so such colonies cluster with flavor rather
> than being dropped.

## 5. Importance & promotion (v1 default, tunable)

```
base   = { core-landmark:80, civic-institution:80,
           campus-hall|civic-private|food|shop|leisure|lodging:55, civic-office:45,
           commemorative-landmark:45, residential:30 }[category]
score  = base
       + (resolvedName != null ? 10 : 0)   // name|brand|operator (ADR-0012)
       + (class==landmark      ?  5 : 0)
       - (isChain              ? 15 : 0)    // has a `brand`/`brand:wikidata` tag (ADR-0018)
       + areaBonus              // ADR-0019: campus halls only, 0..+9 log ramp on footprint m² (in the Normalizer)
score  = clamp(score, 0, 100)

tier   = score>=75 ? landmark : score>=50 ? destination : score>=25 ? minor : structure
```

`commemorative-landmark` is the budgeted landmark subclasses (artwork, viewpoint, memorial, monument).
Base 45 keeps them below interactive venues (food/shops score ≈ 65 with a name), so a district's
cafés and shops win the budget before its sculptures do (a DM's party enters buildings, walks past art).

Narrative salience (ADR-0018) is computed from the `category` (see `SalienceClassifier`) and decides
promotion; `tier`/`importance` only order features.

`core` always promotes: worship; civic institutions (an allowlist of school, college, university,
library, hospital, post office, townhall, police, fire station, courthouse, community centre,
kindergarten); singular institution buildings (`building`=school/hospital/civic/fire_station/stadium,
and a named station); dorms (`amenity`=student_accommodation or `building`=dormitory); historic;
museum/gallery/attraction; major leisure venues (marina, stadium, sports centre, golf course).

`budgeted` competes for a promoted token, up to the per-District budget (top-K by importance); the
rest cluster. It covers commemorative landmarks (artwork, viewpoint, memorial, monument, base 45);
food; shops; campus halls (`building`=university/college, base 55 plus footprint nudge, since there
are many per campus so they compete like venues, ADR-0019); private/other civic (dentist, clinic,
doctors, pharmacy, veterinary, `healthcare`=* passthrough, social_facility, i.e. everything civic not
in the core allowlist, base 55); leisure facilities (pool, ice rink, gym, playground, pitch, but not
the marina/stadium/sports-centre/golf venues, which are core); and lodging. Venues, including halls,
outrank commemoratives.

`clustered` is count-only: residential, plus the `minor`/`structure` tiers.

Unnamed promotion (worship-only, ADR-0012 refined): an unnamed feature earns a token only if
it's worship ("the church"). Every other unnamed feature clusters regardless of its salience, so a
campus's nameless pool basins, sculptures, and out-buildings never become category-label tokens.

## 6. Polygon → point, de-duplication, street snap

- Representative point (ways): polygon centroid. If the centroid lies outside the polygon,
  fall back to pole-of-inaccessibility (label point). v1 may ship centroid-only and upgrade
  later.
- Co-location merge (ADR-0012): after features are built, collapse co-located
  duplicates/fragments within ~40 m of the same category class. An unnamed poi folds into a
  named one; two same-name pois collapse to one (survivor preference: named, then higher
  importance, then stable id). This catches the common case of one place mapped as both an
  `amenity` node and a `building` way, plus stray unnamed fragments around it. The radius and
  same-class guard keep distinct neighbours from being merged.
- Street snap: `street = addr:street` if present, else the nearest `road` LineString within
  ~30 m, which sets `streetApprox=true`. Beyond that, `street=null`.

## 7. Barriers & terrain positioning

- Barrier features are ways with `highway` ∈ {motorway, motorway_link, trunk} (→
  `barrierClass=motorway`), `railway` ∈ {rail, light_rail} (→ `rail`), or
  `natural=water`/`waterway` ∈ {river, canal} (→ `water`). A POI-to-POI Connection whose
  straight segment intersects any barrier geometry gets `[crosses <name|ref>]`.
- Terrain position (for the Terrain block): compute the terrain feature's bbox centre versus
  the map bbox centre, giving an 8-wind octant. If the feature spans more than 40% of an
  axis, describe it as an edge or run ("W/SW edge", "runs N–S") rather than a point octant.

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
