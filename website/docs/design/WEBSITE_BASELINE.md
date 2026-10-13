# WEBSITE_BASELINE — Phase 0 measurements

Commit: `d6c5514` · Branch: `arena/ff328a9c-proglassautomationadv` (working tree clean) · Date: 2026-10-13
Method statement: every number below is reproduced by a command named beside it. Nothing is estimated.

## 1 · Toolchain

| Fact | Value | Evidence |
|------|-------|----------|
| Node / npm | v22.22.3 / 10.9.8 | `node --version` |
| Framework | Next.js **16.3.6**, React **19.2.0**, React-DOM 19.2.0 | `package.json` |
| Styling | Tailwind **3.4.17** + PostCSS 8.5.1 + Autoprefixer 10.4.20 | devDependencies |
| Type system | TypeScript 5.7.3, strict (`tsc --noEmit` clean) | `tsconfig` + gate |
| **Animation libraries** | **NONE (zero)** | dependency ledger: no `motion`, no `framer-motion`, no `gsap`, no `lenis`. All motion is hand-authored CSS (20 `@keyframes` across three stylesheets) + Web APIs (`IntersectionObserver`, pointer events writing custom properties). |
| Testing | Vitest 3.2.7 + Testing Library + jsdom; node:test harnesses | `tests/`, `scripts/` |
| Fonts | 4 self-hosted variable families (Inter, IBM Plex Sans, Source Serif 4, JetBrains Mono) via `@fontsource-variable/*`; system-font second group resolved by the browser | `src/lib/useStudio.ts` typeface table |
| Imagery | `sharp` 0.33.5; render+optimize pipeline (`assets:backdrop/banner/optimize`) producing AVIF+WebP ladders | `public/images/**`, `scripts/render-hero-*.mts` |
| Telemetry | `@vercel/speed-insights` 2.0.0 installed (field CWV when deployed) | dependencies |
| Supabase | `supabase-js` 2.117.2 (operational routes only; master rule 10 keeps it out of focus) | dependencies |

## 2 · Source topology

| Measure | Value | Evidence |
|---------|-------|----------|
| Page routes | 27 (all Server Components) | route inventory doc |
| Locales | 15; every content route statically expanded ×15 | build table |
| Client islands | 73 of 220 `.ts/.tsx` (33%) | `grep -l "use client"` census |
| Stylesheets | `globals.css` 4,257 ln · `clay-engine.css` 2,600 ln · `crystal-engine.css` 1,072 ln = **7,929 ln hand-authored CSS** | `wc -l` |
| Design tokens | ~793 custom-property declarations | regex census of `--*: ` |
| Shared UI primitives | 14 in `components/ui` (Accordion, Badge, Button, Container, EmptyState, Glass, Icon, LocaleLink, Marquee, Reveal, SectionHeading, Slider, Spotlight, Stat) + 19 component dirs, 92 `.tsx` | `find` |
| Motion primitives | `Reveal` (one shared IntersectionObserver per rootMargin; class-toggle, no re-render; CSS-driven so `prefers-reduced-motion` is free), `Spotlight` (pointer→`--mx/--my` custom properties; state-free), `Marquee` (CSS), `HeroField` (client canvas/DOM field) | file reads |
| Reduced-motion guards | 46 `prefers-reduced-motion` hits across CSS + components | census |
| Backdrop-filter declarations | 84 (live budget: blur census 522 surfaces worst-route out of 600) | `crystal:census` |

## 3 · Production build artifacts (fresh build of `d6c5514`)

| Measure | Value | Method |
|---------|-------|--------|
| Client JS (`.next/static/chunks`) | **2 MB raw · 0.5 MB gz** total | `du -sm` + gzip aggregate |
| Largest chunks (raw) | 240K · 232K · 184K · 160K · 112K | `ls -S` |
| CSS emitted | 220 KB raw over 3 files · **38.6 KB gz** | aggregate gzip |
| Public assets | 7.3 MB / 34 images; masters are render sources | `du -sh`, `find` |
| Hero asset ladders | backdrop + banner at 1280/1920/2560/3840 × {avif, webp}; glass-3D at 640/1024/1536 × {avif, webp} (28K–160K each) | `ls public/images/**` |
| Static expansion | 27 routes × 15 locales + 15 blog slugs × 15 — all SSG `●` | build route table |
| Note | Next 16’s build no longer prints the per-route First-Load-JS table; per-route split planned via `app-build-manifest` in Phase 7 | observed |

