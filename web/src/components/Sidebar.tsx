import { useEffect, useMemo, useRef, useState } from 'react'
import type { MapModel } from '../types'
import type { MarkdownDisplaySettings, MarkdownMapSettings } from '../settings'
import { renderMarkdown } from '../markdown'
import { SettingsPopover } from './SettingsPopover'

interface Props {
  model: MapModel
  selected: string | null
  settings: MarkdownMapSettings
  onSettingsChange: (s: MarkdownMapSettings) => void
  display: MarkdownDisplaySettings
  onDisplayChange: (d: MarkdownDisplaySettings) => void
}

// Drag-to-resize bounds. Max is also clamped to the live window so the map keeps room.
const MIN_WIDTH = 320
const MAX_WIDTH = 900
const DEFAULT_WIDTH = 416 // 26rem
const WIDTH_KEY = 'mdmap.sidebarWidth'

function loadWidth(): number {
  try {
    const v = Number(localStorage.getItem(WIDTH_KEY))
    if (v >= MIN_WIDTH && v <= MAX_WIDTH) return v
  } catch { /* ignore */ }
  return DEFAULT_WIDTH
}

export function Sidebar({ model, selected, settings, onSettingsChange, display, onDisplayChange }: Props) {
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [width, setWidth] = useState<number>(loadWidth)
  const widthRef = useRef(width)
  widthRef.current = width

  // Drag the left edge: moving left grows the panel (clientX decreases → width increases).
  function startResize(e: React.PointerEvent) {
    e.preventDefault()
    const startX = e.clientX
    const startW = widthRef.current
    const prevUserSelect = document.body.style.userSelect
    document.body.style.userSelect = 'none'
    document.body.style.cursor = 'col-resize'
    const onMove = (ev: PointerEvent) => {
      const max = Math.min(MAX_WIDTH, window.innerWidth - 320)
      setWidth(Math.max(MIN_WIDTH, Math.min(max, startW + (startX - ev.clientX))))
    }
    const onUp = () => {
      window.removeEventListener('pointermove', onMove)
      window.removeEventListener('pointerup', onUp)
      document.body.style.userSelect = prevUserSelect
      document.body.style.cursor = ''
      try { localStorage.setItem(WIDTH_KEY, String(widthRef.current)) } catch { /* ignore */ }
    }
    window.addEventListener('pointermove', onMove)
    window.addEventListener('pointerup', onUp)
  }

  // Keep within bounds if the window shrinks below the current width.
  useEffect(() => {
    const onWinResize = () => {
      const max = Math.min(MAX_WIDTH, window.innerWidth - 320)
      setWidth((w) => Math.min(w, Math.max(MIN_WIDTH, max)))
    }
    window.addEventListener('resize', onWinResize)
    return () => window.removeEventListener('resize', onWinResize)
  }, [])

  const feature = useMemo(
    () => model.features.find((f) => f.token === selected) ?? null,
    [model, selected],
  )
  const neighbours = useMemo(
    () => (selected ? model.edges.filter((e) => e.fromToken === selected) : []),
    [model, selected],
  )
  // Only parse+sanitize when the rendered preview is actually shown.
  const renderedHtml = useMemo(
    () => (display.rendered ? renderMarkdown(model.markdown) : ''),
    [display.rendered, model.markdown],
  )

  const copy = () => navigator.clipboard?.writeText(model.markdown)
  const download = () => {
    const blob = new Blob([model.markdown], { type: 'text/markdown' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = 'map.md'
    a.click()
    URL.revokeObjectURL(a.href)
  }

  return (
    <aside
      style={{ width }}
      className="relative flex h-full shrink-0 flex-col border-l border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-900"
    >
      {/* drag the left edge to resize */}
      <div
        onPointerDown={startResize}
        onDoubleClick={() => { setWidth(DEFAULT_WIDTH); try { localStorage.setItem(WIDTH_KEY, String(DEFAULT_WIDTH)) } catch { /* ignore */ } }}
        role="separator"
        aria-orientation="vertical"
        aria-label="Resize sidebar"
        title="Drag to resize · double-click to reset"
        className="absolute left-0 top-0 z-30 h-full w-1.5 -translate-x-1/2 cursor-col-resize touch-none hover:bg-sky-400/40"
      />

      {/* details */}
      <div className="border-b border-slate-200 p-3 text-sm dark:border-slate-700">
        {feature ? (
          <div>
            <div className="font-semibold">
              <span className="text-slate-400">{feature.token}</span> {feature.name}
            </div>
            <div className="text-slate-500">
              {feature.category}
              {feature.street && (feature.streetApprox ? ` · near ${feature.street}` : ` · on ${feature.street}`)}
              {feature.district && ` · ${feature.district}`}
            </div>
            <div className="mt-1 text-xs text-slate-400">
              importance {feature.importance} · {feature.tier}
            </div>
            {neighbours.length > 0 && (
              <ul className="mt-2 space-y-0.5">
                {neighbours.map((e, i) => (
                  <li key={i} className="font-mono text-xs">
                    → {e.toToken} {e.toName} — ~{e.meters}m {e.dir}, {e.bucket}
                    {e.crosses && <span className="text-red-500"> [crosses {e.crosses}]</span>}
                  </li>
                ))}
              </ul>
            )}
          </div>
        ) : (
          <p className="text-slate-400">Hover or click a token to inspect it.</p>
        )}
      </div>

      {/* markdown */}
      <div className="flex min-h-0 flex-1 flex-col">
        <div className="flex items-center justify-between border-b border-slate-200 px-3 py-2 dark:border-slate-700">
          <span className="text-sm font-medium">MarkdownMap</span>
          <div className="relative flex gap-2 text-xs">
            <button
              onClick={() => setSettingsOpen((o) => !o)}
              aria-label="MarkdownMap settings"
              aria-expanded={settingsOpen}
              className="rounded bg-slate-200 px-2 py-1 hover:bg-slate-300 dark:bg-slate-700 dark:hover:bg-slate-600"
            >
              ⚙
            </button>
            <button onClick={copy} className="rounded bg-slate-200 px-2 py-1 hover:bg-slate-300 dark:bg-slate-700 dark:hover:bg-slate-600">Copy</button>
            <button onClick={download} className="rounded bg-slate-200 px-2 py-1 hover:bg-slate-300 dark:bg-slate-700 dark:hover:bg-slate-600">Download</button>
            {settingsOpen && (
              <SettingsPopover
                settings={settings}
                onChange={onSettingsChange}
                display={display}
                onDisplayChange={onDisplayChange}
                onClose={() => setSettingsOpen(false)}
              />
            )}
          </div>
        </div>
        {display.rendered ? (
          <div
            className="markdown-body min-h-0 flex-1 overflow-auto bg-slate-50 p-3 text-sm dark:bg-slate-900"
            dangerouslySetInnerHTML={{ __html: renderedHtml }}
          />
        ) : (
          <pre className="min-h-0 flex-1 overflow-auto bg-slate-50 p-3 font-mono text-xs whitespace-pre-wrap dark:bg-slate-900">
            {model.markdown}
          </pre>
        )}
      </div>
    </aside>
  )
}
