# 2. Topological schematic with distance + bearing annotations (not a to-scale grid)

Date: 2026-06-26
Status: Accepted

## Context

The goal is to make an LLM spatially aware of an area for roleplay / D&D. The obvious
approach is a true-to-scale ASCII grid where 1 character = N meters. But real OSM data
is dense and unevenly distributed: a to-scale grid is mostly empty cells, collision-
prone where features cluster, token-explosive, and hard for an LLM to read.

The alternative, a purely topological sketch (nodes + connections, like a hand-authored
schematic), reads fine and doesn't break under messy data, but it throws away distance,
which the LLM needs to pace a scene.

## Decision

Render a topological schematic that's deliberately not to scale, and carry spatial truth
in annotations instead of character spacing:

- Each Connection is annotated with a standardized distance: rounded meters plus a coarse
  qualitative bucket (adjacent / near / short walk / far). Real meters are always computed
  (haversine on lat/lon); buckets are a tunable presentation layer.
- Each Connection carries an 8-wind bearing (N/NE/E/.../NW).

## Consequences

- Output stays compact and legible regardless of geographic density.
- The LLM gets both a precise anchor (meters) and intuitive pacing (bucket), reducing
  false-precision hallucination.
- We give up a visually faithful map; "where things are" is reconstructed from
  annotations, not seen at a glance. That's acceptable since the consumer is an LLM, not a human
  cartographer.
- Distance buckets and bearing granularity are tunable defaults, exposed as user
  settings after the first prototype.
