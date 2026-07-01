// Message protocol between the UI (main thread) and the pipeline Web Worker (ADR-0010).
// The worker runs the .NET WASM runtime so the long synchronous build never blocks the UI.

/** Phase codes. 0 is JS-side (runtime load); 1–3 mirror C# BuildPhase. */
export type Phase = 0 | 1 | 2 | 3
export const Phase = {
  Loading: 0 as Phase,
  Parsing: 1 as Phase,
  Building: 2 as Phase,
  Serializing: 3 as Phase,
}

// main → worker. `options` is the optionsJson (JSON.stringify of MarkdownMapSettings).
export type Request =
  | { type: 'build'; id: number; runtimeUrl: string; options: string; kind: 'osm'; bytes: Uint8Array }
  | { type: 'build'; id: number; runtimeUrl: string; options: string; kind: 'geojson'; text: string }
  // Re-render the worker's cached model with new settings (ADR-0011) — no rebuild.
  | { type: 'rerender'; id: number; runtimeUrl: string; options: string }

// worker → main. A re-render returns the full refreshed MapModel JSON (whole-area markdown plus
// scene-chunks/manifest when Chunking is on, ADR-0016) — not a bare markdown string.
export type Response =
  | { type: 'progress'; id: number; phase: Phase; value: number }
  | { type: 'done'; id: number; json: string }
  | { type: 'rerendered'; id: number; json: string }
  | { type: 'fail'; id: number; message: string }
