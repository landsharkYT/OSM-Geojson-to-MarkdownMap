# MarkdownMap output format (v1.1)

A hand-assembled sample of real emitted output (kept locally, not committed) is used to
approve the shape. This doc is the spec behind it: every section's grammar, so the
document is machine-parseable end to end, plus a worked example of how a consuming LLM
reads it. The example below uses a fictional area ("Rivertown"); real exports stay local.

The document is sections in fixed order: **Directive Preamble → How to read → Bounds →
Terrain → Districts → Connections**. Free-text prose only appears inside the Directive
Preamble and the "How to read" key (both addressed to the LLM); every other section is a
line grammar.

## Line grammars

All fields are whitespace-stripped. `·` separates header fields, `—` precedes metrics,
`,` separates metrics, `[...]` carries flags, `:` separates district keys from values.

### Feature header
```
[NN] <name> (<category>)[ · on <street>][ · <district>]
```
`^\[(\d+)\] (.+?) \((.+?)\)(?: · (?:on|near) (.+?))?(?: · (.+))?$`
The `· on <street>` and `· <district>` segments are **optional** — present only when the
source GeoJSON carries that data. (`near` instead of `on` signals a snapped/fallback
street.) Generator v0 (POI-only input) emits just `[NN] <name> (<category>)`.

### Connection
```
→ [NN] <name> — ~<metres>m <DIR>, <size>[ [<flag>]]
```
`^→ \[(\d+)\] (.+?) — ~(\d+)m ([A-Z]{1,3}), ([a-z ]+?)( \[[^\]]+\])?$`

### Terrain entry
```
- <name> (<kind>) · <position> · <note>
```
`^- (.+?) \((.+?)\) · (.+?) · (.+)$`
`kind` ∈ `water` | `park` | `barrier:<osm-class>` (e.g. `barrier:motorway`).

### District block
```
### <name>[ — <blurb>]
spine: <DIR>[ along <street>]: <key tokens, axis-ordered>
promoted: <count>
clustered: ~<n> minor          (optional; omitted when 0)
```
Key lines match `^(spine|promoted|clustered): (.+)$`. `<DIR>` is one of N–S / NE–SW /
E–W / NW–SE. The **spine is a skeleton** — only the few highest-importance ("key")
features, listed in axis order — not full membership. `promoted` is a **count**; exact
membership is carried per feature in the Connection header's `· <district>` segment.
(`across:` for cross-district links is deferred until barriers exist.)

## Worked example — how a consuming LLM uses it

Given the party at `[05]`:

> "The party leaves **The Anchor Tavern** `[05]` on Main Street. **Corner Cafe** `[06]` is
> right next door, 10 m north; **Rivertown Market** `[08]` is 50 m southeast if they need
> supplies. Someone suggests **Bluebird Coffee** — but that's ~430 m east *across Route 9*,
> so it's a real detour to a crossing, not a quick hop. Mill Lake sits at their backs to
> the west."

The model gets local adjacency from `[05]`'s block, the global frame from the Old Town
spine, the barrier reality from the `[crosses Route 9]` flag, and orientation from Terrain.
