// Convex hull (Andrew's monotone chain) for the "Detailed terrain: off" view (ADR-0013) — the
// old convex-hull extent blob (ADR-0008's look), recomputed client-side from the terrain points
// the Explorer already holds, so the toggle stays a pure display switch (no pipeline / rebuild).
export type Point = [number, number]

/** Convex hull as an ordered ring of points. Returns the input (deduped) when < 3 points. */
export function convexHull(points: Point[]): Point[] {
  const pts = points.slice().sort((a, b) => a[0] - b[0] || a[1] - b[1])
  // de-duplicate adjacent equal points after sort
  const uniq: Point[] = []
  for (const p of pts) {
    const last = uniq[uniq.length - 1]
    if (!last || last[0] !== p[0] || last[1] !== p[1]) uniq.push(p)
  }
  const n = uniq.length
  if (n < 3) return uniq

  const cross = (o: Point, a: Point, b: Point) =>
    (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

  const lower: Point[] = []
  for (const p of uniq) {
    while (lower.length >= 2 && cross(lower[lower.length - 2], lower[lower.length - 1], p) <= 0) lower.pop()
    lower.push(p)
  }
  const upper: Point[] = []
  for (let i = n - 1; i >= 0; i--) {
    const p = uniq[i]
    while (upper.length >= 2 && cross(upper[upper.length - 2], upper[upper.length - 1], p) <= 0) upper.pop()
    upper.push(p)
  }
  lower.pop()
  upper.pop()
  return lower.concat(upper)
}
