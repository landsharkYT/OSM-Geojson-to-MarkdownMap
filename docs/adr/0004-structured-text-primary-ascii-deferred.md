# 4. Structured connection text is the primary output; drawn ASCII art deferred

Date: 2026-06-26
Status: Accepted

## Context

Both early reference sketches (a hand-authored topological map and the `[03]-[42]-[11]`
example) are literally-drawn 2D ASCII diagrams. Generating those programmatically from arbitrary
geographic data is a hard graph-layout-into-a-character-grid problem: fragile,
collision-prone, and worst exactly when data is dense (which the target data is).

More importantly, the consumer is an LLM. LLMs parse explicit structured text
(`[42] Library → 140m NE → [11] Bank`) more reliably than they trace `|` and `-`
across a grid. The pretty picture may *cost* tokens and accuracy rather than add them.

## Decision

The primary rendered form of the schematic is structured connection text: per-Feature
adjacency blocks (Token, full name inline, category/district, and neighbor links each
carrying distance and 8-wind bearing). The document is Header + Districts + Connections;
no separate legend, since names live in the blocks.

Drawn ASCII art is deferred to a later "nice view", appropriate for small maps.

## Consequences

- Holds up at any density; the highest-build-risk component (2D ASCII layout) is removed
  from v1.
- Likely better LLM spatial reasoning and lower token cost than ASCII art.
- We lose the at-a-glance human-readable picture for now, which is acceptable since the
  primary consumer is the model. ASCII can be layered on later without changing the data
  model.
