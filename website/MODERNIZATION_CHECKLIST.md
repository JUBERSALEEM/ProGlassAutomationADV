# ProGlass Automation 2026 Full-Site Modernization Checklist

**Program started:** 2026-10-18
**Scope:** `website/` only
**Branch:** `arena/23bacfef-proglassautomationadv`
**Status:** Phase 1 source/live-response audit and implementation sequence documented. Browser viewport evidence and authenticated live states remain blocked. No new broad redesign implementation begins until this phase record is accepted.

This is the active evidence ledger for the seven-phase modernization program. Generated locale variants are not counted as unique page designs. A checked item means its stated result was actually inspected or tested; source assertions are not treated as visual-browser evidence.

## Permanent constraints

- Preserve all routes, content, 15 locales, RTL behavior, forms, authentication, data/API integrations, SEO, Vercel configuration, security boundaries, and desktop-product meaning.
- ProGlass is Windows desktop software, not browser SaaS. Do not invent cloud accounts, subscriptions, testimonials, guarantees, screenshots, customers, or capabilities.
- Reuse and improve the existing token, material-engine, component, and Motion infrastructure; do not add a competing theme or component library.
- Glass, clay, depth, and animation are selective. Operational data remains dense, readable, and task-oriented.
- Preserve keyboard behavior, focus return, Escape handling, native semantics, history, search parameters, locale routing, validation, submissions, uploads, exports, and authorization.
- Do not modify WPF sources, apply migrations, mutate production data, alter production secrets, or deploy production.
- Phase 9E Windows verification and separate Phase 11 production coordination remain outside this website program.

# Phase 1 — Inspect before editing

## Repository and architecture

- [x] Inspect repository state, package configuration, scripts, framework, dependencies, entry points, localized layout, proxy, loading/error/not-found boundaries, and deployment configuration.
- [x] Inspect global CSS, Clay/Crystal engines, Tailwind semantic bridge, portable theme schema, existing Theme Studio, shared UI/layout components, domain components, and motion utilities.
- [x] Confirm one animation dependency: `motion` 14.2.0 (`motion/react`); no overlapping animation library.
- [x] Inspect server/client boundaries and identify large client surfaces (`ThemeStudio`, navigation, galleries, operational dashboards, inquiry workspaces).
- [x] Inspect metadata helper, canonical/language alternates, structured data, sitemap, robots, manifest, proxy exclusions, and Vercel Speed Insights.
- [x] Inspect existing automated suites and build procedure.

## Route inventory

**35 page templates:**

### Public/editorial/legal — 18 templates

- [x] `/`
- [x] `/about`
- [x] `/features`
- [x] `/how-it-works`
- [x] `/optimization`
- [x] `/worked-example`
- [x] `/screenshots`
- [x] `/pricing`
- [x] `/resources`
- [x] `/blog`
- [x] `/blog/[slug]`
- [x] `/developer`
- [x] `/collaborate`
- [x] `/sponsors`
- [x] `/contact`
- [x] `/legal/privacy`
- [x] `/legal/terms`
- [x] `/legal/license`

### Operational/authenticated — 6 templates

- [x] `/records`
- [x] `/records/login`
- [x] `/reports`
- [x] `/reports/login`
- [x] `/deliveries`
- [x] `/job-orders`

### Inquiry/customer/operator/estimator — 8 templates

- [x] `/inquiries`
- [x] `/inquiries/[id]`
- [x] `/inquiries/submit`
- [x] `/inquiries/submissions/[id]`
- [x] `/inquiries/customer-submissions`
- [x] `/inquiries/customer-submissions/[id]`
- [x] `/inquiries/estimator`
- [x] `/inquiries/estimator/[id]`

### Internal system catalogues — 3 templates

- [x] `/sys/clay`
- [x] `/sys/crystal`
- [x] `/sys/liquid-glass`

**API:** 27 `route.ts` templates covering signout, health, demo requests, records, deliveries, exports, job orders, inquiry/customer/operator/estimator workflows and private documents. Build output also exposes `/api/og`.

**Generated output:** current production build generates **505/505 static pages**. This is locale/blog/metadata expansion, not 505 unique designs.

## Locale behavior

