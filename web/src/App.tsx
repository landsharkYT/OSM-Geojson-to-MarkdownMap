import { useRef, useState } from 'react'
import { buildFromGeoJson, buildFromOsm } from './dotnet'
import { isError, type MapModel } from './types'
import { MapView, type Layers } from './components/MapView'
import { Sidebar } from './components/Sidebar'

const DEFAULT_LAYERS: Layers = { terrain: true, edges: true, minors: true, tokens: true }

export default function App() {
  const [model, setModel] = useState<MapModel | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [selected, setSelected] = useState<string | null>(null)
  const [layers, setLayers] = useState<Layers>(DEFAULT_LAYERS)
  const [dragging, setDragging] = useState(false)
  const fileInput = useRef<HTMLInputElement>(null)

  async function load(file: File) {
    setLoading(true)
    setError(null)
    setSelected(null)
    try {
      const name = file.name.toLowerCase()
      let result
      if (name.endsWith('.osm')) {
        result = await buildFromOsm(new Uint8Array(await file.arrayBuffer()))
      } else if (name.endsWith('.geojson') || name.endsWith('.json')) {
        result = await buildFromGeoJson(await file.text())
      } else {
        setError('Unsupported file — open a .osm or .geojson')
        setLoading(false)
        return
      }
      if (isError(result)) setError(result.error)
      else setModel(result)
    } catch (e) {
      setError(String(e))
    }
    setLoading(false)
  }

  return (
    <div
      className="flex h-full flex-col text-slate-800 dark:text-slate-100"
      onDragOver={(e) => { e.preventDefault(); setDragging(true) }}
      onDragLeave={() => setDragging(false)}
      onDrop={(e) => {
        e.preventDefault()
        setDragging(false)
        const f = e.dataTransfer.files[0]
        if (f) load(f)
      }}
    >
      <header className="flex items-center gap-4 border-b border-slate-200 px-4 py-2 dark:border-slate-700">
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
        <span className="ml-auto text-xs text-slate-400">Runs entirely in your browser — nothing is uploaded.</span>
      </header>

      {error && (
        <div className="border-b border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-300">
          {error}
        </div>
      )}

      <main className="relative flex min-h-0 flex-1">
        {loading && (
          <div className="absolute inset-0 z-10 flex items-center justify-center bg-white/70 dark:bg-black/50">
            <span className="animate-pulse text-slate-500">Generating map…</span>
          </div>
        )}

        {model ? (
          <>
            <div className="min-w-0 flex-1">
              <MapView model={model} selected={selected} onSelect={setSelected} layers={layers} />
            </div>
            <Sidebar model={model} selected={selected} layers={layers} onLayersChange={setLayers} />
          </>
        ) : (
          <div className="flex flex-1 items-center justify-center p-8">
            <div className={`rounded-2xl border-2 border-dashed p-12 text-center ${dragging ? 'border-sky-500 bg-sky-50 dark:bg-sky-950' : 'border-slate-300 dark:border-slate-700'}`}>
              <p className="text-lg font-medium">Drop a <code>.osm</code> or <code>.geojson</code> here</p>
              <p className="mt-1 text-sm text-slate-500">or use “Open” above. It’s parsed and rendered locally — your data never leaves this page.</p>
            </div>
          </div>
        )}
      </main>
    </div>
  )
}
