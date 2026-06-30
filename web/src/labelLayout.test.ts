import { describe, it, expect } from 'vitest'
import { visibleLabels, type LabelCandidate } from './labelLayout'

const c = (token: string, x: number, y: number, importance: number): LabelCandidate => ({ token, x, y, importance })

describe('visibleLabels', () => {
  it('shows every label when none overlap', () => {
    const shown = visibleLabels([c('[01]', 0, 0, 90), c('[02]', 0, 200, 80), c('[03]', 0, 400, 70)])
    expect(shown).toEqual(new Set(['[01]', '[02]', '[03]']))
  })

  it('drops the lower-importance label of an overlapping pair', () => {
    // Two dots at the same spot → boxes overlap; the more important one wins.
    const shown = visibleLabels([c('[lo]', 100, 100, 10), c('[hi]', 100, 100, 90)])
    expect(shown.has('[hi]')).toBe(true)
    expect(shown.has('[lo]')).toBe(false)
  })

  it('always shows the forced (selected) label even when it overlaps', () => {
    const shown = visibleLabels([c('[hi]', 100, 100, 90), c('[sel]', 100, 100, 5)], { force: '[sel]' })
    expect(shown.has('[sel]')).toBe(true)
  })

  it('reveals more labels as zoom spreads dots apart', () => {
    const pts = [c('[01]', 0, 0, 90), c('[02]', 20, 0, 80)] // 20px apart pre-zoom
    const zoomedOut = visibleLabels(pts.map((p) => ({ ...p, x: p.x * 1 })))
    const zoomedIn = visibleLabels(pts.map((p) => ({ ...p, x: p.x * 10 })))
    expect(zoomedOut.size).toBeLessThan(zoomedIn.size)
    expect(zoomedIn.size).toBe(2)
  })
})
