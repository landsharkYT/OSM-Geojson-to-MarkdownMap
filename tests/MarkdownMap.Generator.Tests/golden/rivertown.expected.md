# MARKDOWNMAP — Old Town & Harborside, Rivertown

<!-- DIRECTIVE PREAMBLE · mode=whole-area -->

**TO THE ASSISTANT — READ FIRST.** The document below is the **authoritative map** of
this world. Every position, distance, and direction is canon. **Do not invent
geography or places that are not listed here.** When a scene involves *where* something
is or *how far*, consult this map and reason from it. If something is not on the map, it
is unknown — say so rather than guessing.

<!-- /DIRECTIVE PREAMBLE -->

## How to read this map

- **Feature header** = `[token] Name (category)`, optionally `· on <street> · <district>`.
- **Connection** = `→ [token] Name — ~<metres>m <DIR>, <size>`, read as: from the current
  feature, the named one lies `<metres>` away in compass direction `<DIR>` (N = north/up;
  8-wind: N NE E SE S SW W NW). `<size>`: **adjacent** <25 m · **near** <150 m ·
  **short walk** <500 m · **far** ≥500 m.
- A `[crosses <road>]` flag means a road or rail line lies between the two features —
  passable at a crossing, but **not** a clean walkable hop.
- A `[separated by water]` flag means open water (lake, river, or canal) lies between them:
  the distance is real but you cannot walk it directly. A feature marked `stands apart` is
  reached only across water. **Terrain & barriers** lists water, parks, and barriers with
  their rough position for orientation.
- **Districts** group features; `spine:` lists them in order along the district's axis;
  `clustered:` counts minor features not shown individually.
- Layout is **not to scale** — only the numbers and compass letters are real. Neighbours
  are straight-line closeness, not road distance.

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
[01] Founders Mural (landmark.artwork) · on Main Street · Old Town
   → [02] The Old Mill — ~25m SE, near
   → [03] Old Town Library — ~55m S, near
   → [05] The Anchor Tavern — ~80m S, near

[02] The Old Mill (landmark.attraction) · on Main Street · Old Town
   → [01] Founders Mural — ~25m NW, near
   → [03] Old Town Library — ~35m S, near
   → [05] The Anchor Tavern — ~60m S, near

[03] Old Town Library (civic.library) · on Main Street · Old Town
   → [05] The Anchor Tavern — ~25m S, adjacent
   → [06] Bluebird Coffee — ~35m S, near
   → [02] The Old Mill — ~35m N, near
   → [01] Founders Mural — ~55m N, near

[04] Riverside School (civic.school) · near 10th Street · Harborside
   → [08] Rivertown Market — ~95m NW, near [crosses Route 9]
   → [09] Mill Street Deli — ~95m NW, near [crosses Route 9]
   → [10] Pino's Pizza — ~100m W, near [crosses Route 9]

[05] The Anchor Tavern (food.bar) · on Main Street · Old Town
   → [06] Bluebird Coffee — ~15m SE, adjacent
   → [03] Old Town Library — ~25m N, adjacent
   → [07] Corner Cafe — ~25m S, adjacent
   → [02] The Old Mill — ~60m N, near
   → [01] Founders Mural — ~80m N, near

[06] Bluebird Coffee (food.cafe) · on Main Street · Old Town
   → [05] The Anchor Tavern — ~15m NW, adjacent
   → [07] Corner Cafe — ~20m SW, adjacent
   → [08] Rivertown Market — ~20m S, adjacent
   → [03] Old Town Library — ~35m N, near

[07] Corner Cafe (food.cafe) · on Main Street · Harborside
   → [06] Bluebird Coffee — ~20m NE, adjacent
   → [08] Rivertown Market — ~20m SE, adjacent
   → [05] The Anchor Tavern — ~25m N, adjacent
   → [09] Mill Street Deli — ~25m S, adjacent
   → [10] Pino's Pizza — ~35m S, near

[08] Rivertown Market (shop.convenience) · on Main Street · Harborside
   → [09] Mill Street Deli — ~15m SW, adjacent
   → [07] Corner Cafe — ~20m NW, adjacent
   → [06] Bluebird Coffee — ~20m N, adjacent
   → [10] Pino's Pizza — ~25m SW, near
   → [04] Riverside School — ~95m SE, near [crosses Route 9]

[09] Mill Street Deli (food.deli) · on Main Street · Harborside
   → [08] Rivertown Market — ~15m NE, adjacent
   → [10] Pino's Pizza — ~15m SW, adjacent
   → [07] Corner Cafe — ~25m N, adjacent
   → [04] Riverside School — ~95m SE, near [crosses Route 9]

[10] Pino's Pizza (food.restaurant) · on Main Street · Harborside
   → [09] Mill Street Deli — ~15m NE, adjacent
   → [08] Rivertown Market — ~25m NE, near
   → [07] Corner Cafe — ~35m N, near
   → [04] Riverside School — ~100m E, near [crosses Route 9]
```
