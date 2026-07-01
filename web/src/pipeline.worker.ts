// Runs the .NET WASM pipeline off the main thread (ADR-0010). The runtime is loaded once
// (persistent worker) and reused for every build. The C# pipeline calls back into the
// `progress` module import at real seams; we forward those out as progress messages — they
// reach the (free) main thread even while this worker thread is blocked in the build.
import { Phase, type Request, type Response } from './pipeline-protocol'

interface Exports {
  MarkdownMapWasm: {
    BuildFromOsm: (bytes: Uint8Array, optionsJson: string) => string
    BuildFromGeoJson: (geojson: string, optionsJson: string) => string
    Render: (mapModelJson: string, optionsJson: string) => string
  }
}

let currentId = -1
let runtime: Promise<Exports['MarkdownMapWasm']> | null = null
let lastModelJson: string | null = null // cached for re-render on a settings change (ADR-0011)

function post(msg: Response) {
  ;(self as unknown as Worker).postMessage(msg)
}

async function load(runtimeUrl: string): Promise<Exports['MarkdownMapWasm']> {
  const { dotnet } = await import(/* @vite-ignore */ runtimeUrl)
  const { setModuleImports, getAssemblyExports, getConfig } = await dotnet.create()
  // C#'s [JSImport("report", "progress")] resolves to this. Tag with the in-flight id.
  setModuleImports('progress', {
    report: (phase: number, value: number) =>
      post({ type: 'progress', id: currentId, phase: phase as Phase, value }),
  })
  const config = getConfig()
  const exports = (await getAssemblyExports(config.mainAssemblyName)) as Exports
  return exports.MarkdownMapWasm
}

// IMPORTANT: register via addEventListener, NOT `self.onmessage = …`. The .NET WASM runtime
// reads `self.onmessage` to detect a thread/deputy worker; if it's set, dotnet.create() hangs
// forever waiting for a runtime handshake. addEventListener leaves `self.onmessage` null.
self.addEventListener('message', async (e: MessageEvent<Request>) => {
  const msg = e.data
  if (msg.type !== 'build' && msg.type !== 'rerender') return
  currentId = msg.id
  try {
    if (!runtime) {
      post({ type: 'progress', id: msg.id, phase: Phase.Loading, value: 0 })
      runtime = load(msg.runtimeUrl)
    }
    const api = await runtime

    if (msg.type === 'rerender') {
      if (lastModelJson === null) throw new Error('no map to re-render')
      // Render returns the refreshed MapModel JSON, or {"error":...} JSON on failure (ADR-0016).
      const out = api.Render(lastModelJson, msg.options)
      const err = tryParseError(out)
      if (err) post({ type: 'fail', id: msg.id, message: err })
      else { lastModelJson = out; post({ type: 'rerendered', id: msg.id, json: out }) }
      return
    }

    // Synchronous build — blocks this worker thread, but progress messages already
    // queued via post() flow to the main thread, which keeps animating.
    const json =
      msg.kind === 'osm'
        ? api.BuildFromOsm(msg.bytes, msg.options)
        : api.BuildFromGeoJson(msg.text, msg.options)
    lastModelJson = json
    post({ type: 'done', id: msg.id, json })
  } catch (err) {
    runtime = null // a load failure shouldn't poison every future build
    post({ type: 'fail', id: msg.id, message: String(err) })
  }
})

function tryParseError(out: string): string | null {
  try {
    const o = JSON.parse(out)
    return o && typeof o.error === 'string' ? o.error : null
  } catch {
    return null // not JSON → it's markdown
  }
}
