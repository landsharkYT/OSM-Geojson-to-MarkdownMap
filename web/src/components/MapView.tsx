import { useEffect, useMemo, useRef, useState } from 'react'
import { select } from 'd3-selection'
import { zoom, zoomIdentity, type ZoomTransform } from 'd3-zoom'
import type { MapModel, PromotedFeature } from '../types'
import { computeBounds, makeProjection } from '../projection'
import { visibleLabels, type LabelCandidate } from '../labelLayout'
import { convexHull, type Point } from '../hull'
import type { Layers } from '../mapViewSettings'
import { terrainShownInMarkdown } from '../terrainVisibility'

const W = 1000
const H = 750

export type { Layers }

interface Props {
  model: MapModel
  selected: string | null
  onSelect: (token: string | null) => void
  layers: Layers
  detailedTerrain: boolean
  // Scene-chunk highlight (ADR-0016): tokens of the active/hovered chunk. When set, those nodes
  // get a halo and everything else dims, so the chunk reads as a region on the map.
  highlight?: string[] | null
}

const highlightColor = '#f59e0b' // amber halo — distinct from selection blue and district hues

/** Deterministic district colour (stable hue from the name). */
function districtColor(name: string | undefined): string {
  if (!name) return '#94a3b8'
  let h = 0
  for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) % 360
  return `hsl(${h} 70% 55%)`
}

const waterColor = '#0ea5e9'
const parkColor = '#22c55e'
const terrainStroke = (kind: string) => (kind === 'water' ? waterColor : parkColor)

// Real assembled rings: moderate fill, solid outline (ADR-0014).
function realAreaStyle(kind: string) {
  const water = kind === 'water'
  return { fill: water ? 'rgba(56,189,248,0.18)' : 'rgba(34,197,94,0.14)', stroke: terrainStroke(kind) }
}
// Convex-hull extent blob (Detailed terrain off): soft fill, dashed outline → reads as approximate.
function hullAreaStyle(kind: string) {
  const water = kind === 'water'
  return { fill: water ? 'rgba(56,189,248,0.12)' : 'rgba(34,197,94,0.10)', stroke: terrainStroke(kind) }
}

