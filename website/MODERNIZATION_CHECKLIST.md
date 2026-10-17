# ProGlass Automation 2026 Modernization Checklist

**Scope:** `website/` only  
**Audit baseline:** 2026-10-18 (Asia/Dubai)  
**Branch:** `arena/23bacfef-proglassautomationadv`  
**Baseline commit:** `6f73f1f`  
**Status:** Phase 10.1 audit and Phase 10.2 redesign foundation complete. Phases 10.3–10.10 are being reapplied as full redesign phases with one commit per phase; browser, authenticated-live-state, and localization-completeness gates remain open.

This is the evidence ledger for the full-site modernization. A checkbox is marked complete only when its stated verification was actually performed. Generated pages are not counted as unique designs, and development evidence is not represented as field Core Web Vitals.

## Ground rules

- Preserve routes, content, 15 locales, RTL behavior, forms, authentication, data/API integrations, SEO, Vercel configuration, and security boundaries.
- Reuse and improve the existing component and token infrastructure; do not create a competing design system.
- Keep operational pages dense, legible, and task-oriented. Do not apply decorative motion to business data.
- Motion is one-shot by default, capped, transform/opacity-led, reduced-motion aware, and must never make content depend on JavaScript visibility.
- Preserve keyboard operation, focus management, Escape behavior, semantic names/relationships, history, query parameters, and locale routing.
- Do not alter the WPF application, apply migrations, publish production data, or deploy production.
- Phase 9E Windows live verification remains deferred until 2026-10-19 08:00 Asia/Dubai. Phase 11 production coordination remains blocked.

## Phase 10.1 — repository and baseline audit

### Architecture and inventory

- [x] Confirm repository and branch; restrict scope to `website/`.
- [x] Inspect package scripts and framework dependencies.
- [x] Count route source templates: **35 page templates** under `src/app/[locale]`.
- [x] Count API source templates: **27 `route.ts` files** under `src/app/api` (the build additionally reports `/api/og`, implemented outside that `route.ts` count).
- [x] Count locales: **15** (`en-US` plus 14 localized URL variants); Arabic and Urdu are RTL.
- [x] Distinguish template count from build expansion: baseline production build generated **504/504 static pages**, not 504 unique designs.
- [x] Inspect shared locale layout, route template, global styles/tokens, reveal utilities, and Motion tokens.
- [x] Identify shared loading/error/not-found boundaries at `[locale]` scope.
- [x] Identify metadata, sitemap, robots, and dynamic blog static-parameter generation.
- [x] Inventory component families: layout/navigation/footer, UI primitives, forms, records, reports, deliveries, job orders, inquiries, optimization, product, gallery, theme studio, and motion.
- [x] Inspect locale negotiation: remembered cookie → country → `Accept-Language` → English; English uses an internal rewrite while non-English uses visible redirects; query strings are retained.
- [x] Inspect representative interactive semantics in source and automated coverage (menus, accordion, dialogs/sheets, tabs, forms, tables, pagination, theme studio, and support controls).
- [ ] Manually exercise every interactive control in a real browser. **Blocked:** no browser automation package/runtime is installed in this workspace.
- [ ] Inspect every representative public template at desktop, tablet, and mobile viewports. **Blocked:** no browser workflow is currently available.
- [ ] Inspect authenticated/data-dependent states with live credentials/data. **Unverified:** no credentials or disposable live dataset were provided; source/security tests only.
- [ ] Capture fresh baseline screenshots for representative routes and viewports. **Blocked:** no browser workflow. User-provided Phase 10 screenshots remain historical defect evidence, not a full modernization baseline.
- [ ] Capture browser console and network traces. **Blocked:** no browser workflow.

### Route/template inventory

**Public/marketing/editorial (18 templates):** `/`, `/about`, `/blog`, `/blog/[slug]`, `/collaborate`, `/contact`, `/developer`, `/features`, `/how-it-works`, `/optimization`, `/pricing`, `/resources`, `/screenshots`, `/sponsors`, `/worked-example`, `/legal/license`, `/legal/privacy`, `/legal/terms`.

**Operational/authenticated (10 templates):** `/deliveries`, `/job-orders`, `/records`, `/records/login`, `/reports`, `/reports/login`, `/inquiries`, `/inquiries/[id]`, `/inquiries/estimator`, `/inquiries/estimator/[id]`.

**Customer/internal inquiry workflow (4 templates):** `/inquiries/submit`, `/inquiries/submissions/[id]`, `/inquiries/customer-submissions`, `/inquiries/customer-submissions/[id]`.

**System/theme laboratories (3 templates):** `/sys/clay`, `/sys/crystal`, `/sys/liquid-glass`.

Dynamic inquiry detail templates require authorization/data and are explicitly not visually verified. System routes are retained as internal design diagnostics, not treated as primary marketing surfaces.

### API/data boundary inventory

