# Website Route Inventory — Phase 0

> Executed: 2026-10-13 · HEAD `d3469e4` (`fix(studio): P18 — forensic pass…`) · Working branch `arena/51889ca3-proglassautomationadv` (clean tree at audit time) · Site version `2.0.11` (`package.json` + `src/lib/site.ts`)
> Method: `find src/app -name page.tsx`, production build route table (Next.js 16.3.6, Turbopack), local production server crawl (`audit-site.py`), sitemap source read. Nothing in this file is invented; uninspectable items are marked.

## 1. Stack (verified from source, not assumed)

| Fact | Evidence |
|---|---|
| Framework | Next.js `16.3.6`, React `19.2.0` (`website/package.json`) |
| Router | App Router, `src/app/[locale]/…`, `generateStaticParams` over 15 locales, `dynamicParams = false` (`src/app/[locale]/layout.tsx`) |
| Languages | 15 locales (`src/i18n/locales.ts`): `en-US` (default, unprefixed), `ar-AE`, `bn-BD`, `de-DE`, `es-ES`, `fr-FR`, `hi-IN`, `id-ID`, `ja-JP`, `ko-KR`, `pt-BR`, `ru-RU`, `tr-TR`, `ur-PK`, `zh-CN`. RTL: `ar-AE`, `ur-PK` |
| Locale routing | `src/proxy.ts` — cookie → country (`x-vercel-ip-country`) → `Accept-Language` → `en-US`; non-English = redirect to prefixed URL, English = rewrite to `/en-US…` keeping bare URL |
| Rendering | 429 static pages generated at build (`429/429` in build log) = 26 page templates × locales + blog posts; API routes are dynamic (`ƒ`) |
| Styling | Tailwind CSS `3.4.17` + `src/app/globals.css` (4,241 lines), `clay-engine.css` (2,600 lines), `crystal-engine.css` (1,072 lines); tokens are CSS custom properties (raw RGB triplets) |
| Appearance system | 2 modes (`midnight`/`daylight` via `data-theme`) × 5 families (`studio`, `monet`, `industrial`, `crystal`, `clay` via `data-appearance`, `src/data/themes.ts`); first-run default family = `clay` (hygiene-v2 boot migration), `<html>` server-renders `data-appearance="crystal"` and the inline boot script corrects before paint |
| Motion | **No** `motion/react`, `framer-motion`, `gsap`, or `lenis` installed (grep over `src/` = 0 hits). Motion is CSS + small hooks: `Reveal`/`useReveal` (shared IntersectionObserver), `useSpotlight`, `useTilt`/`Tilt`, `HeroField`, `GlassLife`/`glassLife.ts` |
| Data/integrations | Supabase (`@supabase/supabase-js`) behind operator-gated dashboards; `@react-pdf/renderer` for operations PDFs; `@vercel/speed-insights` |
| Deployed origin | `https://proglassautomationadv-website-tkim.vercel.app` (default in `src/lib/site.ts`, overridable via `NEXT_PUBLIC_SITE_URL`) |

## 2. Page routes (26 templates → 429 built pages)

All routes live under `src/app/[locale]/`. Every page template exists in all 15 locales. `en-US` URLs are unprefixed (`/features`); all other locales are prefixed (`/de-DE/features`).

| # | Route template | Public URL (en-US) | Type | Purpose (from source) | In sitemap | In nav |
|---|---|---|---|---|---|---|
| 1 | `[locale]/page.tsx` | `/` | SSG | Homepage: hero, workflow chain, optimization, modules, FAQ, CTA | ✓ (priority 1.0) | Home |
| 2 | `[locale]/features/page.tsx` | `/features` | SSG | Every module in the app's vocabulary | ✓ 0.9 | Product menu |
| 3 | `[locale]/optimization/page.tsx` | `/optimization` | SSG | Guillotine nesting engine, plans, exports | ✓ 0.9 | Top-level |
| 4 | `[locale]/how-it-works/page.tsx` | `/how-it-works` | SSG | Inquiry → statement, step by step | ✓ 0.8 | Product menu |
| 5 | `[locale]/worked-example/page.tsx` | `/worked-example` | SSG | One real job, every figure | ✓ 0.9 | Product menu |
| 6 | `[locale]/screenshots/page.tsx` | `/screenshots` | SSG | Interface, screen by screen | ✓ 0.8 | Product menu |
| 7 | `[locale]/pricing/page.tsx` | `/pricing` | SSG | Editions and licensing | ✓ 0.8 | Product menu |
| 8 | `[locale]/about/page.tsx` | `/about` | SSG | About ProGlass | ✓ 0.7 | Top-level |
| 9 | `[locale]/developer/page.tsx` | `/developer` | SSG | Developer story, build facts | ✓ 0.7 | Top-level |
| 10 | `[locale]/collaborate/page.tsx` | `/collaborate` | SSG | Work on it with the author | ✓ 0.8 | Top-level |
| 11 | `[locale]/sponsors/page.tsx` | `/sponsors` | SSG | Back the build | ✓ 0.7 | Resources menu |
| 12 | `[locale]/contact/page.tsx` | `/contact` | SSG | Demo request (primary CTA target) | ✓ 0.7 | Footer |
| 13 | `[locale]/resources/page.tsx` | `/resources` | SSG | Guides to the glass workflows | ✓ 0.6 | Resources menu |
| 14 | `[locale]/blog/page.tsx` | `/blog` | SSG | Notes index (3 posts in `posts/`) | ✓ 0.6 | Resources menu |
| 15 | `[locale]/blog/[slug]/page.tsx` | `/blog/{slug}` | SSG | 3 posts × 15 locales = 45 pages; unknown slug = 404 (verified) | ✓ 0.6 | — |
| 16 | `[locale]/legal/privacy/page.tsx` | `/legal/privacy` | SSG | Privacy | ✓ 0.3 | Footer |
| 17 | `[locale]/legal/terms/page.tsx` | `/legal/terms` | SSG | Terms | ✓ 0.3 | Footer |
| 18 | `[locale]/legal/license/page.tsx` | `/legal/license` | SSG | Licence | ✓ 0.3 | Footer |
| 19 | `[locale]/reports/page.tsx` | `/reports` | SSG shell + client data | Operator dashboard (Supabase, session-gated; renders sign-in/preview without session) | ✓ 0.8 | Product menu |
| 20 | `[locale]/reports/login/page.tsx` | `/reports/login` | SSG | Operator sign-in | ✗ (not in sitemap list) | — |
| 21 | `[locale]/records/page.tsx` | `/records` | SSG shell + client data | Records register (session-gated) | ✓ 0.8 | Footer |
| 22 | `[locale]/records/login/page.tsx` | `/records/login` | SSG | Operator sign-in | ✗ | — |
| 23 | `[locale]/job-orders/page.tsx` | `/job-orders` | SSG shell + client data | Job-order production tracking | ✓ 0.8 | Product menu |
| 24 | `[locale]/deliveries/page.tsx` | `/deliveries` | SSG shell + client data | Delivery notes / balances | ✓ 0.8 | Product menu |
| 25 | `[locale]/sys/clay/page.tsx` | `/sys/clay` | SSG, `noindex` | **Internal** clay-engine catalog (review tool) | ✗ excluded | — |
| 26 | `[locale]/sys/crystal/page.tsx` | `/sys/crystal` | SSG, `noindex` | **Internal** crystal-engine catalog (review tool) | ✗ excluded | — |

