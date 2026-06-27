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
[NN] <name> (<category>) · on <street> · <district>
```
`^\[(\d+)\] (.+?) \((.+?)\) · (?:on|near) (.+?) · (.+)$`
(`near` instead of `on` signals a snapped/fallback street.)

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
### <name> — <blurb>
spine: <text, leading with N→S/E→W token order>
across: <token-list> <note>        (optional)
promoted: <token-range-or-list>
clustered: ~<n> <note>
```
Key lines match `^(spine|across|promoted|clustered): (.+)$`. Token lists are
comma-separated `[NN]`; ranges `[NN]-[MM]` expand inclusive.

## Worked example — how a consuming LLM uses it

Given the party at `[05]`:

> "The party leaves **The Anchor Tavern** `[05]` on Main Street. **Corner Cafe** `[06]` is
> right next door, 10 m north; **Rivertown Market** `[08]` is 50 m southeast if they need
> supplies. Someone suggests **Bluebird Coffee** — but that's ~430 m east *across Route 9*,
> so it's a real detour to a crossing, not a quick hop. Mill Lake sits at their backs to
> the west."

The model gets local adjacency from `[05]`'s block, the global frame from the Old Town
spine, the barrier reality from the `[crosses Route 9]` flag, and orientation from Terrain.
