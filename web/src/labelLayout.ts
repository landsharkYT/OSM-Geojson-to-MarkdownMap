// Importance-prioritized label decluttering (ADR-0013). Pure + unit-tested: given each
// feature's screen position and importance, return which labels fit without overlapping,
// placing the most important first (plus a forced one — the selected/hovered feature).
// Positions are pan-invariant (px*k), so this only recomputes when zoom (k) changes.

export interface LabelCandidate {
  token: string
  x: number // screen x of the dot (pan-invariant: projectedX * k)
  y: number // screen y of the dot
  importance: number
}

export interface LabelLayoutOptions {
  /** Always-shown label (selected/hovered), even if it overlaps. */
  force?: string | null
  charW?: number // px per character at the label font size
  lineH?: number // label box height in px
  gapX?: number // dot→label horizontal offset
}

interface Box {
  x: number
  y: number
  w: number
  h: number
}

function boxFor(c: LabelCandidate, charW: number, lineH: number, gapX: number): Box {
  return { x: c.x + gapX, y: c.y - lineH / 2, w: c.token.length * charW, h: lineH }
}

function overlaps(a: Box, b: Box): boolean {
  return a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y
}

/** Tokens whose labels should be drawn at the current zoom. */
export function visibleLabels(cands: LabelCandidate[], opts: LabelLayoutOptions = {}): Set<string> {
  const charW = opts.charW ?? 5.4
  const lineH = opts.lineH ?? 11
  const gapX = opts.gapX ?? 6

  // Force first, then most-important first; ties broken by token for determinism.
  const ordered = [...cands].sort((a, b) => {
    if (a.token === opts.force) return -1
    if (b.token === opts.force) return 1
    return b.importance - a.importance || (a.token < b.token ? -1 : 1)
  })

  const placed: Box[] = []
  const shown = new Set<string>()
  for (const c of ordered) {
    const box = boxFor(c, charW, lineH, gapX)
    const forced = c.token === opts.force
    if (forced || !placed.some((p) => overlaps(p, box))) {
      placed.push(box)
      shown.add(c.token)
    }
  }
  return shown
}
