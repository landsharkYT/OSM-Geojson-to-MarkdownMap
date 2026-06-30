import type { MapModel } from './types'

export interface Projection {
  project: (lon: number, lat: number) => [number, number]
  width: number
  height: number
}

/** Derive a bounding box from all coordinates in the model (fallback when bounds is empty). */
export function computeBounds(m: MapModel): [number, number, number, number] {
  if (m.bounds.length === 4) return m.bounds as [number, number, number, number]
  let minLon = Infinity, minLat = Infinity, maxLon = -Infinity, maxLat = -Infinity
  const add = (lon: number, lat: number) => {
    if (lon < minLon) minLon = lon
    if (lat < minLat) minLat = lat
    if (lon > maxLon) maxLon = lon
    if (lat > maxLat) maxLat = lat
  }
  for (const f of m.features) add(f.lon, f.lat)
  for (const f of m.minors) add(f.lon, f.lat)
  for (const t of m.terrain) for (const part of t.parts) for (const [lon, lat] of part) add(lon, lat)
  if (!isFinite(minLon)) return [-1, -1, 1, 1]
  return [minLon, minLat, maxLon, maxLat]
}

/**
 * Equirectangular projection fitted to the bounds, north up, with longitude compressed by
 * cos(latitude) so the neighbourhood isn't horizontally stretched. Tiny-area scale, so no
 * full map projection is needed.
 */
export function makeProjection(
  bounds: [number, number, number, number],
  width: number,
  height: number,
  pad = 24,
): Projection {
  const [minLon, minLat, maxLon, maxLat] = bounds
  const midLat = (minLat + maxLat) / 2
  const kx = Math.cos((midLat * Math.PI) / 180) || 1
  const w = (maxLon - minLon) * kx || 1e-9
  const h = maxLat - minLat || 1e-9
  const scale = Math.min((width - 2 * pad) / w, (height - 2 * pad) / h)
  const ox = (width - w * scale) / 2
  const oy = (height - h * scale) / 2

  const project = (lon: number, lat: number): [number, number] => [
    ox + (lon - minLon) * kx * scale,
    oy + (maxLat - lat) * scale, // invert latitude → north is up
  ]
  return { project, width, height }
}