- [x] Confirm 15 locale codes; Arabic and Urdu are RTL.
- [x] Confirm remembered cookie → country → `Accept-Language` → English resolution order.
- [x] Confirm English uses prefix-free external URLs through an internal rewrite.
- [x] Confirm non-English selection uses explicit localized URLs and retains query strings.
- [x] Confirm `/de-DE` is a shipped explicit locale route.
- [ ] Manually exercise default-locale redirect, `/de-DE`, language switching, query retention, and back/forward in a real browser. **Blocked: no browser runtime.**

## Live-site inspection

Rendered live responses inspected at `https://proglassautomationadv-website-tkim.vercel.app/`:

- [x] Homepage: accurate Windows/local-SQLite product positioning, product preview, CTAs, developer evidence.
- [x] Features: real 48-target/nine-group module catalogue and WPF terminology.
- [x] Crystal catalogue: shared surface/material grammar and reduced-motion doctrine.
- [x] Reports: authentication gate and public return path.
- [x] Inquiry submit: email-verification gate.
- [ ] Open every public template at desktop, tablet, and mobile widths. **Blocked: fetch tooling returns rendered content but not a visual browser viewport.**
- [ ] Capture baseline screenshots and computed-style evidence. **Blocked: Chromium/Firefox/Playwright/Puppeteer are unavailable.**
- [ ] Inspect browser console/network and interactive runtime defects. **Blocked: no browser runtime.**
- [ ] Inspect authenticated/data-dependent states. **Blocked: no authorized credentials/disposable live dataset.**

## Existing interaction and component audit

- [x] Navigation: desktop dropdowns, mobile panel, language menu, theme controls, current-route behavior, hover/pin and Escape tests.
- [x] Content disclosure: accordion with native button, `aria-expanded`, controlled region and height transition.
- [x] Overlays: operational/inquiry detail modals and gallery viewers identified for focus/exit-motion audit.
- [x] Forms: contact/demo, reports authentication, customer inquiry, staged uploads and server validation boundaries identified.
- [x] Operational controls: tabs, filters, search, tables, pagination, exports, loading/empty/error states identified.
- [x] Shared visual components: PageHero, PageBanner, SectionHeading, IconChip, badges, EmptyState, GlassCard, buttons, layout primitives, footer/legal, gallery/viewer.
- [x] Motion: shared one-shot observer buckets, MotionReveal, MotionStagger, route template, reduced-motion and no-JS visibility safeguards.

## Findings

- [x] Existing system is substantial and must be consolidated, not replaced: semantic variables, Tailwind bridge, Clay/Crystal engines, portable schema, component contracts and shared primitives already exist.
- [x] Current visual strengths: strong product specificity, premium material engines, self-hosted fonts, semantic surface ladder, count-aware card grids, localized shell and robust interaction tests.
- [x] Current consistency risks: very large layered CSS/material engines, page/domain-specific control patterns, many backdrop-filter sites, arbitrary visual literals requiring scoped classification, and a large public Theme Studio client surface.
- [x] Performance risks: large blur census on dense pages, Theme Studio/useStudio size, expensive material effects without current browser profiling, and no measured lab/field baseline.
- [x] Accessibility risks: real-browser focus order/return, narrow Theme Studio usability, modal semantics, glass contrast over actual imagery, RTL/long-string layout, authenticated form/table states remain unverified.
- [x] Dependency baseline: `npm ci` reports 17 audit findings (2 low, 3 moderate, 10 high, 2 critical); no force-fix applied.
- [x] Existing localization completeness failure remains unresolved and must not be hidden by redesign work.

## Baseline automated evidence

- [x] ESLint passed.
- [x] TypeScript passed.
- [x] Modernization contracts: 7/7 passed.
- [x] Theme schema: 5/5 passed.
- [x] Theme components: 4/4 passed.
- [x] Navbar guards: 2/2 passed.
- [x] Vitest interaction suite: 100/100 passed before the new component test file; new targeted component suite passed separately.
- [x] Density/card contracts: 19/19; census passed.
- [x] Production build: 505/505 static pages.
- [ ] Browser accessibility scan, Lighthouse/lab performance, LCP/INP/CLS and bundle comparison. **Blocked: no browser runtime; no field claim made.**

## Approved implementation sequence

