# MARKDOWNMAP — Old Town & Harborside, Rivertown

<!-- DIRECTIVE PREAMBLE · mode=whole-area -->

**Authoritative map — treat as canon.** Do not invent geography or places not listed
here; if something is not on the map, it is unknown — say so rather than guessing.

<!-- /DIRECTIVE PREAMBLE -->

## How to read

- `[token] Name (category)` optionally `· on <street> · <district>`.
- `→ [token] Name — ~<m>m <DIR>`: the named feature lies ~<m> metres away, compass
  `<DIR>` (N = up; 8-wind). Straight-line closeness, **not to scale** — only the numbers
  and letters are real.
- Flags: `[crosses <road>]` a road/rail lies between (cross at a crossing); `[separated by
  water]` open water lies between (not directly walkable); `stands apart` reached only across water.
- `spine:` orders a district along its axis; `clustered:` counts minor features not listed.

**Bounds:** 45.5194 N,-120.5008 W → 45.5212 N,-120.499 E · **North is up.**

## Terrain & barriers

- Mill Lake (water) · W · open water
- Riverside Park (park) · N · green space
- Route 9 (barrier:motorway) · runs N–S, E · impassable except at crossings

## Districts

### Harborside — Main Street area
spine: NW–SE along Main Street: [07],[10],[09],[08],[04]
promoted: 5
clustered: ~2 minor

### Old Town — Main Street area
spine: N–S along Main Street: [01],[02],[03],[05],[06]
promoted: 5
clustered: ~2 minor

## Connections

```
[01] Founders Mural (landmark.artwork) · Old Town
   → [02] The Old Mill — ~25m SE
   → [03] Old Town Library — ~55m S
   → [05] The Anchor Tavern — ~80m S

[02] The Old Mill (landmark.attraction) · Old Town
   → [03] Old Town Library — ~35m S
   → [05] The Anchor Tavern — ~60m S

[03] Old Town Library (civic.library) · Old Town
   → [05] The Anchor Tavern — ~25m S
   → [06] Bluebird Coffee — ~35m S

[04] Riverside School (civic.school) · near 10th Street · Harborside
   → [08] Rivertown Market — ~95m NW [crosses Route 9]
   → [09] Mill Street Deli — ~95m NW [crosses Route 9]
   → [10] Pino's Pizza — ~100m W [crosses Route 9]

[05] The Anchor Tavern (food.bar) · Old Town
   → [06] Bluebird Coffee — ~15m SE
   → [07] Corner Cafe — ~25m S

[06] Bluebird Coffee (food.cafe) · Old Town
   → [07] Corner Cafe — ~20m SW
   → [08] Rivertown Market — ~20m S

[07] Corner Cafe (food.cafe) · Harborside
   → [08] Rivertown Market — ~20m SE
   → [09] Mill Street Deli — ~25m S
   → [10] Pino's Pizza — ~35m S

[08] Rivertown Market (shop.convenience) · Harborside
   → [09] Mill Street Deli — ~15m SW
   → [10] Pino's Pizza — ~25m SW

[09] Mill Street Deli (food.deli) · Harborside
   → [10] Pino's Pizza — ~15m SW

[10] Pino's Pizza (food.restaurant) · Harborside
```
