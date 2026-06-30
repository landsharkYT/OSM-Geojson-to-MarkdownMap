import { useEffect, useMemo, useRef, useState } from 'react'
import { select } from 'd3-selection'
import { zoom, zoomIdentity, type ZoomTransform } from 'd3-zoom'
import type { MapModel, PromotedFeature } from '../types'
import { computeBounds, makeProjection } from '../projection'

const W = 1000
const H = 750

export interface Layers {
  terrain: boolean
  edges: boolean
  minors: boolean
  tokens: boolean
}

interface Props {
  model: MapModel
  selected: string | null
  onSelect: (token: string | null) => void
  layers: Layers
}

/** Deterministic district colour (stable hue from the name). */
function districtColor(name: string | undefined): string {
  if (!name) return '#94a3b8'
  let h = 0
  for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) % 360
  return `hsl(${h} 70% 55%)`
}

export function MapView({ model, selected, onSelect, layers }: Props) {
  const svgRef = useRef<SVGSVGElement>(null)
  const [t, setT] = useState<ZoomTransform>(zoomIdentity)

  const proj = useMemo(() => makeProjection(computeBounds(model), W, H), [model])
  const byToken = useMemo(() => {
    const m = new Map<string, PromotedFeature>()
    for (const f of model.features) m.set(f.token, f)
    return m
  }, [model])

  useEffect(() => {
    if (!svgRef.current) return
    const sel = select(svgRef.current)
    const z = zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.4, 50])
      .on('zoom', (e) => setT(e.transform))
    sel.call(z)
    return () => { sel.on('.zoom', null) }
  }, [])

  // De-duplicate the bidirectional edges for drawing.
  const drawEdges = useMemo(() => {
    const seen = new Set<string>()
    return model.edges.filter((e) => {
      const key = e.fromToken < e.toToken ? e.fromToken + e.toToken : e.toToken + e.fromToken
      if (seen.has(key)) return false
      seen.add(key)
      return byToken.has(e.fromToken) && byToken.has(e.toToken)
    })
  }, [model, byToken])

  const pt = (lon: number, lat: number) => proj.project(lon, lat).join(',')

  return (
    <svg
      ref={svgRef}
      viewBox={`0 0 ${W} ${H}`}
      className="h-full w-full bg-slate-50 dark:bg-slate-900"
      onClick={() => onSelect(null)}
    >
      <g transform={`translate(${t.x},${t.y}) scale(${t.k})`}>
        {/* terrain (back) */}
        {layers.terrain &&
          model.terrain.flatMap((te, i) =>
            te.parts.map((part, j) => {
              const points = part.map(([lon, lat]) => pt(lon, lat)).join(' ')
              if (te.kind === 'barrier')
                return (
                  <polyline key={`t${i}-${j}`} points={points} fill="none"
                    stroke="#ef4444" strokeWidth={2} strokeDasharray="5 4" vectorEffect="non-scaling-stroke" />
                )
              const fill = te.kind === 'water' ? 'rgba(56,189,248,0.30)' : 'rgba(34,197,94,0.22)'
              const stroke = te.kind === 'water' ? '#0ea5e9' : '#22c55e'
              return (
                <polygon key={`t${i}-${j}`} points={points} fill={fill} stroke={stroke}
                  strokeWidth={1} vectorEffect="non-scaling-stroke" />
              )
            }),
          )}

        {/* proximity edges */}
        {layers.edges &&
          drawEdges.map((e, i) => {
            const a = byToken.get(e.fromToken)!
            const b = byToken.get(e.toToken)!
            const [x1, y1] = proj.project(a.lon, a.lat)
            const [x2, y2] = proj.project(b.lon, b.lat)
            return (
              <line key={`e${i}`} x1={x1} y1={y1} x2={x2} y2={y2}
                stroke={e.crosses ? '#ef4444' : '#cbd5e1'}
                strokeWidth={e.crosses ? 1.5 : 1}
                strokeDasharray={e.crosses ? '4 3' : undefined}
                vectorEffect="non-scaling-stroke" />
            )
          })}

        {/* minor features */}
        {layers.minors &&
          model.minors.map((f, i) => {
            const [x, y] = proj.project(f.lon, f.lat)
            return <circle key={`m${i}`} cx={x} cy={y} r={2} fill="#94a3b8" opacity={0.5} />
          })}

        {/* promoted tokens */}
        {layers.tokens &&
          model.features.map((f) => {
            const [x, y] = proj.project(f.lon, f.lat)
            const isSel = f.token === selected
            return (
              <g key={f.token} className="cursor-pointer"
                onClick={(ev) => { ev.stopPropagation(); onSelect(f.token) }}
                onMouseEnter={() => onSelect(f.token)}>
                <circle cx={x} cy={y} r={isSel ? 6 : 4}
                  fill={districtColor(f.district)}
                  stroke={isSel ? '#111827' : '#fff'} strokeWidth={isSel ? 2 : 1}
                  vectorEffect="non-scaling-stroke" />
                <text x={x + 6} y={y + 3} fontSize={9} fill="#475569"
                  className="select-none dark:fill-slate-300" style={{ pointerEvents: 'none' }}>
                  {f.token}
                </text>
              </g>
            )
          })}
      </g>
    </svg>
  )
}
