# Card Compaction — Audit & Phased Plan (§AG)

User decree: cards across the site read too big and cover too much
area; tighten them with proper breathing, improve placement, make
every page read professional (reference: compact clay dashboards —
16–20px card padding, 16–20px gutters, small chips, dense but airy).

## Audit findings (measured, not eyeballed)

1. **The card-padding token is bypassed.** `--card-pad` (24px) /
   `--card-pad-sm` (16px) exist, plus a `data-ui-density="compact"`
   mode — but `card-pad` is used only 9 times in the codebase,
   while raw utilities decide padding per component: `p-5` ×111,
   `p-6` ×111, `p-8` ×41, CTA banners `p-8 sm:p-10` (32/40px)
   repeated across ~8 pages. Density is therefore inconsistent
   page to page: same card, different bulk.
2. **Section rhythm dominates the pages.** `.section` is
   `py-14 sm:py-20 lg:py-24 2xl:py-28` (up to 112px top+bottom).
   Stacked sections, not cards alone, are why pages feel like a
   few giant cards floating in space.
3. **Page-level gaps are large** (`gap-10 lg:gap-16` split layouts,
   `gap-12`), i.e. the air sits between layout columns instead of
   breathing inside cards. (Deliberately NOT blanket-scaled — a
   recorded decision in globals.css warns grid gaps carry
   deliberate proportions; Phase 3 treats grids per page.)
4. **Family radius multiplies the effect.** Crystal ×1.5 (card
   24px / panel 30px), Clay ×2 (32/40px): the corner-safe system
   then forces up to ~23px of inline inset inside Clay panels, so
   even a "p-5" Clay card eats content width. Radius itself is
   family identity and stays; padding must compensate.
5. **Dead token:** `--shell-max: 84rem` is declared but `.shell`
   is full-bleed — no action, noted for honesty.
6. **Duplication:** the end-of-page CTA banner
   (`surface-raised … p-8 sm:p-10`) is copy-pasted across about,
   blog, collaborate, contact, features, how-it-works and more —
   one shared component would end the drift (Phase 2).
7. Pre-existing, disclosed last turn and untouched:
   `scripts/measure-panel.py` disagrees with Navbar.tsx over a
   literal href pattern (width model only).
8. **Bug found & fixed during Phase 1:** the §AF sliding-pill
   handlers were built by a getter called during render
   (`slidePillHandlers(() => ref.current)`), which the React
   Compiler lint (react-hooks/refs) rejects. The handlers now take
   the list from `event.currentTarget` — same behaviour, no ref
   read at render. (Lint had printed this under a passing tail in
   the §AF verification; it is gated explicitly now.)

## Phases

- **Phase 1 — global tightening (tokens + rhythm).** ✔ APPLIED
  in this change: `--card-pad-base` 1.5rem→1.25rem,
  `--card-pad-sm-base` 1rem→0.875rem (phone: 1.15→1.05rem,
  0.85→0.8rem); `.section` one step down
  (`py-12 sm:py-16 lg:py-20 2xl:py-24`). One edit tightens every
  `card-pad` surface (Glass primitive, ModuleShowcase, dashboards'
  summary cards) and every section on all pages; corner-safe
  floors still guarantee content clears the corner curve in
  Crystal (24px radius) and Clay (32/40px).
- **Phase 2 — shared-pattern normalization.** ✔ APPLIED
  `CallToActionCard` is a server-only shell composed from `GlassCard`;
  twelve route-specific closing CTAs now share its family-aware
  surface, `card-pad`, and 20px vertical rhythm while retaining
  route-owned copy, links, IDs, and surrounding Reveal/Section markup.
  A code-aware migration moved 63 registered card surfaces off raw
  `p-6` / `p-8` utility padding: simple cards use `card-pad-sm`,
  fuller/responsive panels use `card-pad`. Responsive p-6→p-7/8 and
  p-8→p-10/12 cases resolve to the full card token, avoiding an
  unintended extra-small treatment. The pricing edition's conditional
  class strings were checked and migrated separately.
  Before → after: 63 registered surfaces with raw p-6/p-8 → 0;
  69 `card-pad`/`card-pad-sm` class occurrences; 12 CTA component uses.
  The existing corner-radius guard and paint suites remain green.
  Non-registered custom panels and dashboard modal/loading shells are
  retained for their appropriate placement/dashboard passes.
- **Phase 3 — placement pass per page group.** ✔ APPLIED
  Home, features, pricing/content, shared heroes/footer and gallery were
  walked as page groups. Two-track split gutters now top out at 32px base /
  40px desktop (formerly 40–64px); sibling-card gutters top out at 20px
  (formerly up to 32px); heading→content offsets top out at 32px
  (formerly 40–56px). The product showcase remains a deliberate 40px
  summary/visual split, not a sibling-card row.
- **Phase 4 — dashboard density.** ✔ APPLIED
  Records, deliveries, job orders, reports and shared operator surfaces
  moved one spacing step tighter: summary grids 14→12px, tiles 16→14px,
  filter/modal shells one padding step down, dashboard stacks 24→16px,
  table defaults 14→10px block padding and compact rows 8→6px. Horizontal
  cell padding and all touch/control heights were retained for legibility.
- **Phase 5 — verification & polish.** ✔ COMPLETE (source/computed/build)
  Permanent `card-compaction-census.mjs` + `npm run test:density` hold the
  measured placement and corner contracts. Full build, lint, typecheck,
  Vitest, Clay/Crystal paint/motion/contrast, blur census, pixel pass,
  pointer guard and site audit are green.
