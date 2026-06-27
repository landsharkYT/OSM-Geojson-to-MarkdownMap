# Context: OSM → MarkdownMap

This project turns OpenStreetMap data into an LLM-digestible textual map, so an
AI running a roleplay / D&D scene can reason about *where things are* relative to
each other and *how far apart* they are.

## Glossary

### MarkdownMap
The final output artifact. A **topological schematic** of an area rendered in a
markdown code block: features shown as compact tokens connected by lines that
preserve relative compass direction and adjacency. It is *not* drawn to scale —
spatial truth is carried by **distance + bearing annotations** on connections and
by a **Legend**, not by character spacing.

### Feature
A single named place of interest on the map (a building, amenity, road junction,
landmark). Drawn as a short bracketed **Token** (e.g. `[42]`) on the schematic and
expanded in the Legend.

### Token
The compact on-map label for a Feature (e.g. `[42]`). Kept short so it never breaks
the ASCII grid or wastes LLM tokens. The human/LLM-readable name lives only in the
Legend.

### Legend
The markdown key that maps each Token to its full name (and any carried metadata).
Lives alongside the schematic, e.g. `[42]: Old Town Library`.

### Connection
A link drawn between two Features on the schematic. Annotated with a **standardized
distance** and a **bearing** (compass direction), since the layout itself is not to
scale.

### Importance Tier
A rank assigned to each Feature that decides whether it is **Promoted** (shown as its
own Token) or **Clustered** (folded into a District). v1 default: landmarks/amenities/
shops/civic rank above named buildings, which rank above minor/unnamed structures.
The ranking is a **tunable default, never hardcoded** — exposed as user settings after
the first prototype.

### District (Cluster)
A named group of lower-tier Features shown as a single block instead of individual
Tokens, e.g. "Harborside (+38 minor buildings)". Keeps a whole-area
MarkdownMap legible without scene-chunking. **Derived from real OSM
`place=neighbourhood`/`quarter` nodes** (e.g. Old Town, Harborside, Mill District,
Riverside ...) as district anchors: each Feature joins its nearest anchor
(Voronoi). Geometric-cluster fallback only where no place coverage exists.

### Scope (v1)
A single MarkdownMap renders the **entire ingested OSM extract**. Scene-radius /
focal-anchor chunking is a deferred later feature, not part of v1. Chunking is not just
a smaller map — it **reinterprets** the document: it carries an intrinsic **Anchor** and
**Off-map edges**, and its Directive Preamble reframes the map as "immediate
surroundings" rather than "the world". See ADR-0005.

### Directive Preamble
An optional, clearly-delimited block at the top of a MarkdownMap that instructs the
consuming LLM (treat as authoritative geography, how to read a Connection line, where the
Anchor is). Toggleable and **mode-aware** — kept separate from the map data so it never
pollutes non-LLM consumers. See ADR-0005.

### Transit (deferred)
Public-transport features. OSM carries them richly (named bus stops by cross-street,
bus/trolleybus/light-rail **route relations** with operators + route numbers). Splits
like roads (see ADR-0003): **stops** are cheap Features that can land early; **routes**
are ordered **relations** needing the same topology preservation the Normalizer must do
deliberately. Routes are the *long-range, cross-District* connective tissue the proximity
graph cannot express, and make travel time **mode-dependent** (walk vs bus vs rail).

### Anchor (deferred)
"You are here." The focal Feature a scene-chunk map is built around. Intrinsic to
chunked maps; absent (or user-stated) in whole-area maps.

### Off-map edge (deferred)
A marker on a scene-chunk map's boundary pointing toward what continues beyond it
(e.g. "→ N: toward Harborside, off-map"), so the LLM does not treat the map edge as the
edge of the world.

### Source (acquisition)
Where raw map data comes from: a **static OSM export file** (`.osm`/`.pbf`) the user
downloads once per area (OSM Export / Geofabrik / Overpass) and feeds the pipeline
offline. No runtime API. A locally-supplied `.osm` extract is one. A live puller can slot in later
behind the same seam, but is out of scope. Acquisition is a manual, documented step, not
built code.

### Normalizer (Stage 1)
The OSM-aware front stage. Streams an OSM/XML extract and emits **GeoJSON**,
translating raw OSM tags (`amenity`, `shop`, `building`, ...) into a stable,
normalized **Feature property schema** (e.g. `name`, `category`, `importance`).
The only component that knows OSM.

### Generator (Stage 2)
The OSM-agnostic back stage. Consumes **GeoJSON** (from the Normalizer or any other
source) and produces the **MarkdownMap**. Ranks Features and draws Connections using
only the normalized property schema — never raw OSM tags. The **GeoJSON contract** is
the hard boundary between the two stages.

### Street (attribute)
The named road a Feature sits on, carried as a property (`addr:street` tag, else
nearest-named-road snap). v1 ships street *labels*, not street *routing* — see ADR-0003.

### Barrier / crossing flag
A `[crosses <barrier>]` flag on a Connection whose straight-line segment intersects a
known impassable barrier way (e.g. a motorway's geometry). Warns that the proximity edge
is not actually walkable directly. Computed by segment-vs-polyline intersection.

### Spine
A one-line ordering of a District's promoted Features along its main axis (usually a
Street), e.g. "N–S along Main Street: [02],[01],[03],...". Gives the LLM a global
frame so it need not triangulate per-Feature bearings.

### Terrain
The orienting context block: named water bodies, parks, and Barriers with their rough
position, so the LLM knows "the shape of the place" (what's water, what blocks movement).

### Topology (connectivity)
OSM encodes the street/path **graph** via shared node IDs (a node in two ways = an
intersection). Plain GeoJSON cannot represent this — geometries are independent
coordinate arrays. v1 doesn't need it (proximity graph uses centroids only), but the
**deferred road-network Connections feature does**. Therefore: if/when roads land,
the Normalizer must *deliberately* preserve connectivity (retain shared node IDs in
properties or emit an adjacency side-channel). It will not survive naive conversion.

### Feature property schema
The normalized set of properties carried on every GeoJSON Feature across the contract
boundary. Typed by **kind** (`poi`/`road`/`barrier`/`water`/`park`/`place`) plus `name`,
`category`, `importance`, `tier`, `street`. This schema — not raw OSM — is what the
Generator reasons over. Specified normatively in
[`docs/feature-schema.md`](docs/feature-schema.md) (ADR-0006).

### Feature kind
The `kind` discriminator on every contract Feature telling the Generator how to use it:
`poi` (a place, Point), `road` (LineString, for street-snap), `barrier` (LineString, for
crossing flags), `water`/`park` (Polygon, for Terrain), `place` (district anchor, Point).
The contract is a **mixed-geometry** FeatureCollection, not point-only.

### Inclusion gate
The Stage 1 rule for what becomes a Feature at all (distinct from promote-vs-cluster).
RP-noise — parking, benches, bicycle parking, waste baskets, trees — is **dropped
entirely**, not clustered. Unnamed buildings survive only as a per-District clustered
count. See `docs/feature-schema.md` §3.
