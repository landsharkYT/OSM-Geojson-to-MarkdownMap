# 11. MarkdownMap settings are a render pass over an option-independent MapModel

Date: 2026-06-30
Status: Accepted

Extends ADR-0007 (markdown is a view over MapModel) and ADR-0010 (the pipeline runs in a
Web Worker). Implements the long-deferred settings surface (`docs/settings.md`).

## Context

A settings button on the MarkdownMap panel exposes generation toggles. The v1 knobs are the
three "how does the map talk to the LLM" options:

- `connections.bidirectional`: print each link under both endpoints, or once.
- `connections.inlineNeighborName`: print the neighbour's name on each connection line.
- `render.directivePreamble`: emit the LLM framing block.

(`bidirectional` was specified and defaulted on, but never wired. The off-behaviour is
built here, deduping each undirected pair under its lower, more-important token, consistent
with the Explorer SVG's existing pair de-dup.)

The naive wiring, threading options into the build and re-running the pipeline on every
toggle, would re-parse a 10+ MB `.osm` for a cosmetic change. But these three knobs change
only rendering: the `MapModel` (features, edges, districts, terrain) is identical regardless
of their values. Only `MapModel.markdown` differs.

The model-affecting knobs (`neighborsPerFeature`, `buckets`, `spineKeyCap`) genuinely change
the graph and are deferred, since they'd need a rebuild and a different UX.

## Decision

Settings are applied as a render pass over the cached, option-independent MapModel, not a
rebuild.

- A second WASM entrypoint, `Render(mapModelJson, optionsJson) → markdown`, re-renders from
  an existing model. The heavy `Build` entrypoints also take `optionsJson` (used only for
  the initial embedded render), so the first paint already reflects persisted settings.
- The worker caches the last model JSON; a settings change sends a `rerender` message
  carrying only the options. No re-parse, no progress bar: instant, even for huge imports.
- The three knobs are render-only by construction. Reclassifying any of them as
  model-affecting would break this fast path, which is the boundary this ADR draws.
- Settings are live (re-render on change), persisted to `localStorage`, and distinct from
  the Explorer's layer toggles, which control only the SVG view.

## Consequences

- Toggling settings is instant regardless of input size; the parse/model work is never
  repeated for a cosmetic change.
- Two WASM entrypoints (`Build*`, `Render`) and a model that both embeds markdown and gets
  re-rendered is surprising without this context, hence the ADR.
- Single source of truth preserved: rendering stays in C# (`MapGenerator.Render`); the web
  side never reimplements markdown formatting.
- Adding model-affecting settings later is a separate, larger change (rebuild, progress, and
  a cached FeatureCollection or re-parse), deliberately out of scope here.
