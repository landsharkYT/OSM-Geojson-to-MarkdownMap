import { describe, expect, it } from 'vitest'
import { terrainAreaM2, terrainShownInMarkdown, MIN_TERRAIN_AREA_M2 } from './terrainVisibility'
import type { TerrainEntry } from './types'

// A square polygon of `side` metres near the equator-ish lat 47, expressed back in lon/lat.
function squarePark(side: number): TerrainEntry {
  const lat = 47.6
  const mPerLon = 111_320 * Math.cos((lat * Math.PI) / 180)
  const mPerLat = 110_540
  const dLon = side / mPerLon
  const dLat = side / mPerLat
  return {
    name: 'Test', kind: 'park', kindLabel: 'park', note: '', position: '', geometryType: 'Polygon',
    parts: [[[0, lat], [dLon, lat], [dLon, lat + dLat], [0, lat + dLat]]],
  }
}

describe('terrainVisibility (mirror of the Generator markdown filter)', () => {
  it('computes polygon area within a few percent', () => {
    // 100 m square ≈ 10,000 m²
    expect(terrainAreaM2(squarePark(100))).toBeGreaterThan(9500)
    expect(terrainAreaM2(squarePark(100))).toBeLessThan(10500)
  })

  it('hides a sub-threshold pocket park but shows an orienting-scale one', () => {
    expect(terrainShownInMarkdown(squarePark(50))).toBe(false)   // ~2,500 m² < 5,000
    expect(terrainShownInMarkdown(squarePark(100))).toBe(true)   // ~10,000 m² ≥ 5,000
    expect(MIN_TERRAIN_AREA_M2).toBe(5000)
  })

  it('always shows barriers and linear shorelines regardless of extent', () => {
    const barrier: TerrainEntry = {
      name: 'Hwy', kind: 'barrier', kindLabel: 'barrier:motorway', note: '', position: '',
      geometryType: 'LineString', parts: [[[0, 47], [0.001, 47]]],
    }
    const shoreline: TerrainEntry = { ...barrier, kind: 'water', kindLabel: 'water', geometryType: 'LineString' }
    expect(terrainShownInMarkdown(barrier)).toBe(true)
    expect(terrainShownInMarkdown(shoreline)).toBe(true)
  })
})