- Authentication: signout.
- Public/server intake: demo request and health.
- Operational data: deliveries, records, operations export, job-order list/progress/release.
- Inquiry operator/customer boundaries: inquiry records/documents/communications; estimator registration, assignment and queues; submission draft, staged documents, finalize, profile, projects, operator review and downloads.
- Existing inquiry tests enforce bounded payloads, same-origin/auth requirements, role/ownership checks, private storage, RLS, immutable/audited flows, and no service-role browser exposure. Modernization must not bypass these server contracts.

### Existing system findings

- A centralized system already exists in `globals.css`, Tailwind configuration, theme definitions, shared surface primitives, and motion tokens. Modernization should consolidate and refine it rather than add a second library.
- `motion` 14.2.0 is already present. `src/lib/motion.ts` centralizes durations/easing/springs and caps stagger at 200 ms.
- Route entry motion, a shared one-shot IntersectionObserver reveal lane, fail-safe visibility, and reduced-motion behavior already exist. These are assets to preserve and audit, not replace wholesale.
- Navigation, dropdown, dialog, accordion, table, filter, pagination, form, support, and theme controls already have reusable implementations and automated interaction coverage.
- The locale proxy preserves explicit localized URLs, prefix-free English URLs, static/API exclusions, search parameters, and remembered language choice.
- Metadata is centralized through shared layout/page helpers; sitemap and robots routes exist. All modernization route edits must retain page metadata and canonical/alternate behavior.
- Current card collections use a count-aware compact grid from tablet width; intentional statistics grids have a separate fixed-grid marker. Forms, tables, detail panels, and intentional split layouts must remain outside this grammar.
- Source inspection and unit tests are not substitutes for visual, assistive-technology, browser-console, network, or live-data verification.

### Baseline automated evidence

Run on 2026-10-18 against `6f73f1f`:

- [x] `npm run lint` — passed.
- [x] `npm run typecheck` — passed.
- [x] `npm run test:density` — **19/19 passed**; compaction census passed.
- [x] `npm run test:navbar` — source guard **2/2 passed**; Vitest **8 files / 100 tests passed**.
- [x] `npm run test:clay` — passed, including coverage check.
- [x] `npm run testcrystal` — passed.
- [x] `npm run guard:pointer` — passed.
- [x] `npm run test:job-orders` — passed.
- [x] `npm run test:inquiry-security` — **73/73 passed**.
- [x] `npm run build` — compiled and type-checked; **504/504** static pages generated.
- [x] Build route manifest confirmed SSG, dynamic inquiry detail/API routes, sitemap, robots, icon, and middleware/proxy.
- [ ] Lighthouse/Lab performance baseline — not run; no browser runtime. No field CWV claim is made.
- [ ] Automated accessibility scanner — not run; no browser runtime. Existing semantic tests are only partial evidence.

Expected test stderr includes Motion's reduced-motion informational warning in reduced-motion test cases; it did not fail the suite.

## Approved implementation sequence

### Phase 10.2 — consolidate the design system

- [x] Audit and rationalize color, typography, spacing, radius, elevation, surface, focus, and responsive tokens in the existing system.
- [x] Define premium surface hierarchy without making all surfaces glass or inflated clay.
- [x] Preserve neutral near-black Clay Black and compact top-panel contracts.
- [x] Document component usage and migration rules through the centralized token comments, Tailwind semantic mappings, and this ledger.
- **Gate:** token tests, theme contrast/visibility suites, lint, typecheck, and build pass; no competing theme/provider introduced.

### Phase 10.3 — shared shell and navigation

- [x] Modernize header/top panel, desktop menus, mobile navigation, locale/theme controls, support affordance, footer, page container, and shared backgrounds using the existing shared shell and unified site stage.
- [x] Preserve keyboard, focus, hover/pin behavior, Escape dismissal, route/query/locale behavior, and responsive overflow safety; the localized first-focus skip link now moves focus to the programmatically focusable main landmark.
- **Gate:** automated navigation tests plus desktop/tablet/mobile browser verification in LTR and RTL.

**Phase 10.3 implementation evidence (2026-10-18):** The localized layout now owns a unified `site-stage`, subtle logical-edge depth rails, a focusable main landmark, and a translated skip link. Existing top-panel, mobile sheet, locale/theme, footer, support, Escape, and hover/pin behavior remains in the shared shell and its interaction suite. Real viewport LTR/RTL inspection remains an open gate.

### Phase 10.4 — motion and feedback system

- [x] Refine existing tokens and primitives into one restrained motion grammar for route, section, state, dialog, menu, and coordinated hero-fact transitions.
- [x] Keep reveals one-shot, stagger-capped, no-JS safe, and reduced-motion plain.
- [x] Avoid scroll hijacking, long exits, layout shifts, per-card observers, and business-data decoration; short hero groups use one parent observer/timeline.
- **Gate:** motion token/reveal tests, reduced-motion inspection, and performance profiling.

