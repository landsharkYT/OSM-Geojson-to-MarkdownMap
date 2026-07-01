# Institutional buildings: capture, venue-band campus halls, and the footprint-area nudge

An eyeball audit of two real extracts found the largest remaining extraction gap: on a campus,
**named institutional buildings** — `building=university` lecture halls (a campus's named lecture
halls), plus `building=hospital/school/stadium/station/…` — were dropped entirely, because the
Classifier only recognized `building=*` for worship and named-residential. These halls are exactly the
anchors a Dungeon Master narrates ("the party crosses from Maple Hall toward the Chemistry Building"),
so a campus map rendered its cafés and sculptures but none of its actual fabric. This extends ADR-0018.

We decided:

- **Capture the institutional set** (`building`=university/college/school/hospital/civic/government/
  public/fire_station/stadium/train_station, `+railway`/`public_transport`=station), **named-only** —
  an unnamed hall is a weak anchor and clusters as a generic building, same gate as residential.
  Ancillary shells (`boathouse`, `gatehouse`, `retail`, `commercial`, `industrial`) stay out: a named
  `building=retail` is just the envelope of a `shop=*` we already capture, and industrial/commercial
  names are usually the owner, not a scene landmark.
- **Split salience.** The singular institutions (hospital, school, civic, stadium, station, and dorms
  via `building=dormitory` → `lodging.student_accommodation`) are **core** — few, major, matching the
  existing `amenity=` institution rule. But `building=university`/`college` map to a distinct
  `civic.<t>_building` category and are **budgeted**, not core: a campus has 100+ halls, so making them
  core would flood the promotion budget it exists to enforce. A hall competes for a District's K seats
  like a café.
- **Venue band, not institution band.** A hall scores at base **55** (→ 65 with a name), tied with
  food/shops — a lecture hall is a *destination you go to*, and this lets a campus promote a realistic
  **mix** (major halls *and* busy venues) rather than a blanket halls-first sweep.
- **A footprint-area nudge** breaks the otherwise-identical hall scores so the *big* halls win the
  budget instead of an arbitrary 20. Because area is geometry (not a tag), the pure tag-only
  `Classifier` stays untouched: the nudge is applied in the **Normalizer** on the way path only, via a
  `SalienceClassifier.IsAreaRankedBuilding(category)` predicate. It is a bounded log ramp over a
  200 m² floor, **capped at +9** (hall tops at 74) so every budgeted feature stays strictly below the
  core line (75) — the invariant "core institutions always outrank budgeted destinations" holds no
  matter how huge the footprint.

## Consequences

- Confirmed on the real campus extract: 103 halls captured, importance now spreads 65–73 (was a flat
  tie), 70 promoted / 33 clustered — the budget is doing its job, not being bypassed — interleaved
  with dorms, libraries, worship, venues, and the 2 new stations.
- The pure-`Classifier` / geometry-in-`Normalizer` boundary is *deliberately* breached for one signal
  (footprint area). This is the surprising part: a future reader will wonder why importance isn't
  fully determined by tags. It is confined to the area-ranked building categories and clamped so it can
  only reorder within the budgeted tie, never jump a tier.
- `place=square` was considered and **rejected** — only a couple exist, "square" is semantically fuzzy
  (some are road junctions), and mapping a `place=*` to a POI would muddy the clean "place = District
  anchor" rule.
