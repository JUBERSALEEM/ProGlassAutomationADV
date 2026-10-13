# Website Design System — the one visual language

> Established: Phase 2, 2026-10-13 · Source of truth in code: `src/app/globals.css` (tokens + core grammar), `src/app/crystal-engine.css` (Crystal glass), `src/app/clay-engine.css` (Clay), `tailwind.config.ts` (token bridge), `src/components/ui/*` (primitives). This document names the system; it never overrides the code.

## 1. Principles

1. **Components declare intent; engines own material.** Pages say `.surface`, `GlassCard tier="…"`, `Button variant="…"` — never colour, blur, or shadow. The active family decides what that *is*.
2. **Text is mode-scoped, material is family-scoped.** `--text-1…4` are written only under `[data-theme]`; no family file may write `--text-*` (enforced: `crystal-contrast` G4·text, clay-paint isolation probes).
3. **Fills carry text; blur is frosting.** Daylight glass fills ≥ the G4 floors; every translucent surface ships an `@supports not (backdrop-filter)` lane where fills rise and gradients drop.
4. **One primary per section.** `tier="primary"` / `.glass-primary` is the section's subject; everything else is quieter. A page where every card shouts has no hierarchy.
5. **Decorative motion animates `transform`/`opacity` only** (documented ban list in `README.md`: no full-viewport `mix-blend-mode`, no `@property` gradient animation, no wide `filter: blur()`).

## 2. Token map (semantic, by job)

| Group | Tokens (in `globals.css` `:root` / `[data-theme]`) | Consumed via |
|---|---|---|
| Surfaces | `--ink-950…700` (5 steps, page→highest) | Tailwind `ink-*` |
| Lines | `--line`, `--line-soft`, `--line-strong` | `line`, `line-soft/strong` |
| Brand | `--brand-50…700` (violet; `#6546B5` = app `Sp.Accent`) | `brand-*` |
| Glass accents | `--glass-teal/cyan/green/rose` (sparing, glass content only) | `glass-*` |
| Text | `--text-1…4` (measured 21.0→9.4 : 1 on card) | `text-primary/secondary/muted/faint` |
| State/money | `--state-success/warning/danger/info`; `--money-positive/negative/neutral` | `state-*`, `money-*` |
| Surface tiers | `--surface-alpha{,-2,-3}` (0.97/0.95/0.99), `--surface-blur`, `--surface-sat`, `--surface-specular` | surface classes (below) |
| Glass depth | `--glass-hi/lo/sheen/edge-light/edge-dark/ring/line/scrim/frost`, `--glass-primary-tint/glow` | Crystal engine + core grammar |
| Geometry | `--card-radius{,-large}` (12/16px × `--radius-scale`), `--card-hairline` 1.5px, `--card-edge-width` 2px, `--card-pad{,-sm}` | `rounded-card/panel/hero`, `.card-pad` |
| Elevation | `--shadow-panel/raised/glow/glow-strong` (+ Crystal `--glass-drop`) | `shadow-panel/raised/glow…` |
| Wallpaper | `--wall-*`, `--pool-1…3`, `--aurora-*`, `--sweep-*` | `body::after`, `.aurora-field`, `.orb` |
| **Motion (Phase 2)** | `--dur-press` 140ms · `--dur-hover` 200ms · `--dur-card` 220ms · `--dur-lift` 280ms · `--dur-theme` 260ms · `--dur-reveal` 380ms · `--dur-reveal-scale` 340ms · `--dur-settle` 550ms; `--ease-standard` (0.22,0.61,0.36,1) · `--ease-out` (0.16,1,0.3,1) · `--ease-spring` (0.3,0.1,0.4,1.4) | Adopted so far: `.reveal`, `.reveal-scale`, `.card-interactive`, theme-switch cross-fade, Crystal liquid press (§F2) — all at values identical to the pre-token literals. New motion **must** use these (Phase 3 completes adoption) |
| Interaction states | Focus = single 2px ring grammar (`ring-brand-400` + family focus lips); hover/active/disabled defined per primitive (below) | `Button`, `.card-interactive`, nav, forms |

## 3. Surface ladder (the hierarchy every route shares)

| Tier | Classes / primitive | Job | Fill (default family) | Under Crystal |
|---|---|---|---|---|
| Primary | `.glass-primary` · `GlassCard tier="primary"` | The one subject card per section | `--surface-alpha-2` + primary tint/glow | Blur + rim + bevel + `--glass-drop` |
| Raised | `.surface-raised`, `.plate` · `tier="raised"` | Panel over tier 1 | `--surface-alpha-2` | Same glass, quieter |
| Card | `.surface`, `.stat-tile` · `tier="card"` | Normal content card | `--surface-alpha` | Same glass |
| Floating | `.float-card` · `tier="floating"` *(Phase 2)* | Small panel over artwork/another surface | Opaque `ink-900` | `--surface-alpha-2` + 16px blur |
| Reading | `.card-live`, `.menu-card` | Live previews, menus, tables — legibility first | `--surface-alpha-3` (0.99) | Near-solid by doctrine; excluded from tier blur |
| Wells | inputs/`.field` carriers | Receive text/caret | Family well grammar (Crystal §C2) | Frosted well + focus lip |

## 4. Families × modes

- **Modes**: `midnight` (dark) / `daylight` (light) via `data-theme`; mode control = Light·Dark·System.
- **Families** (`data-appearance`): `studio` (site violet), `monet`, `industrial`, `crystal` (glass), `clay` (moulded pastel, **current first-run default** — Gate G-A unapproved, Phase 2 proceeded opt-in). Family isolation is harness-proven; an engine file never edits another family's material.
- **Studio dials** (Appearance Studio): accent ramp, tint multiplier, radius scale, edge weight/colour, typeface, money colours, Crystal material bench (frost/body/glint multipliers — floored/capped in §C4). All writes are CSS variables on `<html>`; storage-hygiene versioned.

## 5. Primitives (`src/components/ui/`)

`Button`/`ButtonLink` (primary/secondary/ghost/quiet × sm/md/lg; primary fill = `brand-600` for measured 5.3–9.6:1 white contrast) · `GlassCard` (primary/raised/card/**floating**) + `GlassMetric/Row/Divider` · `Reveal` (up/scale/none; one shared observer) · `Spotlight` (pointer light, hover+fine only) · `Accordion` · `Badge/IconChip/IndexChip` · `Stat/StatCounter/StatBadge` · `SectionHeading` · `Container/Section` · `Slider` · `Marquee` · `Icon` · `LocaleLink`. Layout: `Navbar`, `Footer`, `PageHero`+`PageBanner` (12 banner arts), `LanguageMenu`, `ThemeStudio`, `BuildStamp`, `LegalPage`.

**States**: hover = edge/light change (no content movement except the gated −3px card lift); press = `scale(0.985)` (Crystal liquid press, tokenised); focus-visible = the one ring; disabled = 55% opacity + no pointer affordance; every animated state has a `prefers-reduced-motion` lane where content is simply present.

## 6. Change rules

- New surface ⇒ token first, class second, catalogue entry (`/sys/clay` or `/sys/crystal`) third, contrast pair fourth. No catalogue+harness ⇒ it does not ship.
- New duration/easing ⇒ use §2 motion tokens; a new value needs a named token + a line in this file.
- Ceilings (`WEBSITE_BASELINE.md` §4, blur census 600) are part of the design system: exceeding one is a design change requiring written justification.
