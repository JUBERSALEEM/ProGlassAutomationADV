# Claymorphism Baseline Audit — evidence before editing

Executed: 2026-10-13 · HEAD `bd2d3f5` · branch arena/ff328a9c-proglassautomationadv

## Commands executed (actual results)

- `find src/app/[locale] -name page.tsx | wc -l` → **26 routes** (about, blog(+slug), collaborate, contact, deliveries, developer, features, how-it-works, job-orders, legal×3, optimization, pricing, records(+login), reports(+login), resources, screenshots, sponsors, sys/clay, sys/crystal, worked-example).
- Class census (TSX): `rounded-xl` 103 · `rounded-lg` 92 · `rounded-card` 69 · `rounded-2xl` 53 · `rounded-md` 36 · `rounded-panel` 32 · tone utilities `bg-ink-900` 166 · `bg-ink-950` 137 · `bg-ink-800` 63 · `bg-ink-850` 60 · `bg-brand-*` 131 · `bg-line-strong` 40 · state colors 49.
- Hard-coded color scan (TSX, hex6): `#FFFFFF` ×10 (og/route.tsx ImageResponse + SvgDefs white stop-opacity + ThemeStudio accent plate registry) — **all legitimate** (social-card SVG + the accent plates themselves); `#F27EA2/#1AC5E2/#FBBF24` = accent plate hexes; `#8B7CF0/#6546B5/#0369A1` family accents → inventory CLEAN; zero unexplained presentation hexes in TSX.
- css hex6: globals 78 (token tables, expected) · clay-engine 19 (token defaults) · crystal-engine 0 ✓ (all tokens).
- rgba/literal-white sites in clay-engine: 31 — all inside token declarations or documented rule comments (§19–22 ladder + scrims).
- Browser-automation: Playwright 1.64 fetched; **chromium binary cannot persist** (sandbox wipes `~/.cache/*` between turns; `cdn.playwright.dev` downloads start but the headless shell does not survive). **Limitation recorded.** Substitute evidence tools executed against the built site over HTTP: `scripts/clay-paint.test.mjs` (175 checks), `scripts/crystal-contrast.test.mjs` (87 pairs), `scripts/crystal-blur-census.mjs` (522 surfaces across 23 pages), `scripts/audit-site.py` (link/content audit), vitest 35.
- Visual witness channel: user-deployed screenshots (10:42 blue-family shots) — the only true pixel evidence available; logged as required input after each deploy.

## Component→token map (verified in clay-engine.css)

band-solid → --band-face · card-accent/panel-feature → --panel-face · surface-raised, card-live, float-card, card-glass, plate, mpane panes, all rounded-utility panes (§21 door) → --card-face · surface/stat-tile → --data-face · menu-card → --overlay-face · nav chrome → --clay-card · eyebeams: --plate-1..6 rotation inside registry rows (+ noria sets) · divide-y rows → plate alpha washes · shadows → --clay-drop-h/--clay-hi/--clay-lo · interior wash → --card-tint-h(2)/--clay-wash · midnight overrides → §22.1 trio + channels.

## Defects found at baseline

1. **D1 — pale-step identity crisis:** data-face (L≈0.90, s≈0.35-family) and overlay steps read as "white cards with blue shadows" on accent-forward pages (user screenshots 10:42). Root: ladder raises LIGHTNESS without preserving SATURATION.
2. **D2 — no formal elevation tiers:** shadows hand-coded per class (card-accent 12px/30px/38%, surfaces var(--shadow-raised)); no single light variable (codefronts discipline).
3. **D3 — dark-mode inset strength:** midnight keeps daylight inset intensities (--clay-hi/--clay-lo literals) → plastic glare risk on dark pastel.
4. **D4 — press/focus grammar unverified:** transform-only press + 3:1-visible focus rings are unasserted across clay controls (buttons/links/menus).
5. **D5 — route coverage ≥ 23 pages measured only by census/audit; no per-route pixel matrix** (limitation: no browser).
6. **D6 — brand-500 chips (bg-brand-400 ×87) on clay:** semantic chips correctly stay out of the card ladder (exception), but verify they don't read as white cards at blue accent (screenshots show pills reading pale).

## Accepted, done-at-baseline (prior verified work now in history)

- §19–22 wiring: card/data/overlay tokens + §21 door (rounded-3xl/lg) + §22 shipped-pastel defaults + adoption-only bench (custom support excised) — commits up to bd2d3f5.
