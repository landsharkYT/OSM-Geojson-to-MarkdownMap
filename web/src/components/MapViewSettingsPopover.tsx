import { useEffect, useRef } from 'react'
import type { Layers, MapViewSettings } from '../mapViewSettings'

interface Props {
  value: MapViewSettings
  onChange: (next: MapViewSettings) => void
  onClose: () => void
}

const LAYER_LABELS: Record<keyof Layers, string> = {
  terrain: 'Terrain',
  edges: 'Proximity links',
  minors: 'Minor features',
  tokens: 'Tokens',
}

/** Map view settings (ADR-0013) — display-only; never touches the pipeline. */
export function MapViewSettingsPopover({ value, onChange, onClose }: Props) {
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const onDown = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) onClose() }
    const onEsc = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onEsc)
    return () => { document.removeEventListener('mousedown', onDown); document.removeEventListener('keydown', onEsc) }
  }, [onClose])

  return (
    <div
      ref={ref}
      role="dialog"
      aria-label="Map view settings"
      className="absolute top-full right-0 z-20 mt-1 w-64 rounded-lg border border-slate-200 bg-white p-2 text-left shadow-lg dark:border-slate-700 dark:bg-slate-800"
    >
      <p className="px-1 pb-1 text-xs font-semibold text-slate-500 dark:text-slate-400">Map view</p>

      <label className="flex cursor-pointer gap-2 rounded px-1 py-1.5 hover:bg-slate-50 dark:hover:bg-slate-700/50">
        <input
          type="checkbox"
          className="mt-0.5 accent-sky-600"
          checked={value.approximateTerrain}
          onChange={(e) => onChange({ ...value, approximateTerrain: e.target.checked })}
        />
        <span>
          <span className="block text-sm text-slate-700 dark:text-slate-200">Approximate terrain</span>
          <span className="block text-xs text-slate-500 dark:text-slate-400">Soft, faint areas — honest about rough extent.</span>
        </span>
      </label>

      <div className="mt-1 border-t border-slate-100 pt-1 dark:border-slate-700">
        <p className="px-1 py-1 text-xs font-semibold text-slate-500 dark:text-slate-400">Layers</p>
        {(Object.keys(LAYER_LABELS) as (keyof Layers)[]).map((k) => (
          <label key={k} className="flex cursor-pointer items-center gap-2 rounded px-1 py-1 text-sm text-slate-700 hover:bg-slate-50 dark:text-slate-200 dark:hover:bg-slate-700/50">
            <input
              type="checkbox"
              className="accent-sky-600"
              checked={value.layers[k]}
              onChange={(e) => onChange({ ...value, layers: { ...value.layers, [k]: e.target.checked } })}
            />
            {LAYER_LABELS[k]}
          </label>
        ))}
      </div>
    </div>
  )
}
