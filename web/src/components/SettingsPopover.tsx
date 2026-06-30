import { useEffect, useRef } from 'react'
import type { MarkdownMapSettings } from '../settings'

interface Props {
  settings: MarkdownMapSettings
  onChange: (next: MarkdownMapSettings) => void
  onClose: () => void
}

const ITEMS: { key: keyof MarkdownMapSettings; label: string; help: string }[] = [
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

/** MarkdownMap generation settings (ADR-0011). Render-only knobs; changes re-render live. */
export function SettingsPopover({ settings, onChange, onClose }: Props) {
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
    </div>
  )
}
