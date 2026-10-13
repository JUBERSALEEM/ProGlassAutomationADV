# Website Baseline — Phase 0 (measured, reproducible)

> Executed: 2026-10-13 · HEAD `d3469e4` · branch `arena/51889ca3-proglassautomationadv` · Node `v22.22.3`, npm `10.9.8` · Site `2.0.11`
> Rule: every number below names the command that produced it. Anything not measured is labelled **unverified**.

## 1. Environment & working tree

| Check | Command | Result |
|---|---|---|
| Branch / HEAD | `git branch --show-current; git log -1 --oneline` | `arena/51889ca3-proglassautomationadv` @ `d3469e4` |
| Working tree | `git status --short` | **Clean** (no tracked modifications; `node_modules/` + `.next/` are gitignored build output) |
| Package manager | `package-lock.json` (lockfileVersion 3) | npm; `npm ci` installed 596 packages in ~16s (1 peer warning: `vite`/`@types/node`, pre-existing) |
| Repo shape | repo root listing | Monorepo: WPF desktop app at root, **website in `website/`** — all website work is scoped to `website/` (WPF is out of scope) |

## 2. Build / lint / type / test — actual outcomes

| Gate | Command | Outcome |
|---|---|---|
| Typecheck | `npm run typecheck` (`tsc --noEmit`) | ✅ **Pass** (0 errors) |
| Production build | `npm run build` | ✅ **Pass** — compiled 19.3s, TS 16.5s, **429/429 static pages**, `build cache marked as d3469e4` |
| Lint | `npm run lint` (`eslint src scripts`) | ⚠️ **2 errors** (pre-existing): `scripts/color-liter-audit.mjs:20` unused `SHADOW_OK`; `src/app/[locale]/sys/clay/page.tsx:32` unused `t`. Exit code was masked by the pipe in this run; `eslint` reports 2 problems |
| Unit/interaction tests | `npx vitest run` | ✅ **55/55** (5 files: studio-interaction 17, clay-studio 17, navbar 8, glass-life 6, crystal-studio 7) |
| Clay contrast + coverage | `npm run test:clay` | ✅ 25/25 contrast pairs; coverage 100% (177 sculpted + 144 lifted, 0 raw) |
| Crystal contrast | `node scripts/crystal-contrast.test.mjs` | ✅ 87/87 |
| Clay paint | `node scripts/clay-paint.test.mjs` | ✅ 175/175 |
| Motion/reduced-motion | `node scripts/crystal-motion.test.mjs` | ✅ 12/12 |
| Pointer-state guard | `npm run guard:pointer` | ✅ 158 files, 0 unguarded state writes |
| Colour-literal audit | `node scripts/color-liter-audit.mjs` | ✅ 0 unresolved problems |
| Site crawl (links/meta/headings) | `python3 scripts/audit-site.py http://127.0.0.1:3000` | ✅ **0 failures, 0 warnings** (23 pages, 34 internal links) |
| Site crawl + budgets | `… --budget` | ✅ 0 failures (table in §4) |
| Blur census | `node scripts/crystal-blur-census.mjs --budget` | ✅ worst route `/features` = 522 surfaces vs budget 600 (23 pages) |
| Clay pixel pass | `node scripts/clay-pixel-pass.mjs` | ✅ 943 sculpted + 821 lifted, **0 raw** over 27 routes (100.0%) |
| Live-origin check | `npm run check:live` | ⚠️ **Could not run from sandbox**: direct egress to `*.vercel.app` is blocked (node `fetch failed`; `curl` = 000). Judged via the fetch tool instead — see §5 |

Local server used for crawls: `npm run start` (production build on `127.0.0.1:3000`), then stopped. Spot checks: `/`, `/features`, `/optimization`, `/how-it-works`, `/pricing`, `/developer`, `/contact`, `/de-DE/features` → 200; `/ar-AE/` → 308 → `/ar-AE` (200, `lang="ar-AE" dir="rtl"`); `/blog/this-post-does-not-exist`, `/xx/features`, `/does-not-exist` → 404.

## 3. Bundle & asset weights (from `.next`, this build)

| Measure | Value | Command |
|---|---|---|
| JS chunks emitted | 42 files, 2.0 MB raw → **493.6 KB gzipped total** (budget 540K ✅) | `du`/`gzip` over `.next/static/chunks` + audit-site |
| CSS emitted | 139 KB + 80 KB + 2.9 KB raw → **38.6 KB gzipped** (budget 39K ✅, **0.4 KB headroom — fragile**) | audit-site `--budget` |
| First-load JS per page (gz) | 296.9–307.6 KB across sampled pages (19 chunks) | audit-site `--budget` |
| Prerendered HTML | 422 `.html` files; homepage raw ~764–1,008 KB per locale (mostly inline RSC payload + full dictionary overlay) | `find .next/server/app -name "*.html"` |
| Public assets | 59 files, 7.3 MB (AVIF/WebP/PNG derivatives at 640–3840 px) | `du -sh public` |

