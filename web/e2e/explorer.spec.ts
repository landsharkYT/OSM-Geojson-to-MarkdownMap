import { test, expect } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const fixture = path.join(path.dirname(fileURLToPath(import.meta.url)), 'fixtures', 'rivertown.geojson')

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

test('clicking a token shows its details', async ({ page }) => {
  await page.goto('/')
  await page.setInputFiles('input[type="file"]', fixture)
  await expect(page.locator('main svg').getByText('[01]', { exact: true })).toBeVisible()

  await page.locator('svg g.cursor-pointer circle').first().click()
  const sidebar = page.locator('aside')
  await expect(sidebar).toContainText('Founders Mural') // [01] = highest importance
  await expect(sidebar).toContainText('importance')
})
