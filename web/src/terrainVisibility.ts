import type { TerrainEntry } from './types'

// Mirror of the Generator's markdown terrain filter (MapGenerator.TerrainShownInMarkdown /
// TerrainAreaM2, ADR-0017). Kept identical so the SVG shows exactly the terrain the markdown lists —
// the map must be honest about what the AI sees. This is display-only; it never changes the markdown.
//
// MUST stay in sync with GeneratorOptions.MinTerrainAreaM2 and MapGenerator.TerrainAreaM2.
export const MIN_TERRAIN_AREA_M2 = 5000

/** Metric polygon area via shoelace over the entry's rings; 0 for non-polygons. */
export function terrainAreaM2(e: TerrainEntry): number {
  if (e.geometryType !== 'Polygon') return 0
  let total = 0
  for (const ring of e.parts) {
    if (ring.length < 3) continue
    const mPerLon = 111_320.0 * Math.cos((ring[0][1] * Math.PI) / 180.0)
    const mPerLat = 110_540.0
    let cross = 0
    for (let i = 0; i < ring.length; i++) {
      const a = ring[i]
      const b = ring[(i + 1) % ring.length]
      cross += a[0] * mPerLon * (b[1] * mPerLat) - b[0] * mPerLon * (a[1] * mPerLat)
    }
    total += Math.abs(cross) / 2.0
  }
  return total
}

/**
 * True when this terrain entry appears in the markdown. Barriers and linear shorelines/canals always
 * show; a park/water **polygon** shows only if it is at least MIN_TERRAIN_AREA_M2 (orienting-scale) —
 * sub-threshold pocket parks are omitted from the markdown text, so the map omits them too.
 */
export function terrainShownInMarkdown(e: TerrainEntry): boolean {
  return (e.kind !== 'water' && e.kind !== 'park') || e.geometryType !== 'Polygon' || terrainAreaM2(e) >= MIN_TERRAIN_AREA_M2
}
