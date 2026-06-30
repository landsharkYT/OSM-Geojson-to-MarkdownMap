import { describe, it, expect } from 'vitest'
import { renderMarkdown } from './markdown'

describe('renderMarkdown', () => {
  it('renders headings, bold, and fenced code blocks', () => {
    const html = renderMarkdown('# Title\n\nsome **bold** text\n\n```\n[01] A → [02] B\n```\n')
    expect(html).toContain('<h1')
    expect(html).toContain('<strong>bold</strong>')
    expect(html).toContain('<pre>')
    expect(html).toContain('[01] A')
  })

  it('strips event-handler attributes injected via an OSM name (no XSS)', () => {
    const html = renderMarkdown('## <img src=x onerror=alert(1)> Riverside Park\n')
    expect(html).not.toContain('onerror')
  })

  it('removes raw script tags', () => {
    const html = renderMarkdown('a place named <script>alert(1)</script> here')
    expect(html).not.toContain('<script')
  })
})
