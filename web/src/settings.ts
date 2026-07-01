// MarkdownMap generation settings (ADR-0011) — the three render-only knobs surfaced by the
// settings button. Distinct from the SVG layer toggles. Keys mirror the C# options DTO
// (camelCase), so JSON.stringify(settings) is the optionsJson passed to WASM.
export type SceneSize = 'tight' | 'standard' | 'wide'

export interface MarkdownMapSettings {
  bidirectional: boolean
  inlineNeighborName: boolean
  directivePreamble: boolean
  // Scene-chunk retrieval (ADR-0016): split the map into self-contained per-area chunks.
  chunking: boolean
  sceneSize: SceneSize
}

export const DEFAULT_SETTINGS: MarkdownMapSettings = {
  bidirectional: false,
  inlineNeighborName: true,
  directivePreamble: true,
  chunking: false,
  sceneSize: 'standard',
}

const KEY = 'mdmap.settings'

export function loadSettings(): MarkdownMapSettings {
  try {
    return { ...DEFAULT_SETTINGS, ...JSON.parse(localStorage.getItem(KEY) ?? '{}') }
  } catch {
    return DEFAULT_SETTINGS
  }
}

export function saveSettings(s: MarkdownMapSettings): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(s))
  } catch {
    /* private mode / disabled storage — settings just won't persist */
  }
}

// Display-only preference for the sidebar. Deliberately *separate* from MarkdownMapSettings:
// it never changes the generated text (Copy/Download stay raw) and must not leak into the WASM
// options DTO. `rendered` = show the MarkdownMap as formatted markdown instead of raw source.
export interface MarkdownDisplaySettings {
  rendered: boolean
}

export const DEFAULT_DISPLAY: MarkdownDisplaySettings = {
  rendered: false,
}

const DISPLAY_KEY = 'mdmap.display'

export function loadDisplay(): MarkdownDisplaySettings {
  try {
    return { ...DEFAULT_DISPLAY, ...JSON.parse(localStorage.getItem(DISPLAY_KEY) ?? '{}') }
  } catch {
    return DEFAULT_DISPLAY
  }
}

export function saveDisplay(s: MarkdownDisplaySettings): void {
  try {
    localStorage.setItem(DISPLAY_KEY, JSON.stringify(s))
  } catch {
    /* private mode / disabled storage — preference just won't persist */
  }
}
