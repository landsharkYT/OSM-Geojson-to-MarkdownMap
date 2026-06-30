// Mirrors the C# MapModel (docs/map-model.md), serialized camelCase.

export interface MapModel {
  title: string
  bounds: number[] // [minLon, minLat, maxLon, maxLat] or []
  features: PromotedFeature[]
  minors: MinorFeature[]
  edges: Edge[]
  districts: District[]
  terrain: TerrainEntry[]
  markdown: string
}

export interface PromotedFeature {
  token: string
  name: string
  category: string
  importance: number
  tier: string
  street?: string
  streetApprox?: boolean
  district?: string
  lon: number
  lat: number
}

export interface MinorFeature {
  name: string
  category: string
  district?: string
  lon: number
  lat: number
}

export interface Edge {
  fromToken: string
  toToken: string
  toName: string
  meters: number
  dir: string
  bucket: string
  crosses?: string
  separatedByWater?: boolean
}

export interface District {
  name: string
  street?: string
  spineDir: string
  spineTokens: string[]
  promotedCount: number
  clusteredCount: number
  anchorLon: number
  anchorLat: number
}

export interface TerrainEntry {
  name: string
  kind: string // water | park | barrier
  kindLabel: string // water | park | barrier:<class>
  note: string
  position: string
  geometryType: string // LineString | Polygon
  parts: number[][][] // each part: [[lon,lat], ...]
}

export interface ErrorResult {
  error: string
}

export type BuildResult = MapModel | ErrorResult

export function isError(r: BuildResult): r is ErrorResult {
  return (r as ErrorResult).error !== undefined
}
