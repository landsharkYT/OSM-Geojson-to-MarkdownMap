# 10. The Explorer runs the pipeline in a Web Worker with instrumented progress

Date: 2026-06-29
Status: Accepted

Amends ADR-0009, which left where on the page's threads the WASM runs unstated; it ran on
the main thread.

## Context

The pipeline call (`BuildFromOsm`/`BuildFromGeoJson`) is a single synchronous `[JSExport]`
invocation. For a ~12 MB `.osm` extract it blocks its thread for ~3.7 s (the OSM XML parse
is ~90% of that). Run on the main thread, as the first Explorer cut did, the browser can't
repaint during the call, so any spinner, progress bar, or status text is frozen until the
work finishes and then snaps to done. A "progress bar with rotating text showing what it's
doing" is therefore impossible on the main thread.

The worker thread that runs the synchronous build is also blocked for the duration, so a
worker alone isn't enough: it can't `postMessage` mid-call unless the C# pipeline itself
calls back out at real seams.

## Decision

The Explorer runs the .NET WASM runtime and the pipeline in a persistent Web Worker. The
pipeline is instrumented to emit structured progress signals which the worker forwards to
the main thread; the main thread, now free, renders a live, determinate progress bar with
rotating phase text.

- Persistent worker: spawned once, runtime loaded once, reused for every import (the ~1–2 s
  runtime load is the dominant cost on small files and is paid once). While a build is in
  flight, new imports are ignored.
- No cancellation in v1: the build is synchronous, so the only way to stop it is
  `worker.terminate()`, which destroys the loaded runtime and fights the persistent-worker
  win. Deferred; if needed later it's kill-and-respawn.
- Structured signals, not display strings: the pipeline reports what happened (a `Phase`
  plus optional `count`), and the Explorer decides how to say it. This preserves the
  UI-agnostic library boundary (the pipeline also feeds the CLI). A small `[JSImport]`
  progress callback is threaded through `MapBuilder` → `OsmNormalizer` / `MapGenerator`.
- Determinate where it counts: the parse phase reports real `stream.Position /
  stream.Length`, so the bar moves truthfully through the segment where the time actually
  goes. Phase weights (load / parse / build+render) frame it; only the parse segment is a
  true percentage. The GeoJSON path has no long parse and flashes through.

## Consequences

- A genuine telemetry readout, not a spinner with subtitles: the bar reflects real byte
  progress and the text reflects real stage completions.
- The UI stays responsive during large imports. This also structurally fixes the
  drop-zone/overlay overlap (the progress card replaces the drop zone while loading and
  overlays the map on re-import).
- Costs: a worker bootstrap plus message protocol; `dotnet.ts` is refactored to talk to the
  worker; the pipeline gains an optional progress callback parameter (no-op for the CLI).
  Marshaling still goes via JSON (ADR-0009).
- Privacy is unchanged: the worker is same-origin, and data still never leaves the browser.
