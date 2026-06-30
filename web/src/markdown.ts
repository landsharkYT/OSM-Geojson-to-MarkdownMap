import { marked } from 'marked'
import DOMPurify from 'dompurify'

// Display-only (sidebar preview). The string itself is the artifact — Copy/Download stay raw.
marked.setOptions({ gfm: true, breaks: false })

/**
 * Render the generator's MarkdownMap to **sanitized** HTML for the sidebar preview.
 * OSM names are user data, so the output is always run through DOMPurify — a place literally
 * named `<img onerror=…>` must not execute. Never used for the copied/downloaded text.
 */
export function renderMarkdown(md: string): string {
  const html = marked.parse(md, { async: false })
  return DOMPurify.sanitize(html)
}
