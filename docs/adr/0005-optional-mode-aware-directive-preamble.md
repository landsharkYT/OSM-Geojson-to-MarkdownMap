# 5. Optional, mode-aware Directive Preamble (kept separate from map data)

Date: 2026-06-26
Status: Accepted

## Context

The MarkdownMap's primary consumer is an LLM running a roleplay scene. Weak/mid models
(e.g. DeepSeek v4 raw-dumped) need explicit framing ("this is authoritative geography, here
is how to read a connection line, here is where the party is"), or they invent geography
and flip bearings. We could leave that framing to the user or emit it with the
map.

But the artifact also has non-LLM consumers (the deferred React helper, possibly the game
engine), and some users embed the map inside their own system prompt with their own
framing. Baking instructions directly into the data conflates two concerns and pollutes
those uses.

Separately, framing isn't mode-invariant: a whole-area map is "the world map", while a
deferred scene-chunk map is "your immediate surroundings", built around an intrinsic
anchor, whose boundary is not the edge of the world.

## Decision

The Generator emits an optional Directive Preamble: a clearly delimited block at the top
of the document that instructs the consuming LLM. It is:

- Toggleable: `render.directivePreamble`, default on for standalone raw-dump use, off when
  the user supplies their own framing or feeds a non-LLM consumer.
- Structurally separate from the map data (its own block), so it's trivially strippable and
  never leaks into non-LLM consumers.
- Mode-aware: rendered from a template parameterized by render mode. v1 implements
  `whole-area`; the deferred `scene-chunk` mode reuses the seam to emit an intrinsic anchor
  ("you are here at `[NN]`") and off-map edge markers so the model knows the world
  continues past the boundary.

## Consequences

- Weak LLMs get reliable framing with zero user setup; power/non-LLM consumers opt out.
- O(1) token cost, same class as the reading key.
- The Generator gains a "render mode" concept now, before chunking exists, so adding
  scene-chunking is an extension rather than a redesign.