1. Finish Phase 1 only when browser-dependent blockers become testable; otherwise retain them visibly and proceed with source-verifiable work per the prompt's unavailable-tool rule.
2. Phase 2: consolidate the current token/schema/material system into the premium navy/cyan/violet/warm hybrid; preserve Clay/Crystal behavior and add scoped enforcement before route migration.
3. Phase 3: refine existing Motion primitives and add coordinated open/close behavior, sticky-nav treatment and selective storytelling; no per-card observers or operational decoration.
4. Phase 4: migrate shared shell first, then public/editorial templates, then operational templates, then inquiry workflows; verify each route family before continuing.
5. Phase 5: responsive/accessibility closure with real viewport and keyboard evidence when tooling is available.
6. Phase 6: measured performance and quality comparison; no invented Core Web Vitals.
7. Phase 7: complete suite, route/locale/interaction matrix, screenshots, Vercel preview only, final defects and unverified areas.

## Phase 1 gate

- [x] Repository/source/live-response audit documented.
- [x] Route/API/generated-count distinction documented.
- [x] Implementation order documented before new redesign work.
- [ ] Browser viewport baseline complete. **Environment blocker.**
- [ ] Authenticated live-state baseline complete. **Credentials/data blocker.**

# Phase 2 — Establish a complete design system

- [x] Reconcile portable ThemeSpec, existing website tokens, Tailwind bridge and live CSS adapters into one authority.
- [x] Define premium navy/cyan/violet/warm semantic palette with verified light/dark contrast.
- [x] Define responsive typography, spacing, shell widths, grid measures and breakpoints.
- [x] Define radii/nested-radius, borders, elevation, glass, clay and interaction-state tokens.
- [x] Define motion, focus, forced-colors and reduced-motion tokens.
- [x] Classify and migrate conflicting page/component hard-coded values with allowlists for engines/catalogues/data visualizations.
- [x] Complete shared component contracts without introducing a duplicate library.
- [x] Run token completeness, contrast/material, component, lint, type and build gates.

**Verification gate:** one authority; no competing provider/library; theme/material suites pass; shared components demonstrate every state; documentation identifies approved usage and exceptions.

## Phase 2 evidence

- [x] `THEME_SYSTEM_ARCHITECTURE.md`, `COMPONENT_LIBRARY.md`, `src/theme/schema.ts`, `src/theme/specs.ts`, `src/theme/component-contracts.ts` and the existing runtime adapters describe one shared authority and its approved exceptions.
- [x] Added `scripts/design-system-governance.test.mjs`: 4/4 checks pass for literal-color containment, core runtime semantic roles, Tailwind semantic mappings and material-family isolation from route/component markup. Comments are ignored and the localized root layout alone may publish the server-default family attribute.
- [x] Theme schema 5/5 and shared component semantics 4/4 passed.
- [x] Clay contrast/visibility 33/33 and 100% engine coverage passed; Crystal contrast suite passed.
- [x] Modernization 7/7, navbar guard plus Vitest 100/100, density 19/19 plus census and pointer guard across 196 files passed.
- [x] ESLint, TypeScript and optimized production build passed; 505/505 static pages generated.
- [ ] Visually validate all semantic/material combinations in a browser, including forced-colors and actual-image glass contrast. **Blocked: no browser runtime; automated/source contrast evidence is not relabeled as visual evidence.**

# Phase 3 — Build the 2026 motion system

- [x] Audit open/close behavior for navigation, mobile panel, accordion, dialogs, lightboxes, filters, tabs, notices and contextual controls.
- [x] Establish small documented directional/reveal patterns.
- [x] Preserve one-shot shared IntersectionObserver behavior and no-JS visibility.
- [ ] Add intentional exit motion only where presence can be retained safely.
- [ ] Add selective homepage/editorial scroll storytelling tied to real product workflow.
- [ ] Add sticky-navigation surface transition and bounded pointer highlights on fine pointers.
- [ ] Preserve route, locale, query, history, focus, Escape, touch and reduced-motion behavior.
- [ ] Profile blur/filter/mask/shadow/sticky costs.

**Verification gate:** motion tests, keyboard/focus tests, reduced-motion/no-JS checks, pointer guard, browser profiling when available; no navigation delay or content gating.

## Phase 3 evidence in progress