## 4 · Quality gates — fresh results on `d6c5514`

| Gate | Command | Result |
|------|---------|--------|
| Production build | `npm run build` | ✓ compiled; SSR/SSG clean; postbuild freshness stamped |
| Type check | `tsc --noEmit` | ✓ clean |
| Lint | `npm run lint` (eslint src scripts) | ✓ clean (two baseline errors cleared in P19) |
| Unit/DOM tests | `vitest run` | ✓ **59/59** (5 files; incl. 21-test studio click-through) |
| Clay paint budget | `node scripts/clay-paint.test.mjs` | ✓ **175/175** |
| Clay contrast | `node scripts/clay-contrast.test.mjs` | ✓ **25/25** |
| Crystal contrast | `npm run testcrystal` | ✓ **87/87** |
| Blur census (live prod server crawl) | `crystal-blur-census --budget` | ✓ 23 pages crawled; worst /features **522/600** |
| Clay coverage | `clay-coverage --check` | ✓ registry 177 sculpted · 144 foils lifted · 100% |
| Pixel pass | `node scripts/clay-pixel-pass.mjs` | ✓ every card-shaped element sculpted on every route |
| Navbar clip | `node --test scripts/nav-pill-clip.test.mjs` | ✓ 2/2 |
| Pointer-state guard | `guard:pointer` | ✓ 158 files, 0 unguarded pointer-state writes |
| Color-literal audit | `scripts/color-liter-audit.mjs` | ✓ 0 unresolved |
| Job-order domain tests | `test:job-orders` | ✓ 8/8 |
| **i18n verification** | `i18n:verify` | ✗ **8/15 locales FAIL** — see §5 |
| Supabase check | `supabase:check` | not run — out of website scope (rule 10); recorded as available |

## 5 · i18n measurement (defect, recorded with numbers)

`node scripts/i18n-verify.mjs` — **4,502 keys** total:

| Locale group | Entries | Coverage | Verdict |
|--------------|---------|----------|---------|
| ar-AE, bn-BD, es-ES, fr-FR, hi-IN, id-ID, pt-BR | ~3,649–3,681 | ~75.3% | **FAILS — marked complete** (8 locales) |
| de-DE | 3,681 | 76.0% | **FAILS — marked complete** |
| ja-JP, ko-KR, tr-TR, zh-CN | 451 | 9.9% | in progress |
| ru-RU, ur-PK | 517–518 | 10.9% | in progress |
| en-US (+ source) | 4,502 | 100% | pass |

## 6 · Limitations (recorded, not worked around)

1. **No browser in this sandbox.** No screenshots, no Lighthouse, no visual inspection claims anywhere in this program — by master prompt rule 4 and its own “If visual inspection tools are unavailable” clause. Verification substitutes: DOM-level suites (jsdom), CSS paint/contrast/ledger harnesses that execute against the built bundle, prod-server route crawls, and artifact measurement. Visual categories in the Phase-1 rubric are marked **inspection-limited** and must be confirmed on the deployed URL by a session with a browser.
2. **Live site unreachable from this sandbox** (egress restricted to GitHub/npm/PyPI). `npm run check:live` ran and returned “unreachable — fetch failed”. Deploy/source diffing and post-deploy verification of the Vercel URL are **blocked at sandbox level**; `check:live` exists and will do the comparison wherever network allows.
3. **No field CWV yet.** `@vercel/speed-insights` is installed, so real-user LCP/INP/CLS accrue on the deployment; this sandbox can only produce lab proxies (artifact weights, surface census, frame-cost-style budgets).

## 7 · Git state

`d6c5514` is HEAD and pushed (`check:live`: “the branch: d6c5514”). Working tree clean at audit start. Convention in force: one phase = one commit = one push, on this branch only.
