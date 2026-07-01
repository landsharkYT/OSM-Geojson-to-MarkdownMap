import { useEffect, useRef } from 'react'
import type { MarkdownDisplaySettings, MarkdownMapSettings, SceneSize } from '../settings'

interface Props {
  settings: MarkdownMapSettings
  onChange: (next: MarkdownMapSettings) => void
  display: MarkdownDisplaySettings
  onDisplayChange: (next: MarkdownDisplaySettings) => void
  onClose: () => void
}

// Only the boolean knobs render as checkboxes here; sceneSize has its own control below.
type BoolKey = 'bidirectional' | 'inlineNeighborName' | 'directivePreamble' | 'chunking'

const ITEMS: { key: BoolKey; label: string; help: string }[] = [
  {
    key: 'bidirectional',
    label: 'Two-way connections',
    help: 'List each link under both features (off = once, ~half the size).',
  },
  {
    key: 'inlineNeighborName',
    label: 'Inline neighbour names',
    help: "Show the neighbour's name on each connection line.",
  },
  {
    key: 'directivePreamble',
    label: 'Directive preamble',
    help: 'Include the “authoritative map” instruction block for the LLM.',
  },
]

const SCENE_SIZES: { value: SceneSize; label: string }[] = [
  { value: 'tight', label: 'Tight' },
  { value: 'standard', label: 'Standard' },
  { value: 'wide', label: 'Wide' },
]

/** MarkdownMap generation settings (ADR-0011). Render-only knobs; changes re-render live. */
export function SettingsPopover({ settings, onChange, display, onDisplayChange, onClose }: Props) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const onDocMouseDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose()
    }
    const onEsc = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    document.addEventListener('mousedown', onDocMouseDown)
    document.addEventListener('keydown', onEsc)
    return () => {
      document.removeEventListener('mousedown', onDocMouseDown)
      document.removeEventListener('keydown', onEsc)
    }
  }, [onClose])

  return (
    <div
      ref={ref}
      role="dialog"
      aria-label="MarkdownMap settings"
      className="absolute top-full right-0 z-20 mt-1 w-72 rounded-lg border border-slate-200 bg-white p-2 text-left shadow-lg dark:border-slate-700 dark:bg-slate-800"
    >
      <p className="px-1 pb-1 text-xs font-semibold text-slate-500 dark:text-slate-400">MarkdownMap settings</p>
      {ITEMS.map((it) => (
        <label key={it.key} className="flex cursor-pointer gap-2 rounded px-1 py-1.5 hover:bg-slate-50 dark:hover:bg-slate-700/50">
          <input
            type="checkbox"
            className="mt-0.5 accent-sky-600"
            checked={settings[it.key]}
            onChange={(e) => onChange({ ...settings, [it.key]: e.target.checked })}
          />
          <span>
            <span className="block text-sm text-slate-700 dark:text-slate-200">{it.label}</span>
            <span className="block text-xs text-slate-500 dark:text-slate-400">{it.help}</span>
          </span>
        </label>
      ))}

      {/* Chunking (ADR-0016) — split the map into self-contained per-area scene-chunks. */}
      <div className="mt-1 border-t border-slate-100 pt-1 dark:border-slate-700">
        <p className="px-1 py-1 text-xs font-semibold text-slate-500 dark:text-slate-400">Chunking</p>
        <label className="flex cursor-pointer gap-2 rounded px-1 py-1.5 hover:bg-slate-50 dark:hover:bg-slate-700/50">
          <input
            type="checkbox"
            className="mt-0.5 accent-sky-600"
            checked={settings.chunking}
            onChange={(e) => onChange({ ...settings, chunking: e.target.checked })}
          />
          <span>
            <span className="block text-sm text-slate-700 dark:text-slate-200">Scene-chunks</span>
            <span className="block text-xs text-slate-500 dark:text-slate-400">Split the map into self-contained per-area chunks with concrete exits. Download gives a zip; the sidebar shows the selected token’s chunk.</span>
          </span>
        </label>
        {settings.chunking && (
          <div className="flex items-center gap-2 px-1 py-1.5">
            <span className="text-sm text-slate-700 dark:text-slate-200">Scene size</span>
            <div className="flex gap-1" role="group" aria-label="Scene size">
              {SCENE_SIZES.map((s) => (
                <button
                  key={s.value}
                  type="button"
                  aria-pressed={settings.sceneSize === s.value}
                  onClick={() => onChange({ ...settings, sceneSize: s.value })}
                  className={`rounded px-2 py-0.5 text-xs ${
                    settings.sceneSize === s.value
                      ? 'bg-sky-600 text-white'
                      : 'bg-slate-200 text-slate-700 hover:bg-slate-300 dark:bg-slate-700 dark:text-slate-200 dark:hover:bg-slate-600'
                  }`}
                >
                  {s.label}
                </button>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Display — affects only how the sidebar paints the markdown, not its text (see settings.ts). */}
      <div className="mt-1 border-t border-slate-100 pt-1 dark:border-slate-700">
        <p className="px-1 py-1 text-xs font-semibold text-slate-500 dark:text-slate-400">Display</p>
        <label className="flex cursor-pointer gap-2 rounded px-1 py-1.5 hover:bg-slate-50 dark:hover:bg-slate-700/50">
          <input
            type="checkbox"
            className="mt-0.5 accent-sky-600"
            checked={display.rendered}
            onChange={(e) => onDisplayChange({ ...display, rendered: e.target.checked })}
          />
          <span>
            <span className="block text-sm text-slate-700 dark:text-slate-200">Rendered preview</span>
            <span className="block text-xs text-slate-500 dark:text-slate-400">Show the MarkdownMap formatted instead of raw text. Copy / Download still use the raw markdown.</span>
          </span>
        </label>
      </div>
    </div>
  )
}
