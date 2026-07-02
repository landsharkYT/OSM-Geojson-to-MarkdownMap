# 1. GeoJSON as the pipeline contract (two independent stages)

Date: 2026-06-26
Status: Accepted

## Context

The system turns dense OpenStreetMap extracts into LLM-digestible textual maps. The
work splits naturally into "understand OSM" and "produce a map". We could fuse these
into one OSM→MarkdownMap pass, or separate them behind an intermediate format. The
user's stated mental model was already two-stage: OSM → (less-dense) GeoJSON → Map.

The Generator may eventually run *inside a game* to regenerate maps, while OSM parsing
is a heavy offline step. Testing a generator against 11 MB of XML is also painful.

## Decision

GeoJSON is a hard contract boundary, not just an incidental intermediate.

- **Stage 1, Normalizer**: the only OSM-aware component. Streams OSM/XML, emits GeoJSON,
  and translates raw OSM tags into a stable normalized Feature property schema (`name`,
  `category`, `importance`, ...).
- **Stage 2, Generator**: OSM-agnostic. Consumes GeoJSON (from the Normalizer or any other
  source) and ranks/draws using only the normalized schema.

## Consequences

- The Generator is testable with small hand-authored GeoJSON; no OSM needed.
- Any GeoJSON source can feed the map generator, not just OSM.
- Stage 2 can be a lean, portable library (see ADR-0004 stack note) suitable for
  later in-game use, decoupled from the heavy parser.
- Cost: the normalized schema is now itself a contract that must be designed and
  versioned. Importance ranking depends on Stage 1 populating it correctly.
- GeoJSON cannot represent OSM topology (shared-node connectivity). This is fine for
  v1 but constrains the deferred road-network feature; see ADR-0003.
