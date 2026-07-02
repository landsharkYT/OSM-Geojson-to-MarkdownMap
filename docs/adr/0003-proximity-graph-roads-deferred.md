# 3. Connections from a geometric proximity graph; road-network routing deferred

Date: 2026-06-26
Status: Accepted

## Context

A schematic can't connect every Feature to every other (O(n²) spaghetti). Something must
decide which Features get a drawn Connection. The most navigationally truthful option is
to connect Features along the real OSM street/path network ("down Main Street"). But that
requires OSM topology, the shared-node connectivity between ways, which plain GeoJSON
doesn't preserve (see ADR-0001), plus feature-to-way snapping and graph routing. That's a
large amount of machinery for a first prototype.

## Decision

For v1, derive Connections from a geometric proximity graph: connect each Feature to its
few nearest neighbors, then prune to a planar, sparse graph (Gabriel / relative-neighborhood
style) so the schematic stays clean. This needs only feature coordinates, which survive the
GeoJSON contract perfectly.

> **Amendment (Generator v0):** the proximity graph ships first as nearest-k (k≈3) without
> Gabriel/RNG pruning. Planar pruning removes crossing edges, which only matters when a 2D
> diagram is drawn; the structured-text Connections list (ADR-0004) has no visual crossings,
> so planarity buys nothing there. Nearest-k already gives sparsity. Gabriel/RNG pruning is
> deferred to the ASCII-view milestone, which does need planarity.

Road-network Connections (path routing) are deferred, not rejected.

"Roads" splits into two features of very different cost, though, and the cheap half ships
in v1:

- In v1, cheap (tag reads plus one geometry test):
  - Street attribution: each Feature labels the street it sits on (`addr:street` tag, or
    nearest-named-road snap). No topology needed.
  - Barrier-crossing flags: a Connection whose straight-line segment intersects a known
    barrier way (e.g. a `motorway` geometry) is flagged `[crosses Route 9]`. A
    segment-vs-polyline intersection test, no routing graph.
  - Terrain context: named water, parks, and barriers listed as orienting context.
- Still deferred, since it needs topology: actual path routing and turn-by-turn navigation
  ("down X, left on Y").

## Consequences

- v1 is buildable now with no topology handling; it works on any GeoJSON.
- Connections reflect "what's physically nearby", not "what's reachable by road". Two
  features on opposite sides of an impassable barrier may appear connected. Barrier-crossing
  flags mitigate this by catching the case that actually misleads narration (e.g. a "short
  walk" straight across a freeway).
- The deferred roads feature has a hard prerequisite: the Normalizer must deliberately
  preserve connectivity (retain shared node IDs in properties, or emit an adjacency
  side-channel), because naive OSM→GeoJSON destroys it. Captured in CONTEXT.md under
  "Topology".
