# WEBSITE_ROUTE_INVENTORY — Phase 0

Baseline commit: `d6c5514` on `arena/ff328a9c-proglassautomationadv` (2026-10-13).
Method: filesystem enumeration of `src/app` + `next build` route table. Nothing inferred, nothing invented.

## Rendering contract, proved at build time

- All **27 page routes are Server Components** — zero `"use client"` in any `page.tsx` (checked per-file).
- 73 client islands out of 220 `.ts/.tsx` files (33%) — interactivity concentrated in primitives (`Reveal`, `Spotlight`, `ThemeStudioLazy`, form controls, operational UIs).
- `[locale]` is a top-level **dynamic segment**: every content route is SSG `●` via `generateStaticParams` for **15 locales**; there is no `src/app/layout.tsx` — `[locale]/layout.tsx` IS the root layout (fonts, boot script, SEO graph).
- `src/proxy.ts` runs as the build’s “Proxy (Middleware)” — locale/rewrite concern, not audited beyond presence here.

## Content routes (all patterns × 15 locales, SSG)

| # | Route pattern | File | Rendering | Notes (inspected) |
|---|---------------|------|-----------|-------------------|
| 1 | `/[locale]` | `[locale]/page.tsx` | server · SSG | Homepage. Composes `ProductHero.tsx` (349 ln), `HeroField.tsx` (client, 94 ln), `DeveloperStory.tsx` (230 ln), home/feature sections. Hero imagery ships as responsive AVIF/WebP ladders (640/1024/1536 + 1280–3840) rendered in-repo by `scripts/render-hero-*.mts` — PNG masters up to 884K are render sources, not shipped bytes. |
| 2 | `/[locale]/features` | server · SSG | Feature catalogue. |
| 3 | `/[locale]/how-it-works` | server · SSG | Workflow explainer. |
| 4 | `/[locale]/optimization` | server · SSG | Optimization engine tour. |
| 5 | `/[locale]/worked-example` | server · SSG | Nesting walkthrough. |
| 6 | `/[locale]/pricing` | server · SSG | Commercial terms. |
| 7 | `/[locale]/screenshots` | server · SSG | Product gallery (historically the 89-reveal page — shared-observer fix in `useReveal`). |
| 8 | `/[locale]/about` | server · SSG | About. |
| 9 | `/[locale]/developer` | server · SSG | Engineering/dossier page (P13–P15 witness origin; 820 ln). |
| 10 | `/[locale]/contact` | server · SSG | Contact + `<DemoRequestForm>` (client island, `/api/demo-request`). |
| 11 | `/[locale]/collaborate` | server · SSG | Partner page. |
| 12 | `/[locale]/sponsors` | server · SSG | Sponsors. |
| 13 | `/[locale]/resources` | server · SSG | Resources hub. |
| 14 | `/[locale]/blog` | server · SSG | Blog index. |
| 15 | `/[locale]/blog/[slug]` | server · SSG | 15 posts × 15 locales statically expanded ([+42 more paths] at build). |
| 16 | `/[locale]/legal/terms` | server · SSG | Legal. |
| 17 | `/[locale]/legal/privacy` | server · SSG | Legal. |
| 18 | `/[locale]/legal/license` | server · SSG | Legal. |
| 19 | `/[locale]/records` | server · SSG | Records board (Supabase-backed operational route — out of marketing-redesign focus per master rule 10). |
| 20 | `/[locale]/records/login` | server · SSG | Records auth (client form island). |
| 21 | `/[locale]/reports` | server · SSG | Reports board (operational; same rule-10 note). |
| 22 | `/[locale]/reports/login` | server · SSG | Reports auth. |
| 23 | `/[locale]/job-orders` | server · SSG | Job-order tracking (operational). |
| 24 | `/[locale]/deliveries` | server · SSG | Delivery tracking (operational). |
| 25 | `/[locale]/sys/clay` | server · SSG | **Internal review surface**, `noindex` — clay engine catalogue. |
| 26 | `/[locale]/sys/crystal` | server · SSG | **Internal review surface**, `noindex` — crystal engine catalogue. |
| 27 | `/[locale]/page.tsx → not-found` | `[locale]/not-found.tsx` | server | Localized 404 with `NotFoundSuggestions`. |

## System routes

| Route | File | Notes |
|-------|------|-------|
| `/_not-found` | root fallback | Static `○`. |
| global error | `src/app/global-error.tsx` | Root error boundary (has `<html>` by necessity — records the missing root layout trade-off). |
| route error | `[locale]/error.tsx` | Localized boundary. |
| loading | `[locale]/loading.tsx` | Streaming state. |
| `/robots.txt` | `src/app/robots.ts` | Static. |
| `/sitemap.xml` | `src/app/sitemap.ts` | Static; `sys/*` excluded. |
| `/icon.svg`, `/favicon.ico` | static | Brand. |
| `/api/og` | dynamic `ƒ` | OG image generation. |

## API routes (all dynamic `ƒ` except `/api/health` static)

`/api/auth/signout` · `/api/deliveries` · `/api/demo-request` · `/api/health` ○ · `/api/job-orders` · `/api/job-orders/progress` · `/api/job-orders/release` · `/api/og` · `/api/operations-export/[report]` · `/api/records` — 10 routes; website-redesign scope treats these as fixed contracts.

## Locales (15 — the application’s own language set)

en-US, ar-AE, bn-BD, de-DE, es-ES, fr-FR, hi-IN, id-ID, ja-JP, ko-KR, pt-BR, ru-RU, tr-TR, ur-PK, zh-CN (`src/i18n/locales.ts`; dictionaries in `src/i18n/dict`, 17 files, 5.3 MB).

## Appearance × theme matrix the routes are viewed through

- **Modes** (`data-theme-mode`): light · dark · system — resolved to daylight/midnight.
- **Families** (`data-appearance`): studio (violet) · monet · industrial (cyan) · crystal (glass) · clay (sculpted pastel) — five live visual systems on every route.
- A route is therefore only fully verified at mode × family, which is why the harness architecture (paint/contrast/census) rather than screenshot spot-checks carries verification in this repository.

## Not found / not inspected

- No hidden or dynamic catch-all routes beyond `blog/[slug]` and the API set.
- Deployed-site DOM diff: **not inspectable** — live host unreachable from this sandbox (see WEBSITE_BASELINE §6).
