import { Phase } from '../pipeline-protocol'

export interface Progress {
  phase: Phase
  value: number
  /** Total bytes of the input, for the parse percentage (0 if unknown, e.g. geojson). */
  total: number
}

// Maps a raw progress signal to a bar fraction + human label. Phase weights frame the bar;
// only the parse segment is a true % (driven by real stream byte position — ADR-0010).
function readout(p: Progress): { fraction: number; label: string; indeterminate: boolean } {
  switch (p.phase) {
    case Phase.Loading:
      return { fraction: 0.05, label: 'Loading engine…', indeterminate: true }
    case Phase.Parsing: {
      const frac = p.total > 0 ? Math.min(1, p.value / p.total) : 0
      const mb = (n: number) => (n / 1_000_000).toFixed(1)
      const detail = p.total > 0 ? ` — ${mb(p.value)} / ${mb(p.total)} MB` : '…'
      return { fraction: 0.1 + 0.75 * frac, label: `Parsing OSM${detail}`, indeterminate: false }
    }
    case Phase.Building:
      return { fraction: 0.9, label: `Building map — ${p.value.toLocaleString()} features`, indeterminate: false }
    case Phase.Serializing:
      return { fraction: 0.97, label: 'Finalizing…', indeterminate: false }
    default:
      return { fraction: 0, label: 'Working…', indeterminate: true }
  }
}

export function ProgressCard({ progress }: { progress: Progress }) {
  const { fraction, label, indeterminate } = readout(progress)
  return (
    <div className="w-80 max-w-[80vw] rounded-2xl border border-slate-200 bg-white p-5 shadow-lg dark:border-slate-700 dark:bg-slate-800">
      <p className="mb-3 text-center text-sm font-medium text-slate-700 dark:text-slate-200">Generating map</p>
      <div className="h-2 w-full overflow-hidden rounded-full bg-slate-200 dark:bg-slate-700">
        {indeterminate ? (
          <div className="h-full w-1/3 animate-[mdmap-indeterminate_1.1s_ease-in-out_infinite] rounded-full bg-sky-500" />
        ) : (
          <div
            className="h-full rounded-full bg-sky-500 transition-[width] duration-200 ease-out"
            style={{ width: `${Math.round(fraction * 100)}%` }}
          />
        )}
      </div>
      <p className="mt-3 h-4 text-center font-mono text-xs text-slate-500 dark:text-slate-400">{label}</p>
    </div>
  )
}
