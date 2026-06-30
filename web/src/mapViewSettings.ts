// Map view settings (ADR-0013) — display-only, pure client-side SVG state, never touches the
// pipeline. Distinct from MarkdownMapSettings (settings.ts), which change the generated text.

export interface Layers {
  terrain: boolean
  edges: boolean
  minors: boolean
  tokens: boolean
}

export interface MapViewSettings {
  layers: Layers
  /** Terrain drawn soft/faint as approximate orientation context (honest about ADR-0008). */
  approximateTerrain: boolean
}

export const DEFAULT_MAP_VIEW: MapViewSettings = {
  layers: { terrain: true, edges: true, minors: true, tokens: true },
  approximateTerrain: true,
}

const KEY = 'mdmap.mapview'

export function loadMapView(): MapViewSettings {
  try {
    const saved = JSON.parse(localStorage.getItem(KEY) ?? '{}')
    return {
      ...DEFAULT_MAP_VIEW,
      ...saved,
      layers: { ...DEFAULT_MAP_VIEW.layers, ...(saved.layers ?? {}) },
    }
  } catch {
    return DEFAULT_MAP_VIEW
  }
}

export function saveMapView(v: MapViewSettings): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(v))
  } catch {
    /* private mode — settings just won't persist */
  }
}
