# 15. Separation and isolation annotations (annotate, never route)

Date: 2026-06-30
Status: Accepted

## Context

The proximity graph (ADR-0003) is purely geometric: each Connection carries crow-flies
meters, an 8-wind bearing, and a distance bucket. The only accessibility hint is the
existing `[crosses <barrier>]` flag (see "Barrier / crossing flag" in CONTEXT.md), a
straight-segment-vs-barrier intersection.

This makes the markdown silently overstate reachability. A token 180 m across open water
reads identically to one 180 m down the same street; the LLM can't tell that the first
isn't walkable directly. Two distinct real-world causes produce the same "looks adjacent"
output:

1. Incomplete OSM: the connecting path was simply never imported. The place is reachable;
   our data just doesn't show how.
2. Genuine separation: island, far shore, fenced compound. It really is hard to reach.

The trap is that we usually can't distinguish (1) from (2), since "no path in the data"
isn't the same as "no path exists." So any markdown that asserts "unreachable" will
sometimes be confidently wrong, and an LLM will repeat that to a player as fact.

We now have a signal that didn't exist when ADR-0003 was written: real water geometry
(ADR-0014 water-area polygons) plus linear `barrier:water` (river/canal). Crossing water is
the single strongest "you can't just walk this" cue.

## Decision

Annotate separation, never route. Distance stays honest crow-flies. We enrich the
accessibility annotation and never claim a route exists or doesn't.

1. Distance is unchanged: crow-flies meters, 8-wind, bucket. Cheap, honest, robust to
   incomplete OSM. We don't build a routable network or report path distance, since OSM
   extracts routinely lack the path data, and a computed "route" over a missing network
   would mislead more confidently than crow-flies does.

2. Per-edge separation flag. Keep `[crosses <road/rail name>]` for road and rail. Add
   `[separated by water]` when a Connection's straight line passes through a water area
   (ADR-0014 polygon) or a `barrier:water` (river/canal). Phrased "separated by," never
   "unreachable": a bridge or ferry may exist, and we don't claim to know.

3. Per-feature "stands apart" flag. Mark a Feature `stands apart — reached only across
   water` when every one of its proximity links is water-separated.

   Only water counts toward isolation. Of the three barrier classes (`motorway`, `rail`,
   `water`, see [TerrainClassifier.cs](../../src/MarkdownMap.Normalizer/TerrainClassifier.cs)),
   there are no walls, cliffs, or fences in the taxonomy, and roads and rail are passable
   (you cross roads; level crossings exist). Counting road/rail crossings would fire the flag
   on ordinary downtown features ringed by arterials, which is noise, the opposite of honest.
   So road and rail crossings show as per-edge tags only and never trigger isolation.

   This rule is scale-invariant: it keys on whether there's a clean, open-land link, not on
   an absolute distance threshold, so it behaves the same on a dense city extract and a
   sparse rural one. A node 1 km down the same street isn't flagged; a node ringed by water
   is.

Markdown guardrail, inherited from ADR-0014: these are short qualitative tags, not geometry.
No coordinates, shorelines, or routes enter the markdown text.

## Consequences

- The LLM can tell "180 m, walk there" from "180 m, but across water," without us ever
  fabricating a route or a false "unreachable."
- Contract: Connections need a water-crossing signal distinct from the barrier name that
  `Crosses` already carries, and the crossing test needs the barrier class to separate water
  from road/rail. This is one small additive field, no pipeline-philosophy change.
- Isolation is derived at render time from the edges' water-separation flags; there's no new
  per-feature stored state beyond what the edges already imply.
- Honest under incomplete OSM: worst case the flag is absent (we under-warn), never a false
  "unreachable." We never assert reachability we can't back.
- Rail is treated as passable. Revisit only if a concrete case shows rail genuinely cutting a
  feature off and it matters for play.
- Routing stays explicitly out of scope. If a future feature needs true travel distance it
  must first solve connectivity preservation (see CONTEXT under "Topology").
