# 13. Explorer readability: fixed-size symbols, decluttered labels, and a client-only map-view settings surface

Date: 2026-06-30
Status: Accepted

Extends ADR-0009 (the Explorer) and the settings story (ADR-0011). Keeps ADR-0002
(not-to-scale) and ADR-0008 (extent-only terrain) intact.

## Context

The SVG view became unreadable on a real extract: everything lived in one scaled `<g>`,
so zooming magnified the dots and labels instead of separating them; ~130 labels overlapped;
the gray proximity graph read like a street network; barriers and "crosses-barrier" edges were
both red dashes; and the convex-hull terrain (ADR-0008) looked like an inaccurate shoreline.

## Decision

**Rendering model — fixed-size symbols over semantic-zoom geometry.**
- *Geometry layer* (terrain polygons, proximity edges) stays in the scaled `<g>` — real
  areas/lines that should zoom like a map.
- *Symbol layer* (token dots, minor dots, labels) renders at **constant screen size**,
  positioned by applying the zoom transform to coordinates. Zooming **spreads** a cluster
  while keeping markers small, so it declutters instead of magnifying.

**Label decluttering — importance-prioritized collision avoidance.** Place labels
most-important-first, skipping any whose screen box overlaps one already placed; recompute on
zoom (so more labels appear as features separate). The selected/hovered feature is always
labeled. The authoritative full key remains the MarkdownMap text + the sidebar.

**Symbology fixes.** Proximity edges are faint by default and brighten for the selected
feature's links; a "crosses-barrier" edge is shown distinctly from a **barrier** (no longer
both red dashes). An **ⓘ legend** popover in the header names every symbol and states the two
non-obvious truths: proximity links are **not streets**, terrain is **approximate** extent.

**Detailed terrain, toggleable.** A **"Detailed terrain"** toggle (default on) switches terrain
*geometry mode*, not just styling (amended after ADR-0014 gave terrain real shapes):
- **On** → the real shapes: assembled rings as filled polygons, bbox-clipped water as
  shoreline lines.
- **Off** → the old **convex-hull extent** blobs (ADR-0008's look), drawn soft/faint with a
  dashed outline to read as approximate. The hull is **recomputed client-side** (monotone-chain
  over the terrain points the Explorer already holds) — the pipeline no longer emits hulls
  (ADR-0014), so this is purely a view, honoring the client-only rule below. Off-mode hulls all
  area terrain; barriers stay lines.

**Two distinct settings surfaces.** *Map view settings* (this ADR) are **display-only and
purely client-side** — the layer toggles plus Detailed-terrain, behind a ⚙ in the header,
persisted to localStorage. They never touch the pipeline, which is what separates them from
*MarkdownMap settings* (ADR-0011, run in WASM, change the generated artifact). Keeping them as
two surfaces prevents conflating "how the map looks" with "what the map says."

## Consequences

- Zoom now declutters; dense districts become legible by zooming in.
- The symbology is self-explaining via the legend; the road/barrier confusions are resolved.
- A second settings popover exists — justified by the hard display-vs-generation split; the
  two are visually and conceptually separated.
- Terrain precision is *not* pursued (ADR-0008 stands); we present it honestly instead. If
  precise terrain is ever needed, that is a separate decision reversing 0008.
- All of this is Explorer-only; the pipeline, contract, and golden output are untouched.
