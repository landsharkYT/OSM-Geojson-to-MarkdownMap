import { describe, it, expect } from 'vitest'
import { computeBounds, makeProjection } from './projection'
import { rivertown } from './test/fixture'

describe('makeProjection', () => {
  it('puts north at the top and fits the box', () => {
    const p = makeProjection([0, 0, 1, 1], 1000, 1000, 0)
    const [, topY] = p.project(0, 1) // max latitude
    const [, botY] = p.project(0, 0) // min latitude
    expect(topY).toBeLessThan(botY) // north is up
    expect(topY).toBeCloseTo(0, 3)
    expect(botY).toBeCloseTo(1000, 3)
  })

  it('keeps points within the viewport', () => {
    const p = makeProjection(computeBounds(rivertown), 1000, 750)
    for (const f of rivertown.features) {
      const [x, y] = p.project(f.lon, f.lat)
      expect(x).toBeGreaterThanOrEqual(0)
      expect(x).toBeLessThanOrEqual(1000)
      expect(y).toBeGreaterThanOrEqual(0)
      expect(y).toBeLessThanOrEqual(750)
    }
  })
})

describe('computeBounds', () => {
  it('uses model bounds when present', () => {
    expect(computeBounds(rivertown)).toEqual(rivertown.bounds)
  })

  it('derives bounds from coordinates when empty', () => {
    const b = computeBounds({ ...rivertown, bounds: [] })
    expect(b[0]).toBeLessThanOrEqual(b[2])
    expect(b[1]).toBeLessThanOrEqual(b[3])
  })
})
