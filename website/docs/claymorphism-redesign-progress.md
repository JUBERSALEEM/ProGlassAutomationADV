# Claymorphism Redesign — progress tracker

Owner loop: **Inspect → Implement → Build → Run → Screenshot → Compare →
Fix → Retest → Update progress → Continue.**
Spec authority: `docs/CLAYMORPHISM.md` + reference boards (img-1..8).
Engine: `src/app/clay-engine.css` (imported last, fully isolated to
`[data-appearance="clay"]`). Latest milestone: **§8 physicality + the midnight &
nav-interaction batch** (aurora whisper, guards rescoped, divide-line gap,
nav single-click pins).

---

## A. Environment reality (recorded honestly, never silently)

| Capability | Status |
|---|---|
| Browser automation / screenshot tooling (Playwright etc.) | **Not available in this sandbox.** No live render can be captured here. |
| What that constrains | Columns "Desktop QA / Mobile QA" below are *structural* audits (engine rule coverage of the classes a route actually writes + server-render smoke + budget audit), **not** pixel comparisons. Pixel review against the boards happens on the user's screen (live preview port 3000 / Vercel deployment). No route is claimed screenshot-verified; none will be without one. |
| Every-route smoke | `scripts/audit-site.py --budget` crawls all rendered routes per release: HTML validity, links, budgets, DOM weights. Latest: **0 failures / 0 warnings**. |
| Reference for screenshots the user sent | Vercel stamp `db03d88` = **pre-engine build**; the engine (`3f61ffe`) and the primitives below land after it. |

## B. Route inventory (discovered from `src/app/[locale]`, not assumed)

`#`=routes batched by shared template. All routes share the `[locale]`
layout (Navbar, Footer/band-deep, Menu sheet, SupportBubble, ThemeStudio).

| # | Route | Purpose | Engine coverage evidence | Desktop QA | Mobile QA | Visual | Status |
|---|-------|---------|--------------------------|------------|-----------|--------|--------|
| 1 | `/` | marketing home: hero, feature stack, story, StatusFlows, pricing, FAQ, CTA | product hero variants foiled; .surface/.stat-tile/glass-primary recipes; stat rotation; chrome + band | structural | structural | n/a (no render here) | engine-complete, pixel-review pending |
| 2 | `/features` | module catalog + service-file lanes | utility sweep (bg-ink/border foils), lanes re-voiced, tiles squircled | structural | structural | n/a | engine-complete, pixel-review pending |
| 3 | `/how-it-works` | workflow narrative | sweep + surfaces + chips | structural | structural | n/a | engine-complete, pixel-review pending |
| 4 | `/optimization` | engine deep-dive | sweep + surfaces | structural | structural | n/a | engine-complete, pixel-review pending |
| 5 | `/worked-example` | full walkthrough w/ pinned tables | sweep + table foils + buttons | structural | structural | n/a | engine-complete, pixel-review pending |
| 6 | `/pricing` | plans + FAQ | surfaces + coral CTAs + accordion | structural | structural | n/a | engine-complete, pixel-review pending |
| 7 | `/screenshots` | app screen gallery | frame/chrome recipes; images untouched (product truth) | structural | structural | n/a | engine-complete, pixel-review pending |
| 8 | `/about` | product story | sweep + surfaces | structural | structural | n/a | engine-complete, pixel-review pending |
| 9 | `/developer` | author story, paper trail | DeveloperStory plates + history chips | structural | structural | n/a | engine-complete, pixel-review pending |
| 10 | `/collaborate` | help/gaps, form | input wells + buttons + chips | structural | structural | n/a | engine-complete, pixel-review pending |
| 11 | `/contact` | contact cards | surfaces + buttons | structural | structural | n/a | engine-complete, pixel-review pending |
| 12 | `/sponsors` | sponsor tiers | surfaces + links | structural | structural | n/a | engine-complete, pixel-review pending |
| 13 | `/resources` | resource index | sweep + chips | structural | structural | n/a | engine-complete, pixel-review pending |
| 14 | `/blog` + `/blog/[slug]` ×3 | writing | serif reading face retained deliberately (voice, §6 note) | structural | structural | n/a | engine-complete, pixel-review pending |
| 15 | `/job-orders` + modal | records UI + detail dialog | scrim lilac, dialog surface+wells, state chips mint/lemon/coral | structural | structural | n/a | engine-complete, pixel-review pending |
| 16 | `/deliveries` + modal | tracking UI + dialog | ditto | structural | structural | n/a | engine-complete, pixel-review pending |
| 17 | `/records` | records hub | surfaces + chips | structural | structural | n/a | engine-complete, pixel-review pending |
| 18 | `/reports` | reporting overview | surfaces | structural | structural | n/a | engine-complete, pixel-review pending |
| 19 | `/legal/privacy`, `/legal/terms`, `/legal/license` | legal docs | pinned-copy tables foiled, legal-copy ink | structural | structural | n/a | engine-complete, pixel-review pending |
| 20 | `/error`, `not-found` templates | failure states | same shell + buttons | structural | structural | n/a | engine-complete, pixel-review pending |

**Totals: 25 rendered routes (per build manifest 399 localized pages),
0 pixel-verified (tool unavailable), 25 engine-complete.**