**Phase 10.4 implementation evidence (2026-10-18):** Added `MotionStagger`, a Framer Motion (`motion/react`) parent timeline for small, meaningfully ordered groups. Page-hero facts now enter through one observer with capped shared timing, transform/opacity-only children, reduced-motion plain rendering, and an explicit no-JavaScript visibility override. Ordinary grids remain on the shared CSS reveal lane; operational data receives no decorative animation. Browser performance profiling remains open.

### Phase 10.5 — marketing and editorial routes

- [ ] Apply the centralized 2026 surface, typography, density, responsive-grid, and reveal grammar to homepage, about, features, how-it-works, pricing, optimization, worked example, screenshots, resources, developer, collaborate, sponsors, contact, blog index/article, and legal templates.
- [ ] Preserve copy, links, CTA destinations, blog slugs, metadata, localization, and form integrations.
- **Gate:** every template inspected in LTR/RTL at desktop/tablet/mobile; links/forms and metadata verified.

### Phase 10.6 — operational and authenticated routes

- [ ] Apply the centralized operational surface and density grammar to deliveries, records, reports, job orders, and login states for legibility and task completion.
- [ ] Preserve filters, sorting, pagination, exports, query/history behavior, tables, empty/loading/error states, and auth redirects.
- [ ] Keep data dense and motion minimal.
- **Gate:** automated suites plus authenticated live-state verification where credentials/data are available; otherwise leave explicit blockers.

**Phase 10.2 implementation evidence (2026-10-18):** The redesign keeps the established semantic palette and material engines, then adds reusable `Flow`, `Cluster`, and count-aware `AutoGrid` layout primitives backed by a fluid spacing and card-measure scale. `scripts/modernization-contract.test.mjs` guards the token, layout, landmark, motion, and reduced-motion contracts. This is the foundation for route migrations in later phases, not a claim that Phases 10.3–10.6 are complete. Their earlier grouped pass is superseded by the one-phase/one-commit redesign sequence requested by the owner.

### Phase 10.7 — inquiry workflows

- [ ] Modernize customer submission, tracking, operator review, estimator queue/detail, assignments, documents, and communications without changing security boundaries.
- [ ] Preserve validation, staged upload progress/finalization, ownership, role checks, download authorization, audit behavior, and backend contracts.
- **Gate:** all inquiry/security tests, keyboard/focus/error-state QA, and authorized live workflow verification. No migration or production mutation.

### Phase 10.8 — responsive and accessibility closure

- [ ] Verify all templates at representative mobile, tablet, desktop, zoom, LTR, and RTL states.
- [ ] Audit landmarks/headings, names/descriptions, focus order/visibility, dialogs, disclosures, tables, errors, contrast, target sizing, and reduced motion.
- [ ] Correct overflow and layout shifts while preserving count-aware grid behavior.
- **Gate:** browser screenshots, keyboard run, automated scanner, and documented manual exceptions.

### Phase 10.9 — performance and SEO closure

- [ ] Measure honest pre/post lab results on identical routes/build conditions.
- [ ] Audit image/font/script loading, client boundaries, bundle impact, layout stability, metadata, canonical/alternate links, sitemap, robots, and structured data where applicable.
- [ ] Report lab findings as lab findings; do not claim field Core Web Vitals without field data.
- **Gate:** production build, route manifest, lab evidence, and SEO checks pass.

### Phase 10.10 — final regression and Vercel preview

- [ ] Run the complete automated suite and production build.
- [ ] Verify every route family and critical interaction in preview, including locale redirects and authenticated/data-dependent blockers.
- [ ] Capture final desktop/tablet/mobile evidence and compare with baseline.
- [ ] Deploy **preview only**; do not alter production configuration or secrets.
- [ ] Produce final report: routes/files, component and motion changes, evidence, test/build/accessibility/performance results, preview status, defects, and unverified areas.

## Persistent blockers and unresolved items

1. Browser-based visual, responsive, console/network, accessibility, and performance baselines are currently unavailable because no browser automation runtime is installed.
2. Authenticated and data-dependent visual states cannot be certified without suitable test credentials and disposable/live test data.
3. Phase 9E Windows live verification remains deferred until 2026-10-19 08:00 Asia/Dubai; Phase 11 production coordination must not start beforehand.
4. No Vercel preview has been deployed for this modernization phase.
5. No field performance dataset has been supplied; therefore no field Core Web Vitals statement is possible.
6. `npm run i18n:verify` currently fails its pre-existing completeness policy: eight locales marked complete report roughly 75% coverage, while six locales remain explicitly in progress at roughly 10%. The Phase 10.2–10.6 pass introduced no new untranslated key (the skip link reuses the existing translated `Skip to content` key), but full locale completeness remains unresolved.

## Evidence policy

For each subsequent phase, append: date/commit, changed templates/components, commands and exact outcomes, screenshots/preview references, manually exercised interactions, locale/viewports, defects found/fixed, and remaining blockers. Do not overwrite unresolved items merely because source checks pass.