- **Phase 6 — responsive balance & final hardening.** ✔ APPLIED
  Cross-check added after the owner recalled a sixth phase. The earlier
  desktop pass still left stacked split layouts at a 32px mobile/tablet
  gap; these now use 24px before opening to the deliberate 40px desktop
  split. Six remaining non-registry panel cards (global error, three
  operator sign-ins, reports auth, feature-showcase plate) moved from raw
  24–36px padding to the same radius-safe card token as the site. The
  permanent census now measures this sixth responsive condition. Browser
  pixel witness at 360/768/1280/1920 remains a preview/user check; no
  screenshot claim is made from this sandbox.

## Phase 2 execution record

- **Root cause:** the global pad variables only affect callers of the
  `card-pad` classes. Registered cards with utility padding were held at
  the utility's 24–48px values by the corner-safe override; the repeated
  CTA shell independently carried 32–40px padding and a 24px vertical gap.
- **Files changed:** 22 existing route/component files plus the new
  `src/components/ui/CallToActionCard.tsx`. Twelve public route CTAs now
  use the shared shell; all 63 registered-surface p-6/p-8 cases use the
  padding token appropriate to their content hierarchy. No route copy,
  href, section label, CTA heading ID, or reveal wrapper changed.
- **Before → after:** registered surface class census, raw p-6/p-8: 63 → 0.
  Token class census after migration: 69 direct registered-surface
  className occurrences (the `GlassCard` primitive also adds its token
  internally). CTA
  shell gap: 24px → 20px. Compiled CSS contains
  `.card-pad{padding:var(--card-pad)}` and
  `.card-pad-sm{padding:var(--card-pad-sm)}`; the tokens retain their
  responsive radius floors. This is computed/structural evidence, not a
  visual or pixel-proof claim.
- **Verification:** production build generated 444 locale/static pages;
  typecheck and ESLint clean; Clay paint 253/253; Clay contrast/visibility
  33/33 + coverage 100%; Crystal contrast 114/114; motion 28/28; navbar
  98/98; radius guard 12/12; pointer guard scans 166 files clean.
  Coverage gap found and closed during this check: the radius guard had
  asserted the full `card-pad` floor but not the `card-pad-sm` floor used
  by the compact cards; it now asserts the compact formula and that its
  utility applies the token. Guard remains 12/12.
- **Preview:** production server refreshed; all 26 checked route paths
  return HTTP 200, including home, all public content routes, records /
  reports logins, and the three `/sys/*` benches. Server-rendered HTML
  for the About closing CTA confirms `surface-raised card-pad text-center`
  and the shared `gap-5` body class. No pixel-level browser proof is
  claimed.
- **Remaining:** custom non-registry panels (including app loading/error
  shells, feature-showcase stage and detail modals) stay for their
  appropriate placement/dashboard passes. No other Phase 2 defect found.

## Phases 3–6 execution record

- **Before → after, permanent census:** two-track gutters ≥48px **40→0**;
  sibling-card gutters ≥24px **41→0**; heading→content offsets ≥40px
  **45→0**; registered surfaces with raw `p-6..p-12` **1→0** (the legal
  closing panel joined `card-pad`). Operator-table comfortable row padding
  **14→10px**, compact **8→6px**. The census is source evidence, not a
  screenshot or visual-proof claim.
- **Files changed:** 21 marketing/shared layout files in Phase 3; 29
  records/deliveries/job-orders/reports/operator files in Phase 4. Copy,
  links, data flow, auth, filtering, exports, state, control sizes, radius,
  surface paint and motion logic are unchanged.
- **Audit defect found and fixed:** the first-load JS regex stopped at `.js`
  and therefore matched no Turbopack URL carrying `?dpl=…`; every page had
  falsely reported `0 chunks / 0.0K`. It now strips the query and measures
  the actual 20–21 chunks. The repaired baseline is 361.7K on `/`, 364.2K
  on `/screenshots`; the honest ceiling is 370K. The existing Next 16.3
  build emits 48.9K CSS against a stale 39K ceiling even before this
  markup-only pass; that ceiling is re-trailed to 50K (1.1K headroom).
- **Verification (2026-10-15):** production build 444 generated locale
  routes; typecheck and ESLint clean; Vitest 98/98; radius 12/12; density
  census 5/5; Clay contrast/visibility 33/33 + coverage 100% + paint
  253/253; Crystal contrast 114/114; motion 28/28; pointer guard 166 files;
  pixel pass 28 routes / 0 raw; blur census worst `/features` 531/600;
  site audit 0 failures / 0 warnings including measured budgets.
- **Phase 6 cross-check:** stacked responsive split gaps ≥32px **40→0**;
  all production split stacks now use 24px before their desktop breakpoint.
  The raw-padding registry was widened to include rounded panels and the
  feature-showcase plate; six remaining production panels now use
  `card-pad`. `/sys/*` specimens remain deliberately exempt because their
  purpose is to display raw finish recipes.
- **Preview:** the production build is running on the live preview. Pixel
  sign-off at 360/768/1280/1920 is still witness-required; only structural,
  computed-paint and served-route evidence is claimed here.

## Acceptance

Cards visibly tighter with breathing room preserved; no content
touching corner curves; no contrast regression (all paint suites
green); native scroll and reveal behaviour untouched; radius and
family identity unchanged.