export function MapView({ model, selected, onSelect, layers, detailedTerrain, highlight }: Props) {
  const svgRef = useRef<SVGSVGElement>(null)
  const [t, setT] = useState<ZoomTransform>(zoomIdentity)
  // Minor-feature hover (its own affordance, separate from the token/sidebar selection): a minor
  // has no token and never drives the sidebar — hovering just shows a tiny "what is this" tooltip.
  const [hoverMinor, setHoverMinor] = useState<number | null>(null)

  const highlightSet = useMemo(
    () => (highlight && highlight.length ? new Set(highlight) : null),
    [highlight],
  )

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
          // Only terrain the markdown lists — sub-threshold pocket parks are omitted from the text,
          // so the map omits them too (honest about what the AI sees). Mirrors the Generator filter.
          model.terrain.filter(terrainShownInMarkdown).flatMap((te, i) => {
            // Barriers are always lines, in both modes.
            if (te.kind === 'barrier')
              return te.parts.map((part, j) => (
                <polyline key={`t${i}-${j}`} points={part.map(([lon, lat]) => proj.project(lon, lat).join(',')).join(' ')}
                  fill="none" stroke="#ef4444" strokeWidth={2} strokeDasharray="5 4" vectorEffect="non-scaling-stroke" />
              ))

            // Detailed off: collapse the whole area to its convex-hull extent blob (ADR-0008 look).
            if (!detailedTerrain) {
              const all: Point[] = te.parts.flatMap((part) => part.map(([lon, lat]) => proj.project(lon, lat)))
              const hull = convexHull(all)
              if (hull.length < 3) return []
              const s = hullAreaStyle(te.kind)
              return [
                <polygon key={`h${i}`} points={hull.map((p) => p.join(',')).join(' ')} fill={s.fill}
                  stroke={s.stroke} strokeOpacity={0.5} strokeDasharray="4 4" strokeWidth={1}
                  vectorEffect="non-scaling-stroke" />,
              ]
            }

            // Detailed on: real shapes — assembled rings filled, clipped shorelines as lines.
            return te.parts.map((part, j) => {
              const points = part.map(([lon, lat]) => proj.project(lon, lat).join(',')).join(' ')
              if (te.geometryType === 'LineString')
                return (
                  <polyline key={`t${i}-${j}`} points={points} fill="none" stroke={terrainStroke(te.kind)}
                    strokeWidth={2} strokeOpacity={0.8} vectorEffect="non-scaling-stroke" />
                )
              const s = realAreaStyle(te.kind)
              return (
                <polygon key={`t${i}-${j}`} points={points} fill={s.fill} stroke={s.stroke}
                  strokeWidth={1} vectorEffect="non-scaling-stroke" />
              )
            })
          })}

        {/* proximity links — faint background structure; the selected feature's brighten */}
        {layers.edges &&
          drawEdges.map((e, i) => {
            const a = byToken.get(e.fromToken)!
            const b = byToken.get(e.toToken)!
            const [x1, y1] = proj.project(a.lon, a.lat)
            const [x2, y2] = proj.project(b.lon, b.lat)
            const active = isActive(e)
            // When a chunk is highlighted, only its internal links keep their weight; the rest fade.
            const inChunk = highlightSet == null || (highlightSet.has(e.fromToken) && highlightSet.has(e.toToken))
            const opacity = active ? 0.9 : inChunk ? 0.2 : 0.04
            return (
              <line key={`e${i}`} x1={x1} y1={y1} x2={x2} y2={y2}
                stroke={active ? '#38bdf8' : '#94a3b8'}
                strokeOpacity={opacity}
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
            const x = sx(px), y = sy(py)
            const hov = hoverMinor === i
            return (
              <g key={`m${i}`}>
                <circle cx={x} cy={y} r={hov ? 3.5 : 2} fill={hov ? '#cbd5e1' : '#94a3b8'}
                  opacity={highlightSet ? 0.15 : hov ? 0.95 : 0.5} />
                {/* transparent hit target — the 2px dot is too small to land on directly */}
                <circle cx={x} cy={y} r={5} fill="transparent" className="cursor-help"
                  onMouseEnter={() => setHoverMinor(i)}
                  onMouseLeave={() => setHoverMinor((h) => (h === i ? null : h))} />
              </g>
            )
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
            const isHi = highlightSet?.has(f.token) ?? false
            const faded = highlightSet != null && !isHi
            return (
              <g key={f.token} className="cursor-pointer" opacity={faded ? 0.2 : 1}
                onClick={(ev) => { ev.stopPropagation(); onSelect(f.token) }}
                onMouseEnter={() => onSelect(f.token)}>
                {isHi && (
                  <circle cx={x} cy={y} r={9} fill="none" stroke={highlightColor} strokeWidth={2} strokeOpacity={0.9} />
                )}
                <circle cx={x} cy={y} r={isSel ? 6 : 4}
                  fill={districtColor(f.district)}
                  stroke={isSel ? '#0ea5e9' : '#fff'} strokeWidth={isSel ? 2.5 : 1} />
                {(isSel || isHi || shownLabels.has(f.token)) && (
                  <text x={x + 6} y={y + 3} fontSize={9} fill="#475569"
                    className="select-none dark:fill-slate-300" style={{ pointerEvents: 'none' }}>
                    {f.token}
                  </text>
                )}
              </g>
            )
          })}

        {/* minor-feature hover tooltip — drawn last so it sits on top; just says what it is */}
        {layers.minors && hoverMinor != null && model.minors[hoverMinor] && (() => {
          const f = model.minors[hoverMinor]
          const [px, py] = proj.project(f.lon, f.lat)
          const x = sx(px), y = sy(py)
          const w = Math.max(f.name.length, f.category.length) * 6 + 12
          const flip = x + 10 + w > W // keep it on-canvas near the right edge
          const tx = flip ? x - 10 - w : x + 10
          return (
            <g pointerEvents="none" transform={`translate(${tx},${y})`}>
              <rect x={0} y={-16} width={w} height={30} rx={4} fill="rgba(15,23,42,0.92)" stroke="#334155" />
              <text x={6} y={-3} fontSize={10} fill="#e2e8f0">{f.name}</text>
              <text x={6} y={9} fontSize={9} fill="#94a3b8">{f.category}</text>
            </g>
          )
        })()}
      </g>
    </svg>
  )
}