## C. Component primitives — status (Phase B)

| Primitive | Clay behavior | State |
|---|---|---|
| Design tokens (§0 channels, both modes) | spec verbatim incl. pink | ✅ |
| `.surface` / `.surface-raised` | full §4 recipe, hue-matched | ✅ |
| `.stat-tile` + pastel rotation (lav/mint/peach/sky) | saturated pastel metric cards | ✅ 3f61ffe+ac6 |
| `.glass-primary` feature plate | solid #B8A6FF, plum ink, white pill | ✅ |
| `.nav-glass` bar / `.nav-sheet` (mobile sheet) | white sheet / raised sheet | ✅ |
| `.band-deep` footer | lavender plate, plum ink | ✅ |
| `.btn-primary` | coral verb, sink on press | ✅ |
| `.btn-secondary` | white sheet, ¼ shadow | ✅ |
| inputs / textarea / select | pressed-in well, 16px | ✅ |
| Icon + Index chips (`ui/Badge.tsx` tiles) | squircle 20px, full recipe | ✅ |
| Brand-wash chips (bg-brand-*/12–16) | plum on lavender tint | ✅ |
| Active nav item (`aria-current`) | lavender pill bar + sheet | ✅ |
| Accordion | raised sheet + tinted open pin | ✅ |
| Badges (tones) | §4 semantic tints mint/lemon/coral | ✅ |
| Modals + scrim | lilac scrim + raised dialog | ✅ |
| Tables / pinned-copy | foil hairlines, violet pipes | ✅ |
| Scrollspy TOC | active lavender pill via wash rule | ✅ |
| Support bubble | brand accent retained (mono-accent ok) | ✅ |
| Serif pull-quote | intentional persona voice — kept, documented | 🟡 decision |
| Tailwind utility inventory reach | foils/hairlines/shadows swept site-wide | ✅ |

## D. Defect log

| # | Flow | Defect | Evidence | Fix |
|---|------|--------|----------|-----|
| D1 | earlier builds | brutalist hard-offset plate behind cards | screenshots/code | removed in db03d88 |
| D2 | db03d88 (user screenshots) | white-on-white insider cards, violet chrome slabs, un-clayed utility fills | Screenshots\_20261012\_0206\*\* | engine 3f61ffe floors all utilities |
| D3 | pre-primitives | 9.6px-radius icon tiles, metric cards monochromatic white, invisible active nav mark, pale expanded pin text | class audit | primitives layer shipped |
| D4 | open | pixel-verification gap (no browser here) | environment | needs on-device review; preview + Vercel |
| D5 | 40/100 review (user) | named-class registry baked ink fills beyond the bg-ink sweep: .menu-card, .plate, .float-card, .card-live, .card-glass, .band-solid, .band-rich, .panel-feature, .nav-pill, .spine-node, .mpane\_\_\*, .mat-rail\_\_chip | class-level grep of all baked backgrounds | §7.4: full registry on recipe |
| D6 | 40/100 review (user) | aurora wallpaper too loud for boards' calm ground; gradient clip-ink headlines; 2px strokes on the material; pastel rotation too faint | boards + tokens grep | §7.1–§7.5 |
| D7 | §7 | buttons w/o presence; status chips translucent ghosts | boards | §7.6–§7.7 |
| D8 | midnight screenshots 08:57 | aurora base token 0.5 alpha = lilac ocean behind midnight pages, dark cards on it | screenshots | §7.1 base → 0.10/0.05 |
| D9 | midnight screenshots 08:57 | daylight dark text lock unscoped → plum ink painted onto midnight sheets | guard audit | guards rescoped per mode, both extended to §7.4 registry |
| D10 | review | divide-line/divide-ink COLOR utilities missed by the border sweep (table stripes, accordion rules stayed gray) | class audit | added to hairline sweep |
| D11 | user report | desktop dropdowns: hover opened on enter, so the first click TOGGLED SHUT what hover opened — read as "needs double click"; no outside-close | Navbar.tsx hover/click conflict | pinnedMenu ref: click pins, hover-out never closes pinned, pointer-down outside + Escape + route-change clear |
| D12 | "didn't felt the engine" | TWO causes: (a) the shipped DEFAULT family was crystal — a returning visitor's stored choice also outranked clay, so pages were judged without ever reaching the engine; (b) stale studio dials could outrank family recipe. | boot audit | DEFAULT_APPEARANCE + boot fallback = clay; hygiene v2 migration runs before resolution: wipes stored studio vars/attrs AND seeds stored appearance = clay once; palette picker still freely switches away |

## E. Current gate results (this build)

- tsc 0 · eslint 0 · `test:job-orders` 8/8 · build 399/399
- audit 0 failures / 0 warnings; css budget re-measured per trail: 27.0 → 27.9 (engine) → 28.2 (primitives) → see commit message of the §7 pass for current

## F. Next exact tasks (resume here)

1. **Pixel pass with the user**: Daylight + Midnight, all 25 routes,
   log any non-clay card by route in this file's section D, fix at the
   engine, re-tail audit.
2. **Watch remaining candidates**: `bg-brand-400/8,10` ultra-faint washes,
   `PreviewShell` bubble-input styling vs clay family, hero orb clamps.
3. **If user adds state colors**: map to §4 semantic tints.