## 4. Per-page weight (audit-site `--budget`, gzipped HTML / DOM nodes)

| Page | gz | Budget | Nodes | Budget | Verdict |
|---|---|---|---|---|---|
| `/` | 103.9K | 107K | 3,840 | 4,400 | ✅ (3K headroom) |
| `/features` | 93.6K | 95K | 4,833 | 4,900 | ⚠️ **67 nodes headroom** |
| `/optimization` | 60.1K | 61K | 2,256 | 2,400 | ✅ (0.9K headroom) |
| `/how-it-works` | 56.9K | 57K | 1,959 | 2,100 | ⚠️ **0.1K headroom** |
| `/screenshots` | 38.7K | 39K | 1,268 | 1,400 | ⚠️ 0.3K headroom |
| `/developer` | 61.4K | 62K | 1,340 | 1,450 | ⚠️ 0.6K headroom |
| `/worked-example` | 57.5K | 59K | 1,710 | 1,800 | ✅ |
| `/sponsors` `/collaborate` | 40.5K / 41.9K | 41K / 42K | 876 / 933 | 950 / 980 | ⚠️ ≤0.5K headroom |
| All others | ≤38.5K | 39–40K | ≤905 | 800–980 | ✅ |

**Reading**: every gate passes, but 7 of 10 budgeted pages sit within ~1K / ~70 nodes of their ceilings. Any net-new markup (e.g. added motion wrappers) must be budget-neutral or the ceilings must be re-justified in the same change — this is now a hard constraint for Phases 2–5.

Blur census (crystal `backdrop-filter` surfaces): `/` and `/features` = 470 each (audit unit) / 513–522 (census unit) vs 600 budget — the two heaviest pages are also the two closest to the blur ceiling.

## 5. Deployed site vs checked-out source

| Check | Evidence | Result |
|---|---|---|
| Deployed revision | `GET https://proglassautomationadv-website-tkim.vercel.app/api/health` (via fetch tool; sandbox direct egress blocked) | `{"revision":"d3469e4","branch":"arena/ff328a9c-proglassautomationadv","environment":"production","builtAt":"2026-10-13T11:29:37Z","version":"2.0.11"}` |
| vs local HEAD | `git log -1` = `d3469e4` | ✅ **Same revision** — deployed build is the checked-out code (branch label differs because the session branch `arena/51889…` was branched from the deployed `arena/ff328…` at the same commit; tree is clean) |
| Deployed homepage content | Fetched `/` HTML/text via fetch tool | Matches source: hero ("Glass business management, built around your workflow."), demo CTAs, `LiveSurface` dashboard, developer story — no content drift observed |
| Local health | `curl 127.0.0.1:3000/api/health` | `d3469e4`, branch `arena/51889…`, `environment: local` — stamp machinery works |

**Discrepancy recorded**: `src/lib/site.ts` doc comment says *"At the time of writing the two Vercel origins both answer no stamp"* — that is **stale**; the primary origin now answers with a current stamp. Doc-only defect (see audit D-10).

## 6. Performance baseline (lab only — no field data)

- **Not measured (unverified)**: LCP / INP / CLS numbers, Lighthouse scores, real-device frame rates. No browser exists in the sandbox and no CrUX export was provided, so **no Core Web Vitals figure is claimed in Phase 0**. Phase 7 must produce lab measurements under stated conditions (throttling, device profile) and label them as lab data.
- **Structural facts that bound performance** (source-verified): native scrolling (no scroll-hijack library); decorative animation restricted to `transform`/`opacity` by an explicit, documented ban list in `README.md` (no full-viewport `mix-blend-mode`, no `@property` gradient animation, no wide `filter: blur()`); shared single `IntersectionObserver` for all reveals; rAF-coalesced pointer handlers writing CSS variables only (`pointer-state-guard` enforces no React state in pointer handlers); fonts self-hosted with `preload: false`; hero image via `next/image` (`priority`, AVIF/WebP, `deviceSizes` to 3840).

## 7. Screenshot baseline

**Not captured — tooling limitation, stated plainly.** The sandbox cannot keep a headless-browser binary (recorded identically in the 2026-10-13 clay baseline audit). Phase 0 therefore has **no pixel baseline**, and no later phase may claim "before/after" pixel proof from Phase 0. Substitutes used: computed-style harnesses (175 paint probes, 87 crystal + 25 clay contrast pairs, 522-surface blur census, 27-route pixel-pass classification) and deployed-HTML fetches. Phase 1 defines the screenshot protocol (deployed preview + witness captures) to close this gap before any visual change ships.
