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

### Name resolution
How a Feature gets its display name. The Normalizer (Stage 1, the only stage with raw OSM
tags) resolves `name → brand → operator`, preferring `short_name` over a long `name`; a name
derived from `brand`/`operator` counts as **named** for promotion and importance. A Feature
the Normalizer still can't name is **unnamed**. Real name parsing can only live in the
Normalizer — the Explorer/Generator never sees the raw tags. See ADR-0012.

### Unnamed fallback
The display label for an **unnamed** Feature: its humanized **category** subclass, lowercase
(`place of worship`, not `unnamed`) — casing signals "type, not a proper name" to the LLM, and
the redundant `(category)` is dropped on those lines. Render-only (needs only `category`), so
it re-renders live and stays consistent with the SVG token labels. Distinct from
[[name-resolution]], which runs in the Normalizer. See ADR-0012.

### Co-location merge
A Normalizer pass that collapses **duplicate/fragment** Features at the same place: an
**unnamed** Feature within a small radius of a **named** one of compatible category folds into
it; two Features with the **same name** within that radius collapse to one (prefer the
point / higher importance). Targets the common OSM pattern where one place is mapped as both a
node *and* a building way (and stray unnamed fragments around it). Conservative radius +
same-category guard so distinct neighbours are never merged. See ADR-0012.

### Unnamed promotion (tiered)
Unnamed Features promote to their own Token only at **landmark tier**; unnamed
destination/lower Features are demoted to a District's clustered count. A fixed default (not a
toggle — it is model-affecting, unlike the render-only [[markdownmap-settings]]). Keeps the
notable unnamed church while folding unnamed noise away. See ADR-0012.

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

### Explorer (React helper)
The browser-based helper app. Runs the pipeline **client-side** (.NET WASM, ADR-0009 — the
input never leaves the browser) and **visualizes the Generator's structure** — tokens,
proximity edges, districts, terrain, crossing flags — at true geographic positions, beside
the MarkdownMap it produces. A human-facing *true-geographic* view, complementing the
LLM-facing *topological* MarkdownMap. The pipeline runs in a **persistent Web Worker** so the
UI stays responsive and can show live progress during large imports (ADR-0010).

### MarkdownMap settings
The generation knobs that change the produced **MarkdownMap** (the copied artifact), exposed
via a settings button on the MarkdownMap panel. v1 surfaces the three **render-only** knobs —
`bidirectional` (each link under both features or once), `inlineNeighborName`, and
`directivePreamble`. These change only the rendered markdown, never the **MapModel**, so a
change re-renders the cached model instantly with no re-parse (ADR-0011). Live + persisted.
**Distinct from [[layer toggles]]**, which only affect the SVG view. Model-affecting knobs
(`neighborsPerFeature`, `buckets`) are deferred. Defaults are the documented ones
(`docs/settings.md`); `bidirectional` stays **on** by default — the toggle exists for terser
output when the consuming LLM is smart enough to infer the reverse direction.

### Layer toggles
Checkboxes (Terrain / Edges / Minor features / Tokens) that show or hide **SVG** map layers in
the human-facing view. A display concern only — they do **not** affect the generated
MarkdownMap. Live behind the [[map-view-settings]] button. Distinct from [[markdownmap-settings]].

### Map view settings
The Explorer's **display-only** settings (ADR-0013), behind a ⚙ button in the header — separate
from [[markdownmap-settings]] (which change the generated text and run in WASM). Map view
settings never touch the pipeline: [[layer-toggles]] plus **Detailed terrain** (default on),
which switches terrain *geometry mode* — on = real shapes (rings + shoreline lines, ADR-0014);
off = the old convex-hull **extent** blobs (ADR-0008's look), recomputed **client-side** from
the terrain points the Explorer already holds. Persisted to localStorage; pure client-side state.

### Explorer legend
The ⓘ popover in the header (ADR-0013) explaining the SVG symbology — crucially that the faint
gray lines are **proximity links** ([[connection]]s), *not streets*, and that terrain areas are
**approximate** extent, not precise shorelines. Addresses the two standing confusions of the
schematic view.

### Progress signal
A structured event the pipeline emits at real seams (parse / build / serialize), carrying a
**Phase** and optional **count** — never display text. The Explorer maps it to the rotating
status line and the progress bar; the parse phase reports real stream byte-position so the
bar is *determinate* through the segment where the time actually goes. UI-agnostic by design
(the CLI ignores it), keeping the pipeline a reusable library. See ADR-0010.

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
barrier way (motorway, rail, or river/canal). Warns that the proximity edge is not actually
walkable directly. Computed by segment-vs-polyline intersection. Distinct from a
[[separation-flag]]: roads and rail are *passable* (you cross them), so a crossing flag is
informational and never on its own makes a Feature [[stands-apart]].

### Separation flag
A `[separated by water]` tag on a Connection whose straight line passes through a **water
area** (ADR-0014 polygon) or a `barrier:water` (river/canal). The honest accessibility signal:
crow-flies distance is real but the link is *not directly walkable*. Phrased "separated by",
**never "unreachable"** — a bridge or ferry may exist and we don't claim to know. We annotate
separation; we never compute a route (ADR-0015). Distinct from [[barrier-crossing-flag]] (road/
rail), which does not imply separation.

### Stands apart
A per-Feature flag — `stands apart — reached only across water` — set when **every** one of a
Feature's proximity links is water-[[separation-flag|separated]]. Only water counts (the
taxonomy has no walls/fences; roads and rail are passable), so the flag stays quiet on ordinary
features ringed by streets and fires on the genuinely cut-off ones (island, far shore). It keys
on *the absence of any clean open-land link*, not on an absolute distance, so it is
scale-invariant across dense and sparse extracts. Derived at render time from the edges; honest
under incomplete OSM (worst case it is absent, never a false "unreachable").

### Spine
A one-line ordering of a District's promoted Features along its main axis (usually a
Street), e.g. "N–S along Main Street: [02],[01],[03],...". Gives the LLM a global
frame so it need not triangulate per-Feature bearings.

### Terrain
The orienting context block: named water bodies, parks, and Barriers with their rough
position, so the LLM knows "the shape of the place" (what's water, what blocks movement).
The **markdown** projection is deliberately **coarse — name · octant · note** — and stays that
way: real geometry (ADR-0014 ring assembly) flows into the contract + Explorer SVG, never into
the markdown text, where coordinate detail would only bloat tokens and degrade LLM reasoning.

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
