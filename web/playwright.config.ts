import { defineConfig, devices } from '@playwright/test'

// E2E: drives the real Explorer in a browser — proving the in-browser .NET WASM load
// + render that unit tests can't cover. Requires `npm run build:wasm` first (so
// public/dotnet exists for the dev server to serve).
export default defineConfig({
  testDir: './e2e',
  timeout: 90_000,
  expect: { timeout: 45_000 },
  fullyParallel: false,
  retries: 0,
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run dev -- --port 5173 --strictPort',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
})
