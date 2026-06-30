import { describe, it, expect } from 'vitest'
import { convexHull, type Point } from './hull'

describe('convexHull', () => {
  it('drops interior points and keeps the corners of a square', () => {
    const square: Point[] = [
      [0, 0], [2, 0], [2, 2], [0, 2],
      [1, 1], // interior → must be dropped
    ]
    const hull = convexHull(square)
    expect(hull).toHaveLength(4)
    for (const corner of [[0, 0], [2, 0], [2, 2], [0, 2]] as Point[])
      expect(hull).toContainEqual(corner)
    expect(hull).not.toContainEqual([1, 1])
  })

  it('fills the notch of a concave (L-shaped) outline', () => {
    // L-shape: hull is the full bounding square, so the notch corner is excluded and the
    // outer square corners are present.
    const ell: Point[] = [
      [0, 0], [0, 2], [1, 2], [1, 1], [2, 1], [2, 0],
    ]
    const hull = convexHull(ell)
    expect(hull).toContainEqual([2, 0])
    expect(hull).not.toContainEqual([1, 1]) // the reflex (notch) vertex
  })

  it('returns the points unchanged when fewer than 3', () => {
    expect(convexHull([[0, 0], [1, 1]])).toHaveLength(2)
  })
})
