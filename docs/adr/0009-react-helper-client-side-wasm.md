# 9. The React helper runs the pipeline client-side via .NET WASM

Date: 2026-06-27
Status: Accepted

## Context

The deferred "React helper" is a browser app that must use the C# pipeline (Normalizer +
Generator). A browser can't call C# directly, so the options are:

1. Server API (ASP.NET): React uploads a `.osm`, the server runs the pipeline.
2. Rewrite Stage 2 (and somehow Stage 1) in TypeScript.
3. Compile the C# pipeline to WebAssembly and call it from React/TS in-browser.

A `.osm` extract is real-location data, the same thing the whole project has worked to
keep private. Where the parsing happens is therefore a privacy decision.

## Decision

The pipeline is compiled to a .NET WebAssembly module (`[JSExport]`); the React/TS UI calls
it client-side. No server. The `.osm`/GeoJSON never leaves the browser.

## Consequences

- Privacy by construction: input data is never uploaded, so there's nothing to leak.
- Static-site deployable (e.g. GitHub Pages); no backend/hosting to run.
- Instant local re-runs: re-generation on interaction has no network round-trip.
- Single source of truth: reuses the tested C# pipeline instead of a parallel TS
  reimplementation that would drift.
- The Generator is refactored to expose a structured MapModel; markdown and the visual
  explorer are both views over it.
- Costs and risks: WASM feasibility of OsmSharp + NetTopologySuite is unproven in browser,
  so de-risk with a spike first. The .NET WASM runtime adds an MB-scale one-time download,
  and C#↔JS marshaling goes via JSON. If the spike fails, fall back to a localhost-only
  backend that keeps data on the user's machine.
