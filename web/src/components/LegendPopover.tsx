import { useEffect, useRef, type ReactNode } from 'react'

/** Explorer legend (ADR-0013) — names the SVG symbology and the two non-obvious truths:
 *  proximity links are not streets, and terrain areas are approximate extent. */
export function LegendPopover({ onClose }: { onClose: () => void }) {
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
      aria-label="Map legend"
      className="absolute top-full right-0 z-20 mt-1 w-80 rounded-lg border border-slate-200 bg-white p-3 text-left shadow-lg dark:border-slate-700 dark:bg-slate-800"
    >
      <p className="px-1 pb-1.5 text-xs font-semibold text-slate-500 dark:text-slate-400">Legend</p>
      <ul className="space-y-1.5 text-xs text-slate-600 dark:text-slate-300">
        <Row swatch={<Dot fill="#f59e0b" />}>feature — colour = its district</Row>
        <Row swatch={<Dot fill="#f59e0b" ring />}>selected feature</Row>
        <Row swatch={<Dot fill="#94a3b8" small />}>minor feature (counted in the map text)</Row>
        <Row swatch={<Line />}>proximity link — straight-line closeness, <b>not a street</b></Row>
        <Row swatch={<Cross />}>that link <b>crosses a barrier</b> (not directly walkable)</Row>
        <Row swatch={<Line dashed color="#ef4444" />}>barrier — freeway/rail/river (impassable)</Row>
        <Row swatch={<Area fill="rgba(56,189,248,0.5)" stroke="#0ea5e9" />}><b>approximate</b> water extent</Row>
        <Row swatch={<Area fill="rgba(34,197,94,0.4)" stroke="#22c55e" />}><b>approximate</b> park / green space</Row>
      </ul>
      <p className="mt-2 border-t border-slate-100 px-1 pt-2 text-[11px] text-slate-400 dark:border-slate-700">
        Positions are true geographic (north up); terrain is approximate.
      </p>
    </div>
  )
}

function Row({ swatch, children }: { swatch: ReactNode; children: ReactNode }) {
  return (
    <li className="flex items-center gap-2">
      <span className="flex h-4 w-6 shrink-0 items-center justify-center">{swatch}</span>
      <span>{children}</span>
    </li>
  )
}

function Dot({ fill, ring, small }: { fill: string; ring?: boolean; small?: boolean }) {
  return (
    <svg width="16" height="12" viewBox="0 0 16 12">
      <circle cx="8" cy="6" r={small ? 2 : 4} fill={fill}
        stroke={ring ? '#0ea5e9' : '#fff'} strokeWidth={ring ? 2 : 1} />
    </svg>
  )
}
function Line({ dashed, color = '#94a3b8' }: { dashed?: boolean; color?: string }) {
  return (
    <svg width="24" height="12" viewBox="0 0 24 12">
      <line x1="1" y1="6" x2="23" y2="6" stroke={color} strokeWidth="2"
        strokeDasharray={dashed ? '4 3' : undefined} />
    </svg>
  )
}
function Cross() {
  return <svg width="16" height="12" viewBox="0 0 16 12"><text x="8" y="6" fontSize="11" fill="#ef4444" textAnchor="middle" dominantBaseline="central">✕</text></svg>
}
function Area({ fill, stroke }: { fill: string; stroke: string }) {
  return <svg width="20" height="12" viewBox="0 0 20 12"><rect x="1" y="1" width="18" height="10" rx="2" fill={fill} stroke={stroke} strokeDasharray="3 2" /></svg>
}
