import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const distPath = path.resolve(__dirname, '../dist')
const serverEntry = pathToFileURL(path.resolve(__dirname, '../dist/server/entry-server.js')).href
const { render } = await import(serverEntry)

const template = fs.readFileSync(path.join(distPath, 'index.html'), 'utf-8')

const routes = [
  '/',
  '/about',
  '/faq',
  '/login',
  '/signup',
  '/support',
  '/blog',
  '/blog/personalized-bedtime-storybooks',
  '/blog/ai-story-generator-for-kids',
  '/blog/personalized-childrens-books',
  '/blog/personalized-bedtime-stories',
  '/blog/bedtime-story-ideas-for-kids',
  '/blog/how-ai-story-generators-help-kids-love-reading',
]

// React 19 renders <title>, <meta>, <link>, <script> inline in renderToString output.
// Extract them and move to <head> via the <!--app-head--> placeholder.
function extractHeadTags(html) {
  const headTagPattern = /<title[\s\S]*?<\/title>|<meta[^>]*\/?>(<!-- -->)?|<link[^>]*\/?>(<!-- -->)?|<script[^>]*>[\s\S]*?<\/script>/gi
  const headTags = []
  const bodyHtml = html.replace(headTagPattern, (match) => {
    // Only hoist tags that look like SEO/metadata (not inline scripts with logic)
    const lower = match.toLowerCase()
    if (
      lower.startsWith('<title') ||
      lower.startsWith('<meta') ||
      lower.startsWith('<link') ||
      (lower.startsWith('<script') && lower.includes('application/ld+json'))
    ) {
      headTags.push(match.replace(/<!-- -->/g, '').trim())
      return ''
    }
    return match
  })
  return { headTags, bodyHtml }
}

for (const route of routes) {
  const { html: appHtml } = await render(route)
  const { headTags, bodyHtml } = extractHeadTags(appHtml)

  const headTagsStr = headTags.filter(Boolean).join('\n    ')

  const finalHtml = template
    .replace('<!--app-head-->', headTagsStr)
    .replace('<div id="root"></div>', `<div id="root">${bodyHtml}</div>`)

  const outDir = route === '/' ? distPath : path.join(distPath, route)
  fs.mkdirSync(outDir, { recursive: true })
  fs.writeFileSync(path.join(outDir, 'index.html'), finalHtml)
  console.log(`Pre-rendered: ${route}`)
}
