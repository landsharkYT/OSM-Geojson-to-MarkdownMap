// Loads the .NET WASM runtime (built by `npm run build:wasm`) once and exposes the
// pipeline. Everything runs in the browser — no upload (ADR-0009).
import type { BuildResult } from './types'

interface WasmExports {
  MarkdownMapWasm: {
    BuildFromOsm: (bytes: Uint8Array) => string
    BuildFromGeoJson: (geojson: string) => string
  }
}

let exportsPromise: Promise<WasmExports> | null = null

async function load(): Promise<WasmExports> {
  const url = new URL('dotnet/_framework/dotnet.js', document.baseURI).href
  const { dotnet } = await import(/* @vite-ignore */ url)
  const { getAssemblyExports, getConfig } = await dotnet.create()
  const config = getConfig()
  return (await getAssemblyExports(config.mainAssemblyName)) as WasmExports
}

function exports(): Promise<WasmExports> {
  return (exportsPromise ??= load())
}

export async function buildFromOsm(bytes: Uint8Array): Promise<BuildResult> {
  const e = await exports()
  return JSON.parse(e.MarkdownMapWasm.BuildFromOsm(bytes)) as BuildResult
}

export async function buildFromGeoJson(geojson: string): Promise<BuildResult> {
  const e = await exports()
  return JSON.parse(e.MarkdownMapWasm.BuildFromGeoJson(geojson)) as BuildResult
}
