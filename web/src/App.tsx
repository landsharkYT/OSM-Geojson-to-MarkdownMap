import { useRef, useState } from 'react'
import { buildFromGeoJson, buildFromOsm, rerender, Phase } from './dotnet'
import { isError, type MapModel } from './types'
import { MapView } from './components/MapView'
import { Sidebar } from './components/Sidebar'
import { ProgressCard, type Progress } from './components/ProgressCard'
import { LegendPopover } from './components/LegendPopover'
import { MapViewSettingsPopover } from './components/MapViewSettingsPopover'
import { loadSettings, saveSettings, type MarkdownMapSettings } from './settings'
import { loadMapView, saveMapView, type MapViewSettings } from './mapViewSettings'

export default function App() {
  const [model, setModel] = useState<MapModel | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [progress, setProgress] = useState<Progress | null>(null)
  const [selected, setSelected] = useState<string | null>(null)
  const [mapView, setMapView] = useState<MapViewSettings>(loadMapView)
  const [settings, setSettings] = useState<MarkdownMapSettings>(loadSettings)
  const [dragging, setDragging] = useState(false)
  const [legendOpen, setLegendOpen] = useState(false)
  const [mapSettingsOpen, setMapSettingsOpen] = useState(false)
  const fileInput = useRef<HTMLInputElement>(null)

  function changeMapView(next: MapViewSettings) {
    setMapView(next)
    saveMapView(next)
  }

  const loading = progress !== null

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
  // in-place — no re-parse, no progress bar.
  async function changeSettings(next: MarkdownMapSettings) {
    setSettings(next)
    saveSettings(next)
    if (!model) return
    try {
      const markdown = await rerender(next)
      setModel((m) => (m ? { ...m, markdown } : m))
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
        <button
          onClick={() => fileInput.current?.click()}
          className="rounded bg-sky-600 px-3 py-1 text-sm text-white hover:bg-sky-700"
        >
          Open .osm / .geojson
        </button>
        <input
          ref={fileInput}
          type="file"
          accept=".osm,.geojson,.json"
          className="hidden"
          onChange={(e) => { const f = e.target.files?.[0]; if (f) load(f); e.target.value = '' }}
        />
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
          <span className="text-xs text-slate-400">Runs entirely in your browser — nothing is uploaded.</span>
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
            <div className="min-w-0 flex-1">
              <MapView
                model={model}
                selected={selected}
                onSelect={setSelected}
                layers={mapView.layers}
                approximateTerrain={mapView.approximateTerrain}
              />
            </div>
            <Sidebar
              model={model}
              selected={selected}
              settings={settings}
              onSettingsChange={changeSettings}
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
              <div className={`rounded-2xl border-2 border-dashed p-12 text-center ${dragging ? 'border-sky-500 bg-sky-50 dark:bg-sky-950' : 'border-slate-300 dark:border-slate-700'}`}>
                <p className="text-lg font-medium">Drop a <code>.osm</code> or <code>.geojson</code> here</p>
                <p className="mt-1 text-sm text-slate-500">or use “Open” above. It’s parsed and rendered locally — your data never leaves this page.</p>
              </div>
            )}
          </div>
        )}
      </main>
    </div>
  )
}
