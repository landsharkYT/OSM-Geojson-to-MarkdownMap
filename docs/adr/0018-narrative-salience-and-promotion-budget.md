# 18. Narrative salience + per-District promotion budget

Date: 2026-07-01
Status: Accepted

> **Refined by ADR-0019.** Institutional buildings: singular institutions/stations/dorm buildings
> join the core, while `building=university`/`college` campus halls are budgeted venue-band
> competitors with a footprint-area nudge, so a campus's 100+ halls don't flood the budget.
>
> **Update 2026-07-01: civic salience is an allowlist, not a denylist.** `SalienceClassifier` keyed
> `civic` as core unless explicitly budgeted, so `healthcare=*` passthrough (psychotherapist,
> physiotherapist, alternative) and `amenity=social_facility` silently fell through to core at
> importance 90: always promoted, bypassing the budget, flooding a medical-dense district. Inverted
> to a positive `CoreCivicInstitution` allowlist (school/college/university/library/hospital/
> post_office/townhall/police/fire_station/courthouse/station/community_centre/civic/kindergarten);
> everything else civic defaults to budgeted and competes. Fail-safe: an un-enumerated civic
> subclass now budgets rather than promoting at institution rank. Same change dropped private
> practices (dentist/clinic/doctors) from base 60 to 55 so they tie venues instead of outranking
> them, and dropped `government`/`public` from the institution-building set, since they collide with
> the budgeted `civic.government` office subclass. Net on the real campus extract: promoted medical
> offices went 28 → 9, venues surfaced 11 → 33, ~340 fewer tokens.

Extends [ADR-0012](0012-name-resolution-and-co-location-merge.md) (name resolution, importance,
tiered unnamed promotion). ADR-0012's promotion rule, that every `landmark` and named `destination`
Feature gets a token, doesn't survive a dense extract.

## Context

Run against a dense campus + retail extract (~570 kept Features), the pipeline promoted all of
them: ~80 restaurants, ~60 cafés, ~40 clothing shops, every private dental / physiotherapy /
psychiatry office, two of the same chain coffee shop 100 m apart, and ~45 campus sculptures. Three
root causes:

- `civic` is `landmark`-tier and lumps institutions with private practices, so a dentist's office
  ranked like a school or hospital.
- Every `destination` promotes: `food`/`shop`/`leisure` all promote once named, with no cap.
- A resolved `brand` name raises importance (+10), so chains outranked the independent café next
  door.

The result was a ~24k-token whole-area map, and (compounding
[ADR-0017](0017-subdivide-districts-by-density-gap-bisection.md)) chunking that fragmented into
dozens of trivial one- and two-Feature "scenes". A modest neighbourhood extract looked fine only
because it had fewer such Features; the rules didn't adapt, they just weren't stressed. We want
the same rules to yield a comparably legible map on both.

## Decision

A layered curation: parse-time salience flags decide who's scene-worthy, and a per-District
budget bounds how many.

1. Narrative salience (Stage 1, from tags). The Normalizer sorts each Feature into:
   - core: always promotes. Worship, civic institutions (school, library, hospital, post office,
     university building), historic sites, major venues. (Splits ADR-0012's `civic`: institutions
     stay core, private practices drop to budgeted.)
   - budgeted: competes for a token. Artwork/monuments, food, shops, private services (dental,
     physio, salon, boutique), small leisure.
   - clustered: never its own token. Residential, minor, structure.

2. Chain flag (Stage 1). A `brand` tag marks a chain and lowers importance (and the old +10
   brand-name bonus is removed). Independents win the budget first; a chain still promotes where
   there's headroom and fades out where density is high. This isn't exclusion: a lone chain in a
   sparse block still shows.

3. Unified per-District promotion budget (Stage 2). Per District, the salience core all promotes;
   the budgeted Features then fill up to K remaining seats by importance; the rest fold into the
   District's clustered count. One budget covers artwork and commodity, so neither a
   sculpture-dense nor a retail-dense area can explode.

4. Fixed top-K default, tunable. K is a per-District default (~20). Self-adapting: a dense
   District clusters most of its long tail; a sparse one promotes nearly all. Model-affecting
   (changes which Features get a token), so it's a rebuild concern, not a render-only knob
   (ADR-0011).

5. Pipeline split. Flags live in Stage 1 (the only stage with tags); the budget lives in Stage 2
   (Districts are a Stage-2 concept). Clean boundary, no tags leak downstream.

## Consequences

- A hyper-dense extract and a modest one come out comparably legible without per-city tuning. The
  whole-area token count drops sharply, and with fewer promoted Features, ADR-0017 chunking yields
  far fewer, more substantial chunks.
- The token set shifts: a Feature that used to have a token may now cluster. This ripples to every
  downstream consumer (chunk files, a retrieval store keyed on tokens), so it's model-affecting and
  versionable, not a live toggle.
- The guaranteed core protects the map's backbone (you never budget-out the church or the
  hospital), which is the main risk of a pure budget.
- Heuristic defaults: K, the salience→class mapping, and the chain penalty are tunable defaults,
  not law. Independent of the ADR-0017 balance guard (chunk-splitter fix); both are needed, and
  they address different stages.

Not chosen: salience-only (no density bound, so a genuinely restaurant-dense block still promotes
40 restaurants); budget-only (no salience, so it could cluster a key landmark while keeping a
chain); commodity-only budget (leaves the artwork explosion uncapped); per-km² rate (truest
spatial balance but needs a District area plus a rate constant); proportional fraction (scales
but doesn't bound absolute density).