- [x] `src/lib/motion.ts` documents the single CSS/motion-react token mirror, bounded directional presets, reveal-delay cap and lane rule; `scripts/motion-tokens.test.mjs` prevents runtime drift.
- [x] Shared `useReveal` buckets avoid one observer per card; MotionReveal has reduced-motion, coarse-pointer and near-viewport fail-safe paths; route entry remains transform/opacity only with a no-JS override.
- [x] Added a single `test:motion-system` gate: motion/Crystal performance contracts 28/28 and reveal/route behavior 20/20 passed.
- [x] Collapsed Accordion panels now use `inert` plus `aria-hidden`, preventing controls inside an animated closed panel from remaining keyboard-focusable.
- [x] Accordion interaction regression 1/1, combined reveal/route/Accordion behavior 21/21, TypeScript and ESLint passed.
- [x] `/features` pilot: every shared SectionHeading now enters as a coordinated eyebrow/title/lede/action sequence through the existing shared observer; the three full-size live product frames use restrained scale reveals; reduced-motion and print force every nested element visible.
- [x] Motion pilot gates: TypeScript, ESLint, motion contracts 28/28 and interaction tests 21/21 passed.
- [ ] Browser profile compositor/paint behavior and manually validate focus, touch and reduced motion. **Blocked in the agent environment: no installed browser runtime. A live preview is required for human visual acceptance.**

## Animation verification matrix

`Source verified` means the route uses the named shared implementation and passed automated contracts. It is not browser evidence. Every row remains visually pending until exercised in a real browser at desktop/mobile widths and with reduced motion.

| Route/template | Intended motion and interaction | Automated/source result | Real-browser result / remaining issue |
|---|---|---|---|
| `/` | Coordinated hero, section-heading sequence, workflow/card reveals, restrained showcase depth | Source verified | Pending scroll both directions, hover/touch, reduced motion |
| `/features` | Heading sequences, searchable module transitions, staggered facts, clipped/scale product frames, accordions | Pilot implemented; tests passed | **Priority visual acceptance pending** |
| `/about` | Editorial headings, evidence/card reveals | Source verified | Pending |
| `/developer` | Editorial headings, disclosure transitions | Source verified | Pending |
| `/collaborate` | Editorial headings, cards, disclosure transitions | Source verified | Pending |
| `/contact` | Heading/form entrance and control feedback | Source verified | Pending form/focus/mobile keyboard |
| `/how-it-works` | Progressive workflow storytelling and cards | Source verified | Pending |
| `/optimization` | Product demonstration reveals and disclosures | Source verified | Pending interactive run/reduced motion |
| `/worked-example` | Progressive document-chain storytelling | Source verified | Pending |
| `/screenshots` | Gallery/filter/viewer transitions and keyboard navigation | Source verified | Pending image loading/touch/keyboard |
| `/pricing` | Edition/card reveals and disclosures | Source verified | Pending |
| `/resources` | Editorial/card reveals | Source verified | Pending |
| `/blog` | Article-card reveals | Source verified | Pending |
| `/blog/[slug]` | Reading-layout heading/content reveals | Source verified | Pending all three slugs |
| `/sponsors` | Editorial/cards/disclosures | Source verified | Pending |
| `/legal/license` | Reading-layout headings | Source verified | Pending |
| `/legal/privacy` | Reading-layout headings | Source verified | Pending |
| `/legal/terms` | Reading-layout headings | Source verified | Pending |
| `/records` | Task-first filters/table; named detail-dialog entry | Dialog semantics verified | Pending auth, focus trap/return, exit motion |
| `/records/login` | Short form/control feedback | Source verified | Pending |
| `/reports` | Task-first filters/table and states | Source verified | Pending auth/data |
| `/reports/login` | Short form/control feedback | Source verified | Pending |
| `/job-orders` | Task-first table; named production dialog entry | Dialog semantics verified | Pending auth, focus trap/return, exit motion |
| `/deliveries` | Task-first table; named ledger dialog entry | Dialog semantics verified | Pending auth, focus trap/return, exit motion |
| `/inquiries` | Task-first queue, filters and dialog states | Source verified | Pending auth/data |
| `/inquiries/[id]` | Detail workflow and contextual controls | Source verified | Pending auth/data |
| `/inquiries/customer-submissions` | Queue/filter states | Source verified | Pending auth/data |
| `/inquiries/customer-submissions/[id]` | Submission detail/document states | Source verified | Pending auth/upload data |
| `/inquiries/estimator` | Queue/filter states | Source verified | Pending auth/data |
| `/inquiries/estimator/[id]` | Estimator workflow/contextual states | Source verified | Pending auth/data |
| `/inquiries/submissions/[id]` | Submission workflow/document states | Source verified | Pending auth/data |
| `/inquiries/submit` | Staged form, validation and upload feedback | Source verified | Pending authenticated submission/upload |
| `/sys/clay` | Internal material controls and bounded effects | Contract tests passed | Pending browser catalogue review |
| `/sys/crystal` | Internal material controls, pointer light and bounded frost | Motion contracts passed | Pending browser/profile review |
| `/sys/liquid-glass` | Prototype tabs, selection and presence transitions | Interaction tests passed | Pending browser/profile review |

