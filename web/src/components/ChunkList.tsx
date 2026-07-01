import type { Chunk } from '../types'

interface Props {
  chunks: Chunk[]
  onOpen: (slug: string) => void
  onHover: (slug: string | null) => void
}

/**
 * The scene-chunk manifest as a navigable menu (ADR-0016): each row opens that chunk's page.
 * This is the "home" view when chunking is on and no token is being inspected — clicking a map
 * node opens the same chunk pages by another route.
 */
export function ChunkList({ chunks, onOpen, onHover }: Props) {
  return (
    <div
      className="min-h-0 flex-1 overflow-auto bg-slate-50 dark:bg-slate-900"
      onMouseLeave={() => onHover(null)}
    >
      <ul className="divide-y divide-slate-200 dark:divide-slate-700">
        {chunks.map((c) => (
          <li key={c.slug}>
            <button
              onClick={() => onOpen(c.slug)}
              onMouseEnter={() => onHover(c.slug)}
              onFocus={() => onHover(c.slug)}
              onBlur={() => onHover(null)}
              className="block w-full px-3 py-2 text-left hover:bg-slate-100 dark:hover:bg-slate-800"
            >
              <span className="block text-sm font-medium text-sky-700 dark:text-sky-300">{c.name}</span>
              <span className="block text-xs text-slate-500 dark:text-slate-400">
                <span className="font-mono">{c.anchorToken}</span> {c.anchorName}
              </span>
              <span className="mt-0.5 block text-xs text-slate-400 dark:text-slate-500">
                {c.neighbours.length > 0 ? `→ ${c.neighbours.join(', ')}` : 'self-contained'}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}
