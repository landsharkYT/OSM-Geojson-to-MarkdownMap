import { useRef, useState } from 'react'
import { buildFromGeoJson, buildFromOsm, rerender, Phase } from './dotnet'
import { isError, type MapModel } from './types'
import { MapView } from './components/MapView'
import { Sidebar } from './components/Sidebar'
import { ProgressCard, type Progress } from './components/ProgressCard'
import { LegendPopover } from './components/LegendPopover'
import { MapViewSettingsPopover } from './components/MapViewSettingsPopover'
import { loadSettings, saveSettings, loadDisplay, saveDisplay, type MarkdownMapSettings, type MarkdownDisplaySettings } from './settings'
import { loadMapView, saveMapView, type MapViewSettings } from './mapViewSettings'

export default function App() {
  const [model, setModel] = useState<MapModel | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [progress, setProgress] = useState<Progress | null>(null)
  const [selected, setSelected] = useState<string | null>(null)
  const [highlight, setHighlight] = useState<string[] | null>(null)
  const [mapView, setMapView] = useState<MapViewSettings>(loadMapView)
  const [settings, setSettings] = useState<MarkdownMapSettings>(loadSettings)
  const [display, setDisplay] = useState<MarkdownDisplaySettings>(loadDisplay)
  const [dragging, setDragging] = useState(false)
  const [legendOpen, setLegendOpen] = useState(false)
  const [mapSettingsOpen, setMapSettingsOpen] = useState(false)
  const [minorsWarningDismissed, setMinorsWarningDismissed] = useState(false)
  const fileInput = useRef<HTMLInputElement>(null)

  function changeMapView(next: MapViewSettings) {
    // Re-arm the warning each time minors are turned off, so re-enabling shows it again.
    if (!next.layers.minors) setMinorsWarningDismissed(false)
    setMapView(next)
    saveMapView(next)
  }

  // Display-only (sidebar render mode): persist, no re-render — the markdown text is unchanged.
  function changeDisplay(next: MarkdownDisplaySettings) {
    setDisplay(next)
    saveDisplay(next)
  }

  const loading = progress !== null

  // Clear the loaded map and return to the drop zone to parse something else.
  function restart() {
    if (loading) return
    setModel(null)
    setSelected(null)
    setHighlight(null)
    setError(null)
  }

  async function load(file: File) {
    if (loading) return // one build at a time (persistent worker, ADR-0010)
    setError(null)
    setSelected(null)
    try {
      const name = file.name.toLowerCase()
      let result
      if (name.endsWith('.osm')) {
        const bytes = new Uint8Array(await file.arrayBuffer())
        setProgress({ phase: Phase.Loading, value: 0, total: bytes.length })
        result = await buildFromOsm(bytes, settings, (phase, value) => setProgress({ phase, value, total: bytes.length }))
      } else if (name.endsWith('.geojson') || name.endsWith('.json')) {
        const text = await file.text()
        setProgress({ phase: Phase.Loading, value: 0, total: 0 })
        result = await buildFromGeoJson(text, settings, (phase, value) => setProgress({ phase, value, total: 0 }))
      } else {
        setError('Unsupported file — open a .osm or .geojson')
        return
      }
      if (isError(result)) setError(result.error)
      else setModel(result)
    } catch (e) {
      setError(String(e))
    } finally {
      setProgress(null)
    }
  }

  // A settings change is render-only (ADR-0011): persist it and re-render the cached model
  // in-place — no re-parse, no progress bar. The refreshed model carries new markdown and, when
  // chunking is on, the scene-chunk set + manifest (ADR-0016).
  async function changeSettings(next: MarkdownMapSettings) {
    setSettings(next)
    saveSettings(next)
    if (!model) return
    try {
      const result = await rerender(next)
      if (isError(result)) setError(result.error)
      else setModel(result)
    } catch (e) {
      setError(String(e))
    }
  }

  return (
    <div
      className="flex h-full flex-col bg-white text-slate-800 dark:bg-slate-950 dark:text-slate-100"
      onDragOver={(e) => { e.preventDefault(); setDragging(true) }}
      onDragLeave={() => setDragging(false)}
      onDrop={(e) => {
        e.preventDefault()
        setDragging(false)
        const f = e.dataTransfer.files[0]
        if (f) load(f)
      }}
    >
      <header className="flex items-center gap-4 border-b border-slate-200 bg-white px-4 py-2 dark:border-slate-700 dark:bg-slate-900">
        <h1 className="text-lg font-semibold">OSM → MarkdownMap Explorer</h1>
        <input
          ref={fileInput}
          type="file"
          accept=".osm,.geojson,.json"
          className="hidden"
          onChange={(e) => { const f = e.target.files?.[0]; if (f) load(f); e.target.value = '' }}
        />
        {model && (
          <button
            onClick={restart}
            disabled={loading}
            title="Parse a different file"
            className="rounded bg-sky-600 px-3 py-1 text-sm text-white hover:bg-sky-700 disabled:opacity-50"
          >
            ↻ Restart
          </button>
        )}
        {model && <span className="text-sm text-slate-500">{model.title}</span>}

        <div className="ml-auto flex items-center gap-3">
          {model && (
            <div className="relative flex items-center gap-1">
              <button
                onClick={() => { setLegendOpen((o) => !o); setMapSettingsOpen(false) }}
                aria-label="Map legend"
                aria-expanded={legendOpen}
                className="flex h-7 w-7 items-center justify-center rounded-full border border-slate-300 text-sm font-semibold text-slate-600 hover:bg-slate-100 dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                ⓘ
              </button>
              <button
                onClick={() => { setMapSettingsOpen((o) => !o); setLegendOpen(false) }}
                aria-label="Map view settings"
                aria-expanded={mapSettingsOpen}
                className="flex h-7 w-7 items-center justify-center rounded-full border border-slate-300 text-sm text-slate-600 hover:bg-slate-100 dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                ⚙
              </button>
              {legendOpen && <LegendPopover onClose={() => setLegendOpen(false)} />}
              {mapSettingsOpen && (
                <MapViewSettingsPopover value={mapView} onChange={changeMapView} onClose={() => setMapSettingsOpen(false)} />
              )}
            </div>
          )}
        </div>
      </header>

      {error && (
        <div className="border-b border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-300">
          {error}
        </div>
      )}

      <main className="relative flex min-h-0 flex-1">
        {model ? (
          <>
            <div className="relative min-w-0 flex-1">
              {mapView.layers.minors && !minorsWarningDismissed && (
                <div
                  role="status"
                  className="pointer-events-none absolute inset-x-0 top-0 z-10 flex justify-center p-2"
                >
                  <span className="pointer-events-auto flex items-center gap-2 rounded-md border border-amber-300 bg-amber-50/95 py-1.5 pl-3 pr-1.5 text-xs font-medium text-amber-800 shadow-sm dark:border-amber-700 dark:bg-amber-950/90 dark:text-amber-200">
                    <span>⚠ You’re showing minor features the AI can’t see. This map no longer matches the markdown.</span>
                    <button
                      type="button"
                      aria-label="Dismiss warning"
                      onClick={() => setMinorsWarningDismissed(true)}
                      className="rounded p-0.5 text-amber-600 hover:bg-amber-200/60 hover:text-amber-900 dark:text-amber-400 dark:hover:bg-amber-800/60 dark:hover:text-amber-100"
                    >
                      <svg viewBox="0 0 20 20" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round">
                        <path d="M5 5l10 10M15 5L5 15" />
                      </svg>
                    </button>
                  </span>
                </div>
              )}
              <MapView
                model={model}
                selected={selected}
                onSelect={setSelected}
                layers={mapView.layers}
                detailedTerrain={mapView.detailedTerrain}
                highlight={highlight}
              />
            </div>
            <Sidebar
              model={model}
              selected={selected}
              settings={settings}
              onSettingsChange={changeSettings}
              display={display}
              onDisplayChange={changeDisplay}
              onClearSelection={() => setSelected(null)}
              onHighlight={setHighlight}
            />
            {/* Re-import over an existing map: dimmed overlay keeps context. */}
            {progress && (
              <div className="absolute inset-0 z-10 flex items-center justify-center bg-white/60 dark:bg-black/50">
                <ProgressCard progress={progress} />
              </div>
            )}
          </>
        ) : (
          // Empty state: progress card *replaces* the drop zone (never both → no overlap).
          <div className="flex flex-1 items-center justify-center p-8">
            {progress ? (
              <ProgressCard progress={progress} />
            ) : (
              <button
                type="button"
                onClick={() => fileInput.current?.click()}
                className={`cursor-pointer rounded-2xl border-2 border-dashed p-12 text-center transition-colors ${dragging ? 'border-sky-500 bg-sky-50 dark:bg-sky-950' : 'border-slate-300 hover:border-sky-500 hover:bg-slate-50 dark:border-slate-700 dark:hover:border-sky-500 dark:hover:bg-slate-800/50'}`}
              >
                <p className="text-lg font-medium">Drop a <code>.osm</code> or <code>.geojson</code> here</p>
                <p className="mt-1 text-sm text-slate-500">or click to browse.</p>
              </button>
            )}
          </div>
        )}
      </main>
    </div>
  )
}
