// Map view settings (ADR-0013) — display-only, pure client-side SVG state, never touches the
// pipeline. Distinct from MarkdownMapSettings (settings.ts), which change the generated text.

export interface Layers {
  terrain: boolean
  edges: boolean
  minors: boolean // named minor features (ADR-0020)
  props: boolean  // nameless prop features (ADR-0020)
  tokens: boolean
}

export interface MapViewSettings {
  layers: Layers
  /**
   * Terrain geometry mode (ADR-0013/0014). On = real shapes (rings + shoreline lines);
   * off = the old convex-hull extent blobs, recomputed client-side.
   */
  detailedTerrain: boolean
}

// `minors` and `props` default OFF so the map is honest by default: it shows only what the AI sees in
// the markdown (promoted tokens + orienting terrain). Prop features are never in the markdown, and
// minor features only when their setting is on — drawing either can make the map disagree with the
// text, so both are opt-in behind the adaptive honesty banner (ADR-0020).
export const DEFAULT_MAP_VIEW: MapViewSettings = {
  layers: { terrain: true, edges: true, minors: false, props: false, tokens: true },
  detailedTerrain: true,
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
