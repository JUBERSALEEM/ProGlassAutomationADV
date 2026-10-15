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
- **Phase 2 — shared-pattern normalization.** Extract the repeated
  CTA banner + repeated `p-8` feature panels into the existing
  primitives (Glass/SectionHeading), migrating raw `p-8`→`card-pad`,
  `p-6`→`card-pad-sm` on registered cards page-group by page-group.
- **Phase 3 — placement pass per page group** (home → features →
  dashboards (records/reports/job-orders/deliveries) → pricing/
  content pages): grid gaps 24→20/16 where cards are siblings,
  heading/lead spacing inside sections, sidebar+content density
  like the reference dashboards.
- **Phase 4 — dashboard density**: summary cards, filter panels,
  tables (row padding), modals — the app-like pages where the
  reference look matters most.
- **Phase 5 — verification & polish**: full suites (clay/crystal
  paint, contrast, coverage, nav, typecheck, lint), responsive
  spot-checks at 360/768/1280/1920, before/after measurements
  recorded here, preview sign-off per page group.

## Acceptance

Cards visibly tighter with breathing room preserved; no content
touching corner curves; no contrast regression (all paint suites
green); native scroll and reveal behaviour untouched; radius and
family identity unchanged.
