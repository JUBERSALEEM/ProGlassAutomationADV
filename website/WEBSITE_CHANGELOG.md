# Website Changelog — Premium Glass & Motion Programme (Phases 0–10)

All work 2026-10-13, on branch `arena/51889ca3-proglassautomationadv`, working tree
uncommitted on top of `d3469e4`. Production still serves `1de50cd` on
`arena/ff328a9c-proglassautomationadv` — nothing below is deployed yet.

## Phase 3 — Motion architecture (Gate G-B + G-C approved by product owner)
- Installed `motion@14.2.0` (`motion/react`). Added `src/lib/motion.ts` (JS mirror of the
  CSS `--dur-*`/`--ease-*` tokens + springs) and a contract test
  (`scripts/motion-tokens.test.mjs`, 2/2) so the two token sources cannot drift.
- `src/components/motion/MotionReveal.tsx` — motion-lane scroll reveal with identical
  grammar to the CSS `Reveal`; reduced-motion renders content simply present.
- Homepage hero: 7-beat sequenced entrance (left column staggered, product visual scaled).
- `/features` ModuleBrowser: filter changes now animate (layout spring + AnimatePresence
  popLayout); reduced-motion degrades to opacity-only.
- `/optimization` PlateInspector: keyed sheet-focus transition on plate change.
- `PageHero` gained `variant: "editorial" | "document" | "showcase"`; assigned to
  features/optimization/screenshots (showcase) and blog/resources (document).
- Budget: emitted JS 493.6K → 543.9K gz (shared motion chunk). Ceiling re-justified
  540K → 560K in `scripts/audit-site.py` with dated measured trail. Homepage first-load
  352.3K vs 360K ceiling.
- Deliberately skipped: route page-transitions (would risk native navigation/back-button
  behaviour for no proven gain); Accordion left on its CSS grid-rows animation.

## Phase 4 — Scroll, pointer & fluidity
- Safe-area insets on both fixed launchers (SupportBubble, ThemeStudio trigger) via
  `max(1.25rem, env(safe-area-inset-*))` — the audit found zero `env()` usage.
- Audit-verified with no change needed: native scroll only (2 passive listeners),
  reduced-motion-gated smooth scroll, global anchor offset, zero layout-property
  keyframes, no-hover menu operation.

## Phase 5 — Route hierarchy completion
- PageHero variants extended: pricing/how-it-works/worked-example → showcase;
  about/contact/collaborate → document. All 11 variant assignments verified in
  rendered HTML (E2). Dashboards (deliveries/job-orders/records/reports), legal
  pages, blog articles and logins verified as correctly bespoke — unchanged.

## Phase 6 — Content & conversion
- No code change (audit outcome). Conversion crawl: every marketing route carries
  3–12 working `/contact` (demo) CTAs; claims scan clean (no invented testimonials
  or metrics); demo data labelled as the seeded demonstration job.

## Phase 7 — Performance engineering
- LazyMotion experiment run, measured, and **rejected**: emitted JS 543.9K → 552.9K,
  homepage first-load 352.3K → 355.9K. Reverted in full; final numbers identical to
  Phase 3. (During the revert, `git checkout` briefly destroyed the uncommitted
  Phase 3 edits in ModuleBrowser/PlateInspector; both were re-applied from the
  Phase 3 record and re-verified — see WEBSITE_PHASE_PLAN.md Phase 7 report.)
- HTML ceilings for /how-it-works (57→58K) and /collaborate (42→43K) re-justified
  with measured trail (+0.1K of Phase 5 hero-variant class tokens).

## Phase 8 — Accessibility & fallbacks
- Fixed a real defect: without JavaScript the site rendered blank (`.reveal`
  opacity 0, MotionReveal hidden inline state, accordion panels at 0fr). Added a
  `<noscript>` resilience sheet in the locale layout + marker classes
  (`motion-reveal`, `accordion-panel`). Zero cost with JS enabled.

## Phases 0–2, 9–10
- Phase 0: baseline/audit docs. Phase 1: design/experience audit + lint fixes.
  Phase 2: motion design tokens, `Glass` floating tier, design-system document.
- Phase 9: full regression sweep (see WEBSITE_FINAL_AUDIT.md). Phase 10: these documents.


## Post-programme fix — 16:54 screenshots (dark slabs, black/grey text)
- **Root cause**: `useStudio.ts` rewrote `--clay-foil` (the paint for every
  `bg-ink-*` utility carrier in Clay) twisted from the *midnight* base
  `46 34 62` even in Daylight. Anyone who had picked a ThemeStudio accent got
  dark-maroon utility panes under Daylight's plum ink — measured 1.0–1.6:1 on
  the P&L bars, stage status chips and sample tags. Default (no accent picked)
  was never affected, which is why every default-state harness passed.
