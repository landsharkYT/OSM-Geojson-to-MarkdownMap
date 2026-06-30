// Talks to the pipeline Web Worker (ADR-0010). The worker hosts the .NET WASM runtime so
// the synchronous build never freezes the UI; progress flows back via the onProgress callback.
// Settings changes re-render the worker's cached model instead of rebuilding (ADR-0011).
// Everything still runs in the browser — no upload (ADR-0009).
import type { BuildResult } from './types'
import type { MarkdownMapSettings } from './settings'
import { Phase, type Request, type Response } from './pipeline-protocol'

export type ProgressFn = (phase: Phase, value: number) => void

type Job =
  | { kind: 'build'; resolve: (r: BuildResult) => void; reject: (e: Error) => void; onProgress?: ProgressFn }
  | { kind: 'rerender'; resolve: (markdown: string) => void; reject: (e: Error) => void }

let worker: Worker | null = null
let nextId = 1
const pending = new Map<number, Job>()

function ensureWorker(): Worker {
  if (worker) return worker
  worker = new Worker(new URL('./pipeline.worker.ts', import.meta.url), { type: 'module' })
  worker.onmessage = (e: MessageEvent<Response>) => {
    const msg = e.data
    const job = pending.get(msg.id)
    if (!job) return
    if (msg.type === 'progress') {
      if (job.kind === 'build') job.onProgress?.(msg.phase, msg.value)
    } else if (msg.type === 'done' && job.kind === 'build') {
      pending.delete(msg.id)
      job.resolve(normalize(JSON.parse(msg.json) as BuildResult))
    } else if (msg.type === 'rerendered' && job.kind === 'rerender') {
      pending.delete(msg.id)
      job.resolve(msg.markdown)
    } else if (msg.type === 'fail') {
      pending.delete(msg.id)
      job.reject(new Error(msg.message))
    }
  }
  return worker
}

// The runtime lives under the app's base path; resolve it on the main thread (the worker
// has no document) so a sub-path deploy (e.g. GitHub Pages) still finds it.
function runtimeUrl(): string {
  return new URL('dotnet/_framework/dotnet.js', document.baseURI).href
}

function normalize(r: BuildResult): BuildResult {
  if ('error' in r) return r
  // Defensive defaults — empty collections may be absent from the JSON.
  r.features ??= []
  r.minors ??= []
  r.edges ??= []
  r.districts ??= []
  r.terrain ??= []
  r.bounds ??= []
  return r
}

type BuildSpec = { kind: 'osm'; bytes: Uint8Array } | { kind: 'geojson'; text: string }

function build(spec: BuildSpec, settings: MarkdownMapSettings, onProgress?: ProgressFn): Promise<BuildResult> {
  const w = ensureWorker()
  const id = nextId++
  return new Promise<BuildResult>((resolve, reject) => {
    pending.set(id, { kind: 'build', resolve, reject, onProgress })
    const req: Request = { type: 'build', id, runtimeUrl: runtimeUrl(), options: JSON.stringify(settings), ...spec }
    w.postMessage(req)
  })
}

export function buildFromOsm(
  bytes: Uint8Array,
  settings: MarkdownMapSettings,
  onProgress?: ProgressFn,
): Promise<BuildResult> {
  return build({ kind: 'osm', bytes }, settings, onProgress)
}

export function buildFromGeoJson(
  geojson: string,
  settings: MarkdownMapSettings,
  onProgress?: ProgressFn,
): Promise<BuildResult> {
  return build({ kind: 'geojson', text: geojson }, settings, onProgress)
}

/** Re-render the cached model with new settings (ADR-0011). Resolves to fresh markdown. */
export function rerender(settings: MarkdownMapSettings): Promise<string> {
  const w = ensureWorker()
  const id = nextId++
  return new Promise<string>((resolve, reject) => {
    pending.set(id, { kind: 'rerender', resolve, reject })
    const req: Request = { type: 'rerender', id, runtimeUrl: runtimeUrl(), options: JSON.stringify(settings) }
    w.postMessage(req)
  })
}

export { Phase }
