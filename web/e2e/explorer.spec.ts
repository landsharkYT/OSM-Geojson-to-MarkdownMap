import { test, expect } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { mkdtemp, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import path from 'node:path'

const fixture = path.join(path.dirname(fileURLToPath(import.meta.url)), 'fixtures', 'rivertown.geojson')

// Synthetic .osm written to a temp file at runtime — `*.osm` is gitignored (privacy) and
// tests stay location-agnostic, so we never commit one. Exercises the Stage-1 OSM XML
// path (OsmSharp/XmlSerializer) end-to-end through WASM, which the geojson fixtures skip.
const SYNTHETIC_OSM = `<?xml version="1.0" encoding="UTF-8"?>
<osm version="0.6" generator="markdownmap-test">
 <bounds minlat="50.0000" minlon="10.0000" maxlat="50.0100" maxlon="10.0100"/>
 <node id="1" lat="50.0050" lon="10.0050"><tag k="place" v="neighbourhood"/><tag k="name" v="Old Town"/></node>
 <node id="2" lat="50.0060" lon="10.0040"><tag k="tourism" v="viewpoint"/><tag k="name" v="Founders Lookout"/></node>
 <node id="3" lat="50.0040" lon="10.0060"><tag k="amenity" v="cafe"/><tag k="name" v="Anchor Cafe"/></node>
 <node id="4" lat="50.0030" lon="10.0030"><tag k="amenity" v="school"/><tag k="name" v="Riverside School"/></node>
 <node id="5" lat="50.0090" lon="10.0090"><tag k="amenity" v="place_of_worship"/></node>
</osm>
`

test('loads a .geojson in-browser via WASM and renders map + markdown', async ({ page }) => {
  const errors: string[] = []
  page.on('pageerror', (e) => errors.push(String(e)))

  await page.goto('/')
  await expect(page.getByText(/Drop a/)).toBeVisible()

  // Feed the file → the .NET WASM runtime loads and generates entirely in-browser.
  await page.setInputFiles('input[type="file"]', fixture)

  // A token label appears in the SVG once the model renders.
  await expect(page.locator('main svg').getByText('[01]', { exact: true })).toBeVisible()

  // The MarkdownMap panel is populated, including computed sections.
  const md = page.locator('pre')
  await expect(md).toContainText('# MARKDOWNMAP — Old Town, Rivertown')
  await expect(md).toContainText('## Districts')
  await expect(md).toContainText('[crosses Route 9]')

  // Terrain title shows the water body (proves relation/terrain rendering reached JS).
  await expect(page.locator('header')).toContainText('Old Town, Rivertown')

  expect(errors, errors.join('\n')).toHaveLength(0)
})

test('parses a .osm in-browser (Stage 1 OSM XML → WASM) and renders', async ({ page }) => {
  const errors: string[] = []
  page.on('pageerror', (e) => errors.push(String(e)))

  const dir = await mkdtemp(path.join(tmpdir(), 'mdmap-osm-'))
  const osmPath = path.join(dir, 'synthetic.osm')
  await writeFile(osmPath, SYNTHETIC_OSM)

  await page.goto('/')
  await page.setInputFiles('input[type="file"]', osmPath)

  // A promoted token must render — proves OsmSharp actually parsed elements (the bug
  // returned an empty model with no error when OsmSharp was trimmed away).
  await expect(page.locator('main svg g.cursor-pointer circle').first()).toBeVisible()

  const md = page.locator('pre')
  await expect(md).toContainText('# MARKDOWNMAP — Old Town')
  await expect(page.locator('header')).toContainText('Old Town')

  // The unnamed place_of_worship (landmark tier) promotes with a humanized category label,
  // lowercase, and no redundant "(category)" (ADR-0012).
  await expect(md).toContainText('place of worship')
  await expect(md).not.toContainText('unnamed')

  expect(errors, errors.join('\n')).toHaveLength(0)
})

test('shows a progress card while building, then clears it', async ({ page }) => {
  await page.goto('/')
  await page.setInputFiles('input[type="file"]', fixture)

  // The build runs in a worker; the card is visible during the (cold) runtime load.
  const card = page.getByText('Generating map', { exact: true })
  await expect(card).toBeVisible()

  // Once the map renders, the card is gone (drop-zone slot is replaced, never overlapped).
  await expect(page.locator('main svg').getByText('[01]', { exact: true })).toBeVisible()
  await expect(card).toBeHidden()
})

test('MarkdownMap settings re-render the markdown live (no rebuild)', async ({ page }) => {
  await page.goto('/')
  await page.setInputFiles('input[type="file"]', fixture)
  const md = page.locator('pre')
  await expect(md).toContainText('DIRECTIVE PREAMBLE')

  // Open settings, turn off the directive preamble.
  await page.getByRole('button', { name: 'MarkdownMap settings' }).click()
  await page.locator('label', { hasText: 'Directive preamble' }).locator('input').uncheck()

  // The markdown updates in place — no progress card, the map stays mounted.
  await expect(md).not.toContainText('DIRECTIVE PREAMBLE')
  await expect(page.getByText('Generating map', { exact: true })).toBeHidden()
  await expect(page.locator('main svg').getByText('[01]', { exact: true })).toBeVisible()

  // Turning it back on restores the block (proves it's a live re-render, not a one-way strip).
  await page.locator('label', { hasText: 'Directive preamble' }).locator('input').check()
  await expect(md).toContainText('DIRECTIVE PREAMBLE')
})

test('rendered preview toggle formats the markdown (display-only, no rebuild)', async ({ page }) => {
  await page.goto('/')
  await page.setInputFiles('input[type="file"]', fixture)
  // Raw by default: the source heading shows its literal markdown hashes in a <pre>.
  await expect(page.locator('pre')).toContainText('# MARKDOWNMAP')

  await page.getByRole('button', { name: 'MarkdownMap settings' }).click()
  await page.locator('label', { hasText: 'Rendered preview' }).locator('input').check()

  // Now the markdown is parsed to HTML: a real <h1>, real <h2> sections — and no rebuild.
  await expect(page.locator('.markdown-body h1')).toContainText('MARKDOWNMAP')
  await expect(page.locator('.markdown-body h2').first()).toBeVisible()
  await expect(page.getByText('Generating map', { exact: true })).toBeHidden()
})

test('chunking splits the map into scene-chunks, navigable via the manifest and the map', async ({ page }) => {
  await page.goto('/')
  await page.setInputFiles('input[type="file"]', fixture)
  await expect(page.locator('main svg').getByText('[01]', { exact: true })).toBeVisible()

  // Turn on scene-chunks in MarkdownMap settings, then close the popover.
  await page.getByRole('button', { name: 'MarkdownMap settings' }).click()
  await page.locator('label', { hasText: 'Scene-chunks' }).locator('input').check()
  await page.keyboard.press('Escape')

  // No rebuild — the worker re-renders the cached model in place.
  await expect(page.getByText('Generating map', { exact: true })).toBeHidden()

  // Home view is the manifest menu: interactive chunk rows, not a raw document.
  await expect(page.getByText(/Manifest of \d+ chunks/)).toBeVisible()
  const firstRow = page.locator('aside ul button').first()
  await expect(firstRow).toBeVisible()

  // Hovering a manifest row spotlights that chunk on the map (amber halo on its nodes).
  const halo = page.locator('svg circle[stroke="#f59e0b"]')
  await firstRow.hover()
  await expect(halo.first()).toBeVisible()

  // Clicking a manifest row opens that chunk's self-contained page, with concrete ways out —
  // and the map highlight persists while the chunk is open.
  await firstRow.click()
  const pre = page.locator('pre')
  await expect(pre).toContainText('# SCENE-CHUNK —')
  await expect(pre).toContainText('## Ways out')
  await expect(halo.first()).toBeVisible()

  // A per-chunk download button is offered only while a chunk is open.
  await expect(page.getByRole('button', { name: 'Download chunk' })).toBeVisible()

  // The back button returns to the manifest menu and clears the highlight.
  await page.getByRole('button', { name: '← Chunks' }).click()
  await expect(page.getByText(/Manifest of \d+ chunks/)).toBeVisible()
  await expect(halo).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Download chunk' })).toBeHidden()

  // Clicking a map node opens its chunk too (the map is a live index of the same chunks).
  await page.locator('svg g.cursor-pointer circle').first().click()
  await expect(pre).toContainText('# SCENE-CHUNK —')

  // Turning it back off restores the single whole-area document (live, no rebuild).
  await page.getByRole('button', { name: 'MarkdownMap settings' }).click()
  await page.locator('label', { hasText: 'Scene-chunks' }).locator('input').uncheck()
  await expect(page.locator('pre')).toContainText('# MARKDOWNMAP')
})

test('the sidebar can be resized by dragging its left edge', async ({ page }) => {
  await page.goto('/')
  await page.setInputFiles('input[type="file"]', fixture)
  await expect(page.locator('main svg').getByText('[01]', { exact: true })).toBeVisible()

  const aside = page.locator('aside')
  const before = (await aside.boundingBox())!.width

  const handle = page.getByRole('separator', { name: 'Resize sidebar' })
  const hb = (await handle.boundingBox())!
  await page.mouse.move(hb.x + hb.width / 2, hb.y + hb.height / 2)
  await page.mouse.down()
  await page.mouse.move(hb.x - 120, hb.y + hb.height / 2, { steps: 8 }) // drag left → wider
  await page.mouse.up()

  const after = (await aside.boundingBox())!.width
  expect(after).toBeGreaterThan(before + 80)

  // Double-click the handle resets to the default width.
  await handle.dblclick()
  expect((await aside.boundingBox())!.width).toBeLessThan(after - 40)
})

test('the Restart button clears the map and returns to the drop zone', async ({ page }) => {
  await page.goto('/')
  await page.setInputFiles('input[type="file"]', fixture)
  await expect(page.locator('main svg').getByText('[01]', { exact: true })).toBeVisible()

  await page.getByRole('button', { name: /Restart/ }).click()
  await expect(page.getByText(/Drop a/)).toBeVisible()          // back to the drop zone
  await expect(page.getByRole('button', { name: /Restart/ })).toBeHidden() // button gone with no map
})

test('clicking a token shows its details', async ({ page }) => {
  await page.goto('/')
  await page.setInputFiles('input[type="file"]', fixture)
  await expect(page.locator('main svg').getByText('[01]', { exact: true })).toBeVisible()

  await page.locator('svg g.cursor-pointer circle').first().click()
  const sidebar = page.locator('aside')
  await expect(sidebar).toContainText('Founders Mural') // [01] = highest importance
  await expect(sidebar).toContainText('importance')
})

test('the legend explains the symbology (not a street / approximate)', async ({ page }) => {
  await page.goto('/')
  await page.setInputFiles('input[type="file"]', fixture)
  await expect(page.locator('main svg').getByText('[01]', { exact: true })).toBeVisible()

  await page.getByRole('button', { name: 'Map legend' }).click()
  const legend = page.getByRole('dialog', { name: 'Map legend' })
  await expect(legend).toContainText('not a street')
  await expect(legend).toContainText('approximate')
})

test('map view settings toggle SVG layers (client-side, no rebuild)', async ({ page }) => {
  await page.goto('/')
  await page.setInputFiles('input[type="file"]', fixture)
  const token = page.locator('main svg').getByText('[01]', { exact: true })
  await expect(token).toBeVisible()

  await page.getByRole('button', { name: 'Map view settings' }).click()
  await page.getByRole('checkbox', { name: 'Tokens' }).uncheck()
  await expect(token).toBeHidden() // layer hidden purely in the SVG, no progress card
  await expect(page.getByText('Generating map', { exact: true })).toBeHidden()
})
