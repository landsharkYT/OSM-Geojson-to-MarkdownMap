# 16. Scene-chunk retrieval (realizes ADR-0005's reserved mode)

Date: 2026-06-30
Status: Accepted

Realizes the `scene-chunk` render mode reserved by ADR-0005; un-defers the chunking scope
named in CONTEXT.md ("Scope (v1)", "Anchor", "Off-map edge").

## Context

A whole-area MarkdownMap renders the **entire** extract. For a **storytelling LLM** running a
long scene, that's wasteful and unfocused: it re-reads the whole world every turn, and a dense
city map buries the handful of places the party can actually see. ADR-0005 anticipated this and
built a "render mode" concept so chunking would be an *extension, not a redesign*, naming a
deferred `scene-chunk` mode with an intrinsic **Anchor** and **off-map edges**.

The win we're optimizing is **in-the-moment narrative quality + token economy**, not RAG
plumbing for its own sake. That framing drove every choice below toward "what helps the
storyteller tell *this* scene," and away from arbitrary, even-but-meaningless partitions.

## Decision

A new **generation** setting, **Chunking**, renders the map as a set of self-contained
**scene-chunks** instead of one document.

1. **Partition by District, spine-split to scene size.** *(The spine-split half of this decision is
   superseded by [ADR-0017](0017-subdivide-districts-by-density-gap-bisection.md): oversized
   Districts subdivide by density-gap bisection, not spine segments. District-as-chunk stands.)*
   A chunk = a District (the existing
   named neighbourhood, anchored on its spine head). Districts over the **scene size** target
   split into contiguous segments **along their spine** (`Old Town · N` / `Old Town · S`) — never
   re-clustered, so a chunk stays spatially contiguous and narratable. Extracts with no `place`
   anchors fall back to proximity clustering. Named, meaningful seams beat even-but-arbitrary
   grid cells for storytelling ("the party moves from Old Town to Harborside" is a scene change).

2. **Each chunk is fully self-contained** (consumed one at a time by a retriever): a **compact**
   reading key, a `scene-chunk` Directive Preamble ("this is the *<name>* area; the party is
   somewhere within; here are the ways out"), local bounds, the **local terrain subset** (only
   terrain bordering the chunk's bounds), the chunk's promoted features + intra-chunk
   connections, a local clustered-minor count, and its exits. No whole-world repetition.

3. **Concrete off-map exits.** For each adjacent chunk, one exit line: neighbour name + bearing +
   the specific **boundary feature** just across + distance (`→ NE toward Harborside (off-map):
   via [42] Dock Gate, ~120m`), derived by aggregating boundary-crossing proximity edges per
   neighbour. A thin halo of exactly the features you'd walk to — enough to narrate the
   transition without importing the neighbour.

4. **Fixed, key-feature anchor → a static chunk set.** Each chunk is oriented on its landmark,
   not regenerated per query, giving a finite cacheable partition retrieved by where the party
   is. The party's *exact* spot is the consumer's to narrate.

5. **Global, stable tokens.** A feature keeps **one** token across every chunk and every exit
   reference, so the party's location and cross-chunk references stay coherent turn to turn
   (`[42]` is Dock Gate in its own chunk and in any exit pointing at it). The cost — sparse
   numbering within a small chunk — is worth the continuity.

6. **Setting surface:** on/off + **scene size** (Tight / Standard / Wide ≈ 8 / 14 / 22 promoted
   features), Standard default. The existing `bidirectional` / `inlineNeighborName` knobs still
   apply *within* a chunk.

7. **Delivery:** the Explorer downloads a **zip** — `manifest.md` (name · file · bbox · anchor ·
   neighbours) plus one `.md` per chunk — and the sidebar shows the chunk for the selected token
   (Copy copies that one chunk). Drop-in for a retrieval store.

8. **Render-only, structured output.** Chunking is computed entirely over the existing MapModel
   (districts, edges, positions, terrain are already there), so it honors ADR-0011: **no
   re-parse**. But it changes the *output shape* from one markdown string to a **chunk set +
   manifest** — so the MapModel grows a `Chunks`/`Manifest` structure and the WASM render
   contract returns that structure, not a bare string.

## Consequences

- The storyteller loads only the party's chunk (~hundreds of tokens), with concrete exits as
  narrative seams — sharp local focus, cheap per turn, coherent across moves.
- **Contract grows:** WASM render returns structured chunks + manifest (not a string); a new
  client dependency (`fflate`) zips them. Whole-area mode is unchanged and stays the default.
- Global tokens couple the chunks: renumbering or dropping a feature shifts references
  everywhere — deliberate, and the reason it's an ADR.
- Realizes ADR-0005's `scene-chunk` preamble seam and un-defers the Anchor / Off-map-edge
  glossary terms; "Scope (v1)" is revised — chunking is no longer deferred.
- Markdown guardrail (ADR-0014/0015) holds per chunk: terrain stays coarse, geometry never
  enters the text.
- Deferred: chunk **overlap** beyond the exit halo, and a non-District (grid/token-budget)
  partition — revisit only if a concrete map shows the District partition failing.