Shared shell for every route: short route entry, sticky navigation treatment, dropdown/mobile-menu state motion, button press/hover, one-shot reveal observer, no-JS visibility, coarse-pointer adaptation, and reduced-motion fallbacks. Browser checks for Escape, outside click, navigation close, history, locale/query preservation, console errors and rendering performance remain mandatory.

# Phase 4 — Modernize every page

- [ ] Shared shell, navigation, footer, support and loading/error/not-found surfaces.
- [ ] Homepage and marketing/product storytelling.
- [ ] About, Developer and Collaborate differentiated editorial compositions.
- [ ] Resources, Blog index/article and reading layouts.
- [ ] Features, Optimization, Worked Example, Screenshots, Pricing and legal pages.
- [ ] Records, Reports, Job Orders and Deliveries task-focused interfaces.
- [ ] Inquiry/customer/operator/estimator routes and form states.
- [ ] Internal catalogues updated as system evidence, not public marketing.
- [ ] Preserve content, translations, metadata, links, forms, data, auth, uploads, exports and APIs.

**Verification gate:** route-by-route matrix for all 35 templates; source and interaction tests; public live/preview inspection; authenticated blockers explicit.

# Phase 5 — Responsive design and accessibility

- [ ] Verify phone, tablet, laptop, desktop, wide, zoom, LTR and RTL.
- [ ] Resolve overflow, clipping, tables, dialog fit, image ratios and mobile form zoom.
- [ ] Verify target sizes, contrast, imagery/glass readability and non-color status.
- [ ] Verify landmarks, headings, labels, descriptions, errors, live regions, focus order/return and keyboard traps.
- [ ] Verify without animation, hover, fine pointer or JavaScript-enhanced reveals.
- [ ] Run automated browser accessibility scan when available.

**Verification gate:** WCAG 2.2 AA-oriented evidence, screenshot matrix, keyboard run, scanner output and documented exceptions.

# Phase 6 — Performance and quality control

- [ ] Measure baseline/final LCP, INP, CLS where valid tooling/data exists.
- [ ] Compare JS/route chunks, client boundaries, hydration, fonts, images and duplicate dependencies.
- [ ] Profile scroll smoothness and animation/material overhead.
- [ ] Inspect console errors, failed requests and route loading.
- [ ] Preserve metadata, canonical URLs, structured data, sitemap, robots and indexability.
- [ ] Investigate dependency audit findings without force upgrades.

**Verification gate:** reproducible measurements under equal conditions; production build; no field-CWV claim without sufficient field data.

# Phase 7 — Verification and deployment

- [ ] Run production build, lint, typecheck and every existing/new automated suite.
- [ ] Verify all page templates, generated locales and dynamic/authenticated blockers.
- [ ] Verify navigation, links, forms, galleries, filters, tables, pagination, locale switching and back/forward.
- [ ] Compare baseline/final screenshots.
- [ ] Inspect preview desktop/mobile performance, console and network.
- [ ] Deploy Vercel preview only; preserve production configuration/secrets.
- [ ] Produce final route matrix, changed files/components, motion patterns, test/accessibility/performance evidence, deployment status, defects and unverified areas.

**Verification gate:** preview accepted; all remaining blockers/defects explicit; checklist retained until final acceptance and explicit archive authorization.

# Persistent blockers

1. No browser executable/automation runtime is installed; visual viewport, screenshots, accessibility tree, console/network and lab performance remain unavailable.
2. Authenticated/data-dependent live states require authorized credentials and disposable test data.
3. Existing localization completeness failure remains open.
4. Dependency audit findings require investigation, not an automatic forced fix.
5. No Vercel preview has been deployed for this seven-phase program.
