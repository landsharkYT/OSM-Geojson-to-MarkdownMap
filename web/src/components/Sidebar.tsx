import { useMemo } from 'react'
import type { MapModel } from '../types'
import type { Layers } from './MapView'

interface Props {
  model: MapModel
  selected: string | null
  layers: Layers
  onLayersChange: (l: Layers) => void
}

const LAYER_LABELS: Record<keyof Layers, string> = {
  terrain: 'Terrain',
  edges: 'Edges',
  minors: 'Minor features',
  tokens: 'Tokens',
}

export function Sidebar({ model, selected, layers, onLayersChange }: Props) {
  const feature = useMemo(
    () => model.features.find((f) => f.token === selected) ?? null,
    [model, selected],
  )
  const neighbours = useMemo(
    () => (selected ? model.edges.filter((e) => e.fromToken === selected) : []),
    [model, selected],
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
    <aside className="flex h-full w-[26rem] shrink-0 flex-col border-l border-slate-200 dark:border-slate-700">
      {/* layers */}
      <div className="flex flex-wrap gap-3 border-b border-slate-200 p-3 text-sm dark:border-slate-700">
        {(Object.keys(LAYER_LABELS) as (keyof Layers)[]).map((k) => (
          <label key={k} className="flex items-center gap-1.5">
            <input
              type="checkbox"
              checked={layers[k]}
              onChange={(e) => onLayersChange({ ...layers, [k]: e.target.checked })}
            />
            {LAYER_LABELS[k]}
          </label>
        ))}
      </div>

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
          <div className="flex gap-2 text-xs">
            <button onClick={copy} className="rounded bg-slate-200 px-2 py-1 hover:bg-slate-300 dark:bg-slate-700 dark:hover:bg-slate-600">Copy</button>
            <button onClick={download} className="rounded bg-slate-200 px-2 py-1 hover:bg-slate-300 dark:bg-slate-700 dark:hover:bg-slate-600">Download</button>
          </div>
        </div>
        <pre className="min-h-0 flex-1 overflow-auto bg-slate-50 p-3 font-mono text-xs whitespace-pre-wrap dark:bg-slate-900">
          {model.markdown}
        </pre>
      </div>
    </aside>
  )
}