Plus per-locale error surfaces: `[locale]/error.tsx`, `[locale]/not-found.tsx`, `[locale]/loading.tsx`, `app/global-error.tsx`, and `/_not-found`.

> Note: `job-orders` and `deliveries` have no separate login templates; they share the records/reports session model via API routes. `records/login` and `reports/login` are the two sign-in pages.

## 3. API / non-page routes (10)

| Route | Method surface | Notes |
|---|---|---|
| `api/health/route.ts` | GET | Build stamp JSON (revision, branch, builtAt). Static (`○`) at build; used by `check:live` / `audit-site.py --fresh` |
| `api/og/route.tsx` | GET | Per-page social card image (1200×630) |
| `api/demo-request/route.ts` | POST | Contact/demo form transport |
| `api/auth/signout/route.ts` | POST | Operator sign-out |
| `api/records/route.ts` | GET | Records data (session-gated) |
| `api/deliveries/route.ts` | GET | Deliveries data |
| `api/job-orders/route.ts` + `progress` + `release` | GET/POST | Job-order data and release actions |
| `api/operations-export/[report]/route.ts` | GET | Operations exports (PDF/CSV, `no-store`) |

Metadata routes: `app/sitemap.ts` (21 pages × 15 locales + 3 posts × 15, with reciprocal `hreflang` alternates), `app/robots.ts`, `app/icon.svg`, `app/favicon.ico`.

## 4. Shared layout & component inventory

- **Layout**: `src/app/[locale]/layout.tsx` — fonts (Inter, JetBrains Mono, Source Serif 4, IBM Plex Sans, Nunito; all self-hosted, `preload: false`), theme boot script, `Navbar`, `Footer`, `ThemeStudioLazy`, `SupportBubble`, `GlassLife`, JSON-LD global graph, `SpeedInsights`.
- **Chrome**: `Navbar.tsx` (822 lines, client), `Footer.tsx`, `LanguageMenu.tsx`, `ThemeStudio.tsx` (939 lines) + `ThemeStudioLazy`, `ThemeToggle`/`ThemeModeSwitch`, `BuildStamp`, `PageHero`/`PageBanner`, `LegalPage`.
- **UI primitives** (`src/components/ui/`): `Button`, `Glass` (`GlassCard`/`GlassMetric`), `Reveal`, `Spotlight`, `Accordion`, `Badge`, `Stat`, `SectionHeading`, `Container`, `Slider`, `Marquee`, `Icon`, `LocaleLink`, `EmptyState`.
- **Product surfaces** (`src/components/product/`): `AppFrame`, `LiveSurface` (1,412 lines), `SpecificationSurface` (1,732), `ProfitLossSurface` (714), `ModuleBrowser`, `ProductSlider`, `WorkflowRail`, stages/log/preview atoms.
- **Census (this audit)**: 128 `.tsx` + 92 `.ts` under `src/` (~31,400 lines); 67 files carry `"use client"`; `Reveal` used 140× across 25 files; pages themselves are Server Components (no `"use client"` in any `page.tsx`).

## 5. What could not be inspected

- **Rendered pixels**: the sandbox has no persistent browser binary (Playwright chromium does not survive between turns — same limitation recorded in `docs/design/CLAYMORPHISM_BASELINE_AUDIT.md`). No screenshot in this inventory is claimed. Visual state is evidenced by computed-style harnesses (paint/contrast/census/pixel-pass) + deployed-HTML fetches instead.
- **Field data**: no CrUX/RUM export was available; Core Web Vitals are lab/build-derived only (see `WEBSITE_BASELINE.md`).
- **Vercel project settings** (Production Branch, env vars): not readable from the repo; deployment freshness is judged only via `/api/health` (see baseline).
