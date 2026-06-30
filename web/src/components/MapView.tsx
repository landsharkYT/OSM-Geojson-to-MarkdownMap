import { useEffect, useMemo, useRef, useState } from 'react'
import { select } from 'd3-selection'
import { zoom, zoomIdentity, type ZoomTransform } from 'd3-zoom'
import type { MapModel, PromotedFeature } from '../types'
import { computeBounds, makeProjection } from '../projection'
import { visibleLabels, type LabelCandidate } from '../labelLayout'
import type { Layers } from '../mapViewSettings'

const W = 1000
const H = 750

export type { Layers }

interface Props {
  model: MapModel
  selected: string | null
  onSelect: (token: string | null) => void
  layers: Layers
  approximateTerrain: boolean
}

/** Deterministic district colour (stable hue from the name). */
function districtColor(name: string | undefined): string {
  if (!name) return '#94a3b8'
  let h = 0
  for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) % 360
  return `hsl(${h} 70% 55%)`
}

function terrainStyle(kind: string, approximate: boolean) {
  const water = kind === 'water'
  const base = water ? '56,189,248' : '34,197,94'
  const stroke = water ? '#0ea5e9' : '#22c55e'
  return approximate
    ? { fill: `rgba(${base},0.12)`, stroke, strokeOpacity: 0.5, dash: '4 4' }
    : { fill: `rgba(${base},${water ? 0.3 : 0.22})`, stroke, strokeOpacity: 1, dash: undefined }
}

export function MapView({ model, selected, onSelect, layers, approximateTerrain }: Props) {
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

  // Projected base positions (viewBox units, pre-zoom) for promoted features.
  const placed = useMemo(
    () => model.features.map((f) => {
      const [px, py] = proj.project(f.lon, f.lat)
      return { f, px, py }
    }),
    [model, proj],
  )

  // De-duplicate bidirectional edges; keep only those between two promoted features.
  const drawEdges = useMemo(() => {
    const seen = new Set<string>()
    return model.edges.filter((e) => {
      const key = e.fromToken < e.toToken ? e.fromToken + e.toToken : e.toToken + e.fromToken
      if (seen.has(key)) return false
      seen.add(key)
      return byToken.has(e.fromToken) && byToken.has(e.toToken)
    })
  }, [model, byToken])

  // Symbols are fixed screen-size, positioned by applying the zoom transform to coordinates —
  // so zooming spreads clusters apart instead of magnifying the dots (ADR-0013).
  const sx = (px: number) => px * t.k + t.x
  const sy = (py: number) => py * t.k + t.y

  // Which labels fit at the current zoom (collision-avoided, importance-first; pan-invariant
  // so it only recomputes when k or selection changes).
  const shownLabels = useMemo(() => {
    if (!layers.tokens) return new Set<string>()
    const cands: LabelCandidate[] = placed.map(({ f, px, py }) => ({
      token: f.token, x: px * t.k, y: py * t.k, importance: f.importance,
    }))
    return visibleLabels(cands, { force: selected })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [placed, t.k, selected, layers.tokens])

  const isActive = (e: { fromToken: string; toToken: string }) =>
    selected != null && (e.fromToken === selected || e.toToken === selected)

  return (
    <svg
      ref={svgRef}
      viewBox={`0 0 ${W} ${H}`}
      className="h-full w-full bg-slate-50 dark:bg-slate-900"
      onClick={() => onSelect(null)}
    >
      {/* --- geometry layer: scales with zoom --- */}
      <g transform={`translate(${t.x},${t.y}) scale(${t.k})`}>
        {layers.terrain &&
          model.terrain.flatMap((te, i) =>
            te.parts.map((part, j) => {
              const points = part.map(([lon, lat]) => proj.project(lon, lat).join(',')).join(' ')
              if (te.kind === 'barrier')
                return (
                  <polyline key={`t${i}-${j}`} points={points} fill="none"
                    stroke="#ef4444" strokeWidth={2} strokeDasharray="5 4" vectorEffect="non-scaling-stroke" />
                )
              const s = terrainStyle(te.kind, approximateTerrain)
              return (
                <polygon key={`t${i}-${j}`} points={points} fill={s.fill} stroke={s.stroke}
                  strokeOpacity={s.strokeOpacity} strokeDasharray={s.dash} strokeWidth={1}
                  vectorEffect="non-scaling-stroke" />
              )
            }),
          )}

        {/* proximity links — faint background structure; the selected feature's brighten */}
        {layers.edges &&
          drawEdges.map((e, i) => {
            const a = byToken.get(e.fromToken)!
            const b = byToken.get(e.toToken)!
            const [x1, y1] = proj.project(a.lon, a.lat)
            const [x2, y2] = proj.project(b.lon, b.lat)
            const active = isActive(e)
            return (
              <line key={`e${i}`} x1={x1} y1={y1} x2={x2} y2={y2}
                stroke={active ? '#38bdf8' : '#94a3b8'}
                strokeOpacity={active ? 0.9 : 0.2}
                strokeWidth={active ? 1.5 : 1}
                vectorEffect="non-scaling-stroke" />
            )
          })}
      </g>

      {/* --- symbol layer: constant screen size, positioned by the transform --- */}
      <g>
        {layers.minors &&
          model.minors.map((f, i) => {
            const [px, py] = proj.project(f.lon, f.lat)
            return <circle key={`m${i}`} cx={sx(px)} cy={sy(py)} r={2} fill="#94a3b8" opacity={0.5} />
          })}

        {/* crossing markers: a red ✕ on the selected feature's links that cross a barrier
            (distinct from a barrier, which is a red dashed line) */}
        {layers.edges && selected != null &&
          drawEdges
            .filter((e) => isActive(e) && e.crosses)
            .map((e, i) => {
              const a = byToken.get(e.fromToken)!
              const b = byToken.get(e.toToken)!
              const [ax, ay] = proj.project(a.lon, a.lat)
              const [bx, by] = proj.project(b.lon, b.lat)
              return (
                <text key={`x${i}`} x={sx((ax + bx) / 2)} y={sy((ay + by) / 2)}
                  fontSize={13} fill="#ef4444" textAnchor="middle" dominantBaseline="central"
                  style={{ pointerEvents: 'none' }}>✕</text>
              )
            })}

        {layers.tokens &&
          placed.map(({ f, px, py }) => {
            const x = sx(px), y = sy(py)
            const isSel = f.token === selected
            return (
              <g key={f.token} className="cursor-pointer"
                onClick={(ev) => { ev.stopPropagation(); onSelect(f.token) }}
                onMouseEnter={() => onSelect(f.token)}>
                <circle cx={x} cy={y} r={isSel ? 6 : 4}
                  fill={districtColor(f.district)}
                  stroke={isSel ? '#0ea5e9' : '#fff'} strokeWidth={isSel ? 2.5 : 1} />
                {(isSel || shownLabels.has(f.token)) && (
                  <text x={x + 6} y={y + 3} fontSize={9} fill="#475569"
                    className="select-none dark:fill-slate-300" style={{ pointerEvents: 'none' }}>
                    {f.token}
                  </text>
                )}
              </g>
            )
          })}
      </g>
    </svg>
  )
}
