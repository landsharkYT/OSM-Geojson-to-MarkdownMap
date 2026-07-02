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
scale. The rendered line carries **rounded metres + bearing**; the coarse size bucket
(adjacent / near / short walk) is **derivable from the metres** and dropped to stay terse — with
one exception: a link at/over the **far** cutoff (≥500 m) is flagged **`(far)`**, because the
proximity graph guarantees connectivity (kNN) and so a sparse area can produce a long edge that
would otherwise read like a walkable local hop. The full bucket survives in the
[[explorer-react-helper|Explorer]] detail view.

### Importance Tier
A numeric rank (0–100) assigned to each Feature. It **orders** Features but no longer *alone*
decides promotion: promote-vs-cluster is governed by [[narrative-salience]] (a guaranteed **core**
promotes; the rest compete) plus the per-District [[promotion-budget]]. v1 default:
landmarks/amenities/shops/civic rank above named buildings, above minor/unnamed structures. A
**tunable default, never hardcoded**.

### Narrative salience
The parse-time classification of how **scene-worthy** a Feature is — distinct from raw
[[importance-tier|importance]]. Set in the [[normalizer-stage-1|Normalizer]] from OSM tags, it sorts
Features into: a **core** that always promotes (worship, civic **institutions** — school, library,
hospital, post office, and singular institution *buildings* like a hospital/school/stadium/**station** —
historic sites, museums/galleries/attractions, major venues, and the **residential addresses** of a
neighbourhood: **named piers/moorages** in a waterfront community and **dorms** on a campus — the
skeleton the party lives/moves among). Civic core is an **allowlist** of those public institutions —
everything else civic (a private practice, a `healthcare` clinic, a social facility, a dept office, a
campus hall) **defaults to budgeted and competes**, so an un-enumerated civic tag can never silently
promote at institution rank. **Budgeted** competitors vie for the [[promotion-budget]];
and **clustered** (residential/minor). A **campus hall** (`building=university`/`college`) is *not*
core — a campus has dozens, so a hall is a **budgeted venue-band destination** that competes like a
café, with a small **footprint-area** nudge so the big halls out-sort the annexes (ADR-0019).
Within *budgeted*, **interactive venues** (food, shops, private services like a dentist or salon, and
campus halls) outrank **commemorative/decorative** landmarks (artwork, viewpoint, memorial, monument,
walk-past man_made structures) and **civic offices** (a dept/gov office, often redundant with its core
institution building) — so a district's cafés, shops and halls win seats before its sculptures and offices,
since a DM's party enters/goes to places and only walks past art. Fixes the dense-extract failures where a private dental
office ranked like a school and campus sculptures crowded out venues. **Model-affecting** (changes
which Features get a [[token]]), so **not** a render-only [[markdownmap-settings|setting]]. See ADR-0018.

### Chain flag
A Normalizer flag: a Feature carrying a `brand` tag is a **chain** (a franchise coffee shop or
retail chain). It
**lowers** the Feature's [[importance-tier|importance]] so **independent** places win the
[[promotion-budget]] first — a chain still promotes where there's headroom (a lone café in a sparse
block) and fades out exactly where density is high. Corrects the prior rule where a resolved brand
name *raised* rank. See ADR-0018.

### Promotion budget
A per-[[district-cluster|District]] cap (Stage 2) on how many **budgeted** Features earn their own
[[token]] beyond the guaranteed [[narrative-salience|salience core]]: the top-K by
[[importance-tier|importance]] promote, the rest fold into the District's clustered count.
Self-adapting — a dense retail District clusters most of its long tail while a sparse one promotes
nearly all — so a hyper-dense extract and a modest one come out comparably legible without per-city
tuning. K a tunable default. Model-affecting. See ADR-0018.

### Name resolution
How a Feature gets its display name. The Normalizer (Stage 1, the only stage with raw OSM
tags) resolves `name → brand → operator`, preferring `short_name` over a long `name`; a name
derived from `brand`/`operator` counts as **named** for promotion and importance. A resolved value
that is a **bare label** — a single character or all digits (a dorm wing `A`, a building number
`12`) — is **not a real name**: it is rejected, so the Feature is **unnamed** and clusters (it never
earns a [[token]] unless worship). A Feature the Normalizer still can't name is **unnamed**. Real
name parsing can only live in the Normalizer — the Explorer/Generator never sees the raw tags. See ADR-0012.

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

### Unnamed promotion (worship-only)
An unnamed Feature earns its own [[token]] **only if it is worship** (the canonical "the church"
case) — every other unnamed Feature folds into a District's clustered count, whatever its
[[narrative-salience]]. Rationale: an unnamed thing is a weak scene anchor (no name to reference),
and a worship site is the one category singular and evocative enough to narrate unnamed ("the
church"); by contrast a campus has *many* unnamed sculptures, pool basins, and out-buildings, which
as category-label tokens (`swimming pool`, `maritime`) are pure noise. A fixed default (not a toggle
— model-affecting, unlike the render-only [[markdownmap-settings]]). Tightens ADR-0012's original
tiered rule to its intent. See ADR-0012 and ADR-0018.

### Minor feature
A clustered Feature that carries a **real name** (`name → brand → operator`, not the humanized
category) but never earned a [[token]] — a named apartment block, a named grave, a café that lost the
[[promotion-budget]]. Set-dressing a DM can still name. Folded into the count by default; an **opt-in**
[[markdownmap-settings|setting]] ("Minor features", render-only, default off) instead lists them per
[[district-cluster|District]] as a compact names line, **deduped by name** with an `×N` count, and the
count relabels to the remaining [[prop-feature|props]]. With the setting off the markdown is
byte-identical (the split lives in the model, unrendered). Distinct from a [[prop-feature]] (nameless)
and from a [[token]] (promoted). Deliberately inoptimal — useful on a sparse extract, noisy on a dense
one — hence a toggle. See ADR-0020.

### Prop feature
A clustered Feature whose "name" is **just its category** (an unnamed pier → `pier`, a nameless
`house`) — anonymous background scenery. **Never** listed in the markdown; only ever part of the
count. The residual tier once the named [[minor-feature|minor features]] are separated out. In the
[[explorer-react-helper|Explorer]] it is its own dot layer, distinct from minor features. See ADR-0020.

### District (Cluster)
A named group of lower-tier Features shown as a single block instead of individual
Tokens, e.g. "Harborside (+38 minor buildings)". Keeps a whole-area
MarkdownMap legible without scene-chunking. **Derived from real OSM
`place=neighbourhood`/`quarter` nodes** (e.g. Old Town, Harborside, Mill District,
Riverside ...) as district anchors: each Feature joins its nearest anchor
(Voronoi). Geometric-cluster fallback only where no place coverage exists.

### Scope (v1)
By default a single MarkdownMap renders the **entire ingested OSM extract**. [[chunking]]
(ADR-0016) is the opt-in alternative: it **reinterprets** the document into self-contained
scene-chunks, each carrying an intrinsic **[[anchor]]** and **[[off-map-edge|off-map edges]]**,
with a Directive Preamble that reframes the map as "immediate surroundings" rather than "the
world". See ADR-0005 (the reserved render-mode seam) and ADR-0016.

### Directive Preamble
An optional, clearly-delimited block that gives the consuming LLM **behavioral** instruction:
treat the map as authoritative / canon, don't invent geography not listed, and (in a
[[chunking|chunk]]) reach other areas only via [[off-map-edge|off-map edges]]. Toggleable
([[markdownmap-settings]], default on) and **mode-aware**, compressed to a dense line or two.
**Distinct from the [[reading-key]]** — the toggle removes the *instruction*, never the parse
legend, so a chunk stays self-contained with the preamble off. See ADR-0005.

### Reading key
The compact, **always-present** legend at the top of every MarkdownMap and every
[[chunking|chunk]] explaining how to *parse* a line — the `[token] Name (category)` header, the
`→ ~<m>m <DIR>` [[connection]] form (N = up, 8-wind), the [[barrier-crossing-flag|crosses]] /
[[separation-flag|separated by water]] / [[stands-apart]] flags, and "not to scale". Kept to a
couple of dense lines and **not a toggle**: it is what makes a chunk **self-contained** (a
retriever may load one chunk with no [[manifest]]), so it stays even when the
[[directive-preamble]] is off. Distinct from the Directive Preamble (behavioral instruction, not a
legend).

### Transit (deferred)
Public-transport features. OSM carries them richly (named bus stops by cross-street,
bus/trolleybus/light-rail **route relations** with operators + route numbers). Splits
like roads (see ADR-0003): **stops** are cheap Features that can land early; **routes**
are ordered **relations** needing the same topology preservation the Normalizer must do
deliberately. Routes are the *long-range, cross-District* connective tissue the proximity
graph cannot express, and make travel time **mode-dependent** (walk vs bus vs rail).

### Anchor
The focal Feature a [[chunking|chunk]] is oriented on — its **key feature** (the most important
Feature in the chunk). Fixed per chunk, giving a static chunk set; the party's *exact* spot is the
consumer's to narrate. Intrinsic to chunked maps; absent (or user-stated) in whole-area maps. Also
the **fallback label** for a [[chunking|sub-area]] whose octant name would collide. See ADR-0016.

### Off-map edge
A concrete exit on a [[chunking|chunk]]'s boundary pointing toward the neighbour that continues beyond it:
neighbour name + bearing + the specific boundary Feature just across + distance (e.g.
"→ NE toward Harborside (off-map): via [42] Dock Gate, ~120m"), so the LLM never treats the chunk
edge as the edge of the world. Derived by aggregating boundary-crossing [[connection|connections]]
per neighbour chunk. See ADR-0016.

### Chunking
An opt-in [[markdownmap-settings|generation setting]] (ADR-0016) that renders the map as a set of
self-contained **scene-chunks** instead of one document, for a storytelling LLM that loads **one
chunk at a time** (retrieval) — sharp local focus, cheap per turn. A **chunk** = a
[[district-cluster|District]], or one **sub-area** of a District too big for the [[scene-size]]
target. A District is subdivided into contiguous, **non-overlapping** sub-areas by **density-gap
bisection** — recursively cutting at the widest gap along the longer (cardinal) axis until each
piece fits, so cuts fall in sparse seams (real exits, no sliced blocks) and a stray feature left
without an in-piece neighbour is merged back to its nearest one. Sub-areas are named
`District · <octant>` (with the [[anchor]] key-feature as fallback on collision). Each chunk is
oriented on its [[anchor]], carrying a compact [[reading-key]], local terrain, its features +
connections, and concrete [[off-map-edge|off-map exits]]. Tokens are **global and stable** across
all chunks so references stay coherent as the party moves. Render-only over the MapModel (no
re-parse, ADR-0011), but the output shape becomes a chunk set + [[manifest]]. Distinct from
whole-area mode (the default). See ADR-0016 and ADR-0017 (subdivision).

### Manifest
The index emitted alongside a [[chunking|chunked]] map: one row per chunk — name · file · bbox ·
[[anchor]] · neighbours — so a retrieval system can pick *which chunk the party is in* without
reading them all. Shipped as `manifest.md` in the download zip. See ADR-0016.

### Scene size
The [[chunking]] control: **Tight / Standard / Wide** (≈ 8 / 14 / 22 promoted Features per
chunk, Standard default). Sets the size a District may reach before it **subdivides** into
sub-areas — i.e. how much world a single scene shows. Measured in promoted Features (clustered
minors are cheap). See ADR-0016.

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
via a settings button on the MarkdownMap panel. v1 surfaces the **render-only** knobs —
`bidirectional` (each link under both features or once), `inlineNeighborName`,
`directivePreamble` (the behavioral [[directive-preamble]] only — the [[reading-key]] is always
emitted, never a knob), `minorFeatures` (list named [[minor-feature|minor features]] per district,
ADR-0020), plus [[chunking]] + [[scene-size]]. These change only the rendered
markdown, never the **MapModel**, so a change re-renders the cached model instantly with no
re-parse (ADR-0011). Live + persisted. **Distinct from [[layer toggles]]**, which only affect the
SVG view. Model-affecting knobs (`neighborsPerFeature`, `buckets`) are deferred. Both terser
knobs now default **off**: `bidirectional` (each link once; the reverse is inferable) and
`inlineNeighborName` (a connection references its neighbour by **[[token]]** only — the name is one
lookup away in the feature header, and the [[reading-key]] documents that nameless form), together
the two largest redundancies in the connections block.

### Rendered preview
A **display-only** sidebar toggle (default **off**) that shows the MarkdownMap as formatted
markdown instead of raw source. Deliberately *not* a [[markdownmap-settings|generation knob]]:
it never changes the text — Copy / Download still emit the raw markdown — and must not leak into
the WASM options DTO, so it is persisted separately. Rendering is sanitized (marked + DOMPurify):
OSM names are user data, so a place literally named `<img onerror=…>` must never execute.

### Layer toggles
Checkboxes (Terrain / Edges / Minor features / Prop features / Tokens) that show or hide **SVG** map
layers in the human-facing view. A display concern only — they do **not** affect the generated
MarkdownMap. Live behind the [[map-view-settings]] button. Distinct from [[markdownmap-settings]].
**Honest-by-default:** the map shows only what the AI sees in the markdown. [[minor-feature|Minor
features]] and [[prop-feature|prop features]] are **separate layers, both default off**, and the
Terrain layer draws only the **orienting-scale** areas the markdown lists (sub-threshold pocket parks
filtered out client-side, mirroring the Generator). Because a prop is *never* in the markdown and a
named minor is in it only when its [[markdownmap-settings|setting]] is on, drawing either can make the
map disagree with the text — an **adaptive banner** flags this both ways: *surplus* (the map draws what
the AI can't see) and *deficit* (the markdown lists minor features the map hides); dismissable,
re-arming when the mismatch changes. The clustered positions and full terrain geometry still reach the
browser — the toggle only governs what is *drawn*. Each dot is **hover-inspectable** (a small tooltip
names it and its category), a look-only affordance separate from the token/sidebar selection. See ADR-0020.

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
On the rendered map a Feature shows its street **only when it differs from its area's dominant
street** — the dominant one is named once (the [[district-cluster|District]] header + [[spine]] in
whole-area, a compact note in a [[chunking|chunk]]) and the redundant per-Feature repeat is
dropped. Off-street features (a different road) still carry their own label.

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
The orienting context block: named water bodies (lakes, bays), parks and other **coarse
geographic areas** (beaches, wetlands, cemeteries), and Barriers with their rough position, so the LLM knows
"the shape of the place" (what's water, what's shore/green, what blocks movement). Such areas are
**terrain, not POI tokens** — a beach is a place you walk to, like a park, not a point.
The **markdown** projection is deliberately **coarse — name · octant** — and stays that
way: real geometry (ADR-0014 ring assembly) flows into the contract + Explorer SVG, never into
the markdown text, where coordinate detail would only bloat tokens and degrade LLM reasoning. For
the same reason the markdown lists only **orienting-scale** terrain: parks/water below an **area
threshold** are omitted from the *text* (their geometry still reaches the contract + Explorer), so
a park-dense extract doesn't drown the block in pocket parks; and the redundant per-line note
(`green space` / `open water`) is dropped — the kind label already says it — while the barrier note
(`impassable except at crossings`) stays, since it carries real information.

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
count. Passing the gate makes a Feature; whether it earns a [[token]] is then a separate call by
[[narrative-salience]] + the [[promotion-budget]]. See `docs/feature-schema.md` §3.
