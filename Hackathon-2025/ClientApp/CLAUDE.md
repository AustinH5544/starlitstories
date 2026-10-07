# Frontend (React 19 + Vite 6)

Loaded when working in `ClientApp/`. Deployed to Azure Static Web Apps by `.github/workflows/frontend.yml`.

## Where things are

- `src/App.jsx`: client router (all routes). `src/main.jsx`: client entry point.
- `src/entry-server.jsx`: SSR entry used **only** for build-time pre-rendering. It has its own route list, renders with a mock logged-out `AuthContext`, and covers public pages only.
- `src/api.js`: shared Axios instance. Base URL is `${VITE_API_URL}/api`. It attaches `Bearer` tokens from `localStorage["token"]` and logs the user out on a 401 (opt out per request with `skipAuth401Handler`). Always call the API through it.
- `src/config.js`: the only place that reads `import.meta.env`. `VITE_API_URL` and `VITE_APP_BASE_URL` are required.
- `src/context/AuthContext.jsx`: `AuthProvider` / `useAuth` (JWT in localStorage). `src/hooks/`: `useUserProfile`, `usePublicConfig`, `useWarmup`.
- `src/analytics.js`: PostHog. Turnstile captcha appears on signup when `VITE_TURNSTILE_SITE_KEY` is set.
- `src/content/blog/*.js` (registered in `content/blog/index.js`) and `src/content/seoTopics.js`: blog and SEO landing-page content.

## Adding or renaming a public (indexable) page — update all four places

1. `src/App.jsx`: client route
2. `src/entry-server.jsx`: SSR route
3. `scripts/prerender.mjs`: the `routes` array
4. `../Program.cs`: the `staticUrls` arrays in **both** `/sitemap.xml` and `/sitemaps/sitemap-{index}.xml`

New blog posts also need registering in `src/content/blog/index.js`. Pages behind login (`/create`, `/profile`, `/view`, `/upgrade`, `/customize`, `/admin`, ...) must **not** be pre-rendered.

## SSR safety

Anything rendered by `entry-server.jsx` runs in Node at build time. Guard `window`, `document`, and `localStorage` behind `typeof window !== 'undefined'` or `useEffect`, or `npm run build` will fail.

## Build / verify

- `npm run build` = client build + SSR build + `node scripts/prerender.mjs`. `npm run build:staging` does the same with `--mode staging`.
- After changing a public page, check the raw `dist/<route>/index.html` for its content, `<title>`, meta description, and OG tags.
- No test suite: run `npm run lint` and check the change in the browser.