- **Fix**: the foil twist now starts from the per-mode base
  (`46 34 62` midnight / `239 234 252` daylight, the engine's own §0 values),
  matching the `CLAY_CARD_BASE[target]` contract beside it. Stored studio
  states self-heal on next load (vars are recomputed and rewritten).
- **Also fixed**: micro-labels on pastel cards at 3.87:1 (`Metric` atom +
  Profit & Loss card labels, `text-text-faint` → `text-text-muted`).
- **Regression gate**: 4 new probes in `scripts/clay-paint.test.mjs` pin the
  studio-foil contract at the values the studio writes, per mode (179/179).


## Post-programme fix — 17:21 witness (engineering-metrics tiles)
- **Cards question answered**: the four metric tiles were never missing — each
  is a separate `.stat-tile` in a `gap-4` grid. Outer glow bridged the gaps and
  pale ink washed the edges out, so the stack read as one glowing column.
- **Glow trimmed** (clay stat-tile rotation): outer colour bleed 0.30→0.20 /
  blur 22→14px by day, 0.16→0.12 at midnight; the white rim lamp 0.85→0.50.
  Sculpted insets untouched. Tiles now read as separate rounded cards.
- **Figure ink**: `StatCounter`'s number carried no text class and inherited
  page colour (white at midnight, on pastel) — it now carries
  `text-text-primary` like `StatBadge` always did, so tile ink locks govern it.
- **Crystal midnight**: stat tiles take a solid ink fill (`--ink-900` at 0.72)
  under their glass ring — bright wallpaper pools can no longer lighten the
  text field behind white figures.
- **Regression gate**: StatCounter-realistic probes (both modes + crystal
  ink-fill pair) added to `scripts/clay-paint.test.mjs`.

## Known open items (handover)
- `i18n:verify`: 8 locales marked complete at ~75% key coverage (pre-existing;
  zero new strings added by this programme). Translation effort, not a code fix.
- E3 witness screenshots: no browser exists in the build sandbox; the visual QA
  rows marked witness-required need captures from the deployed preview.
- Gate G-A (Crystal as first-run default) was never approved; Clay remains default.

### Post-programme fix — full /developer dynamic rewrite (2026-10-13)

User verdict on the interim stat-tile patch: text still invisible; instruction —
remove ALL hardcoded/custom card colours on /developer, make every section
dynamic like the rest of the site, full page upgrade not patches.

- `src/app/[locale]/developer/page.tsx`: every custom-coloured card removed —
  11 `.stat-tile` pastel tiles, 17 `.card-live` cards, the `.glass-primary`
  dream slab, the `.card-accent` closing slab, and the hardcoded brand-gradient
  avatar chip. All now wear the family's standard dynamic recipes
  (`.surface` / `.surface-raised` + token text classes), identical in language
  to the rest of the site in every appearance × mode.
- `src/components/ui/Stat.tsx`: `StatCounter`/`StatBadge` gained
  `variant="surface"` (standard raised card, count-up unchanged); the developer
  page uses it for all metric cards. Default `tile` variant untouched, so no
  other page changed.
- Verification (computed, E1): 11 new permanent probes in
  `scripts/clay-paint.test.mjs` measure the page's real shipped chains on
  `.surface`/`.surface-raised` in all appearances × modes — all pass
  (metric figure 12.58:1 midnight / 10.03:1 daylight); clay-paint 207/207.
  Served-HTML check: zero `stat-tile|card-live|card-accent|glass-primary`
  strings on /en-US/developer. Gates: lint, typecheck, build 429/429,
  clay, crystal, blur census, pixel-pass, vitest, audit-site 0/0 + budget.
- Still E3-pending: the user's own browser witness of the rebuilt page.
  Production still serves the pre-programme revision until the owner deploys.

### Post-programme fix — site-wide dynamic card pass (2026-10-13)

Same instruction as the /developer rewrite, applied to every page: all 19
content routes were read, and every custom-coloured card class was removed
from pages and shared components:
- `stat-tile` retired entirely: `StatCounter`/`StatBadge` now always render
  `.surface-raised` (the component-level `variant` prop was removed; the
  developer page's seven `variant="surface"` props went with it).
- `GlassCard tier="primary"` (`glass-primary`) now maps to `.surface-raised`;
  `glass-primary-head` became a token hairline (`border-b border-line/60`).
- All raw `glass-primary` panels (the closing CTA on ~12 pages, pricing's
  intro panel, collaborate's two panels), all `card-accent` slabs (home,
  collaborate, sponsors, legal layout, error page, LiveSurface), all `plate`
  cards (features ×4, worked-example ×3, blog lead, AppFrame/ScreenshotFrame
  frames, ModuleShowcase, optimization pipeline, sponsors), all `card-live`
  (ModuleShowcase, LiveSurface), the homepage facts-strip `plate-1..4`, the
  how-it-works rotating `plate-N` chain index, and both `panel-feature`
  closing panels (home CTA, DeveloperStory) → `.surface` / `.surface-raised`.
- Verification: grep over src/app + src/components shows zero remaining
  usages outside `/sys/clay` + `/sys/crystal` (design-system specimen pages,
  whose purpose is displaying the finishes) and the ThemeStudio
  `AppearancePreview` sampler (same reason); CSS class definitions remain
  for those specimens. Served-HTML sweep of 17 routes: clean. clay-paint
  probes re-chained to the shipped surface-raised markup (207/207). Gates:
  lint, typecheck, build 429/429, clay, crystal, census, pixel-pass, vitest,
  audit-site 0/0 incl. budget (CSS 38.8K/39K).
- Still E3-pending: user's browser witness. Production unchanged until the
  owner commits and merges this branch.
