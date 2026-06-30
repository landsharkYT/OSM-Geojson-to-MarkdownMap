#!/usr/bin/env bash
# Launch the Explorer dev server. Kept as a single entry point so future setup steps
# (extra deps, codegen, env checks) can be added here without changing how you run it.
#
#   ./run.sh            # start the web dev server
#   ./run.sh -- --host  # extra args are passed through to `npm run dev`
#
# Set DOTNET=/path/to/dotnet if your default `dotnet` lacks the wasm-tools workload
# (needed only when the WASM bundle has to be (re)built).
set -euo pipefail

cd "$(dirname "$0")/web"

# 1. node dependencies
if [ ! -d node_modules ]; then
  echo "[run] installing node dependencies…"
  npm install
fi

# 2. .NET WASM bundle (the Explorer loads it from public/dotnet)
if [ ! -f public/dotnet/_framework/dotnet.js ]; then
  echo "[run] building the WASM bundle (npm run build:wasm)…"
  npm run build:wasm
fi

# 3. dev server
echo "[run] starting dev server…"
exec npm run dev -- "$@"
