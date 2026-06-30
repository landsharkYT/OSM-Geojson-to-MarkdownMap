// MarkdownMap generation settings (ADR-0011) — the three render-only knobs surfaced by the
// settings button. Distinct from the SVG layer toggles. Keys mirror the C# options DTO
// (camelCase), so JSON.stringify(settings) is the optionsJson passed to WASM.
export interface MarkdownMapSettings {
  bidirectional: boolean
  inlineNeighborName: boolean
  directivePreamble: boolean
}

export const DEFAULT_SETTINGS: MarkdownMapSettings = {
  bidirectional: true,
  inlineNeighborName: true,
  directivePreamble: true,
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
