// Publishes MarkdownMap.Wasm (browser-wasm) and copies its _framework bundle into
// web/public/dotnet/ (git-ignored). The Explorer loads /dotnet/_framework/dotnet.js.
// Set DOTNET=/path/to/dotnet if your default `dotnet` lacks the wasm-tools workload.
import { execSync } from 'node:child_process'
import { cpSync, rmSync, mkdirSync, existsSync } from 'node:fs'

const dotnet = process.env.DOTNET || 'dotnet'
const proj = '../src/MarkdownMap.Wasm'
const framework = `${proj}/bin/Release/net10.0/publish/wwwroot/_framework`
const dest = 'public/dotnet/_framework'

console.log(`publishing ${proj} (browser-wasm)…`)
execSync(`"${dotnet}" publish ${proj} -c Release`, { stdio: 'inherit' })

if (!existsSync(framework)) {
  console.error(`error: framework not found at ${framework}`)
  process.exit(1)
}

rmSync('public/dotnet', { recursive: true, force: true })
mkdirSync('public/dotnet', { recursive: true })
cpSync(framework, dest, { recursive: true })
console.log(`copied _framework → ${dest}`)
