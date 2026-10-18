# Theme Center Implementation Checklist

**Temporary execution ledger — do not delete until every acceptance gate is actually verified.**
**Started:** 2026-10-18
**Scope:** `website/` only
**Source audit:** `THEME_CENTER_AUDIT.md`

Checked items mean evidence exists. Code written without verification remains unchecked.

## Phase 0 — Repository and live-site audit

### Work

- [x] Inspect package configuration, framework, dependencies, scripts, entry/layout/proxy architecture.
- [x] Inventory all 35 page templates and 27 API route templates.
- [x] Inspect existing token, family, mode, studio, persistence, motion, component, localization, metadata, and test architecture.
- [x] Inspect representative live rendered responses: homepage, features, Crystal catalogue, restricted reports, authenticated inquiry entry.
- [x] Record current family coverage and requested-family gaps.
- [x] Record existing functionality/data/security boundaries that must be preserved.
- [x] Run baseline lint, typecheck, modernization, navbar/interaction, density, and production build.
- [ ] Capture desktop/tablet/mobile screenshots and computed-style/browser-console/network evidence for representative public routes.
- [ ] Inspect authenticated/data-dependent live states with authorized credentials and disposable data.

### Acceptance/evidence

- [x] `THEME_CENTER_AUDIT.md` contains technology summary, complete route-template inventory, architecture, inconsistencies, performance/motion concerns, accessibility/responsive concerns, preservation requirements, risks, and implementation order.
- [x] Baseline: lint passed; TypeScript passed; modernization 7/7; navbar guards 2/2; Vitest 100/100; density 19/19 + census; build 505/505.
- [ ] Visual browser baseline attached. **Blocked: no browser automation/runtime available through current tooling.**

## Phase 1 — Theme architecture and design tokens

### Work

- [x] Define a versioned technology-neutral `ThemeSpec` schema suitable for JSON export and later WPF translation.
- [x] Separate portable semantic tokens from web-only material/effect tokens in `THEME_SYSTEM_ARCHITECTURE.md`.
- [x] Define semantic token groups: color, surfaces, text, borders/focus, typography, spacing/width, radius/nesting, elevation, material parameters, motion, breakpoints, layering, and states. Gradient/lighting intent is represented through material edge/reflection roles; web gradients remain adapter-specific.
- [x] Define explicit legacy migration targets for Studio, Monet, Industrial, Crystal, and Clay. Runtime migration remains a Phase 3 integration task.
- [x] Add distinct definitions for Claymorphism, Glassmorphism, Liquid Glass, Skeuomorphism, Neumorphism, Flat/Material, and the first approved Hybrid recipe.
- [x] Define schema/version requirements and legacy-family migration behavior. Persisted runtime wiring remains pending.
- [x] Validate and clamp every field in the portable imported/persisted configuration before adapter use.
- [x] Add deterministic JSON serialization and schema validation tests.

### Acceptance

- [x] Every required family has complete portable light/dark token mappings; website fallback translation remains an adapter task.
- [x] Glassmorphism and Liquid Glass are structurally distinct and regression-tested.
- [x] Neumorphism retains explicit border/focus contracts and does not rely on shadow alone.
- [x] Portable JSON contains no React/CSS-only implementation detail.
- [ ] Existing saved mode/family/studio choices migrate safely in the live runtime. **Pending adapter integration; explicit targets exist but are not applied yet.**
- [ ] Full contrast-critical pair matrices pass for every new family. **Pending CSS adapters.**
- [x] Token-family completeness, finite values, allowed configuration values, hostile-input rejection, and schema round-trip tests pass (5/5).

### Phase 1 evidence and outstanding risks

Implemented `src/theme/schema.ts`, `src/theme/specs.ts`, `scripts/theme-schema.test.mjs`, and `THEME_SYSTEM_ARCHITECTURE.md`. `npm run test:theme-system` passed 5/5; repository ESLint and TypeScript passed after a clean dependency install. The Node test runner emits an informational module-type reparsing warning; production TypeScript/Next resolution is unaffected. Phase 1 remains partially open until runtime adapters safely migrate existing persisted choices and every new family has CSS-level contrast/fallback evidence.

## Phase 2 — Shared component library

### Work

- [x] Catalogue current shared primitives and page-specific consolidation candidates in `COMPONENT_LIBRARY.md` and the machine-readable registry.
- [ ] Establish typed variants/sizes/states for every required category. **Completed for existing button/card/badge/navigation/hero/accordion contracts and new field/feedback/loading contracts; domain dialogs, tables, tabs, filters, pagination, galleries, and tooltips still require migration audits.**
- [x] Extend existing library locations rather than creating a competing component package.
- [ ] Move unexplained component/page visual literals to semantic tokens or documented exceptions. **Pending scoped census and route migration; no mass replacement performed.**
- [x] Add initial component accessibility contracts and tests for field relationships, live feedback, native progress, decorative skeletons, and named loading status.

### Acceptance

- [ ] Required component categories have an approved implementation and usage documentation. **Core categories are documented; complex overlays and cross-domain data controls remain open.**
- [ ] Shared components respond consistently across every required family and mode. **Portable coverage contract exists; CSS adapters are not yet implemented.**
- [ ] Keyboard, focus, disabled, loading, error, success, touch, RTL, and reduced-motion states are represented across the complete library. **New core primitives cover their applicable states; route-level verification remains open.**
- [x] Duplicate patterns will be removed only after consumer migration and tests; the Phase 2 pass deliberately leaves domain implementations intact.

### Phase 2 evidence and outstanding risks

Added `src/theme/component-contracts.ts`, `COMPONENT_LIBRARY.md`, shared `Field`, `Feedback`, `Skeleton`, `Progress`, and `Spinner` primitives, and `component-system.css`. The layout imports the shared state grammar once. `npm run test:theme-components` passed 4/4; theme-schema tests passed 5/5; ESLint and TypeScript passed. Phase 2 remains open for audited consolidation of dialogs/popovers/tooltips and domain table/filter/tab/pagination patterns, plus family adapter and route-level verification.

## Phase 3 — Theme Center editor and live previews

### Work

- [ ] Build a dedicated internal Theme Center route/interface, separate from public marketing navigation.
- [ ] Refactor the existing compact Appearance Studio to consume the central schema/store.
- [ ] Support family, mode, accent, typography, spacing, radius, elevation, transparency/blur, motion intensity/speed, and component-variant controls within safe limits.
- [ ] Build live previews for typography, navigation, cards, buttons, forms, tables, dialogs, status, loading, empty/error, and motion.
- [ ] Add same-content family comparison and responsive preview frames.
- [ ] Implement reset and, where persistence is local, named save/duplicate/rename/restore presets.
- [ ] Implement validated JSON export/import without adding a database or authentication service.
- [ ] Define whether the current public drawer remains a limited visitor appearance control or becomes development-only; do not expose administrative configuration accidentally.

### Acceptance

- [ ] Theme edits update shared components through central tokens only.
- [ ] Invalid imported/persisted values fail safely and cannot poison CSS variables.
- [ ] Preset operations and persistence contracts are tested.
- [ ] Responsive previews cannot mutate production data or navigation state.
- [ ] Editor is keyboard accessible, dismissible, and usable with reduced motion.

## Phase 4 — Animation and interaction engine

### Work

- [ ] Document and enforce motion intensity/speed mapping over existing motion tokens.
- [ ] Consolidate route, reveal, group, dropdown, accordion, dialog, popover, tab, gallery, hover/focus/press, and loading transitions.
- [ ] Retain shared observers, one-shot defaults, stagger caps, visibility fail-safes, and no-JS content visibility.
- [ ] Ensure every listener/observer cleans up and every family supports reduced motion.
- [ ] Profile backdrop/filter and animated material costs on representative routes.

### Acceptance

- [ ] No splash screen, artificial delay, scroll hijacking, long grid stagger, or business-data decoration.
- [ ] Transform/opacity remain default animation properties.
- [ ] Mouse, touch, keyboard, Escape, focus return, and browser history work for relevant controls.
- [ ] Reduced-motion and no-JS tests pass.

## Phase 5 — Complete website integration

### Work

- [ ] Build a route-by-route migration matrix for all 35 page templates and shared generated variants.
- [ ] Integrate public/editorial/legal templates through approved primitives and tokens.
- [ ] Integrate operational/authenticated templates without sacrificing density or task completion.
- [ ] Integrate all inquiry/customer/operator/estimator templates without changing backend/security contracts.
- [ ] Integrate loading, error, not-found, metadata, sitemap, robots, manifest, footer, support, and internal catalogue surfaces.
- [ ] Preserve all content, resources, product facts, links, forms, localization, metadata, auth, APIs, query/history state, exports, uploads, and document workflows.

### Acceptance

- [ ] Every template has a recorded code migration and verification status.
- [ ] No route or required section was removed.
- [ ] No page branches on family-specific one-off visual code when a shared component/token can express the intent.
- [ ] Product remains clearly Windows desktop software, not SaaS.

## Phase 6 — Responsive and accessibility audit

### Work

- [ ] Verify representative mobile, tablet, laptop, desktop, wide, zoom, LTR, and RTL states.
- [ ] Check overflow, clipping, image ratios, glass readability, touch targets, mobile form zoom, dialogs/dropdowns, headings, names, labels, errors, focus, traps, and color-only status.
- [ ] Run keyboard and screen-reader-oriented manual checks.
- [ ] Run an automated browser accessibility scanner when tooling is available.
- [ ] Verify every theme/mode against contrast and forced-colors contracts.

### Acceptance

- [ ] Browser screenshot matrix and defects are recorded.
- [ ] WCAG 2.2 AA-oriented checks pass or exceptions are documented with impact/remediation.
- [ ] Browser zoom remains enabled.
- [ ] No critical interaction requires hover or pointer precision.

## Phase 7 — Performance, security, and regression testing

### Work

- [ ] Measure baseline/final JS bundles, route chunks, fonts, image loading, layout shifts, and lab performance under identical conditions.
- [ ] Audit client boundaries, hydration, re-renders, observers/listeners, blur regions, and animation costs.
- [ ] Run lint, typecheck, all design/theme/material/interaction/density/pointer tests, job-order tests, inquiry/security tests, localization verification, and production build.
- [ ] Investigate dependency audit findings without force-upgrading.
- [ ] Inspect browser console/network on representative public/authenticated routes.

### Acceptance

- [ ] Production build succeeds and route manifest is reviewed.
- [ ] Existing tests pass or every failure has an investigation and disposition.
- [ ] No new avoidable runtime/console errors, major layout shifts, duplicate animation dependency, or security regression.
- [ ] Performance report uses measured results only; no development result is called field CWV.

## Phase 8 — Documentation, agent rules, and final verification

### Work

- [ ] Update permanent design-system architecture and family specifications.
- [ ] Document portable tokens versus CSS/Framer/WPF-specific translation.
- [ ] Create/update `website/AGENTS.md` with mandatory design-system governance.
- [ ] Add feasible automated checks for hard-coded visual values, missing/invalid tokens, family completeness, unsupported IDs, duplicate patterns, and accessibility contracts.
- [ ] Produce final route-by-route verification matrix and audit report.
- [ ] Verify a Vercel preview only; do not alter production settings/secrets.
- [ ] Confirm no WPF files changed.

### Acceptance

- [ ] Future agents can identify the sole design authority without repository archaeology.
- [ ] Agent rules require token/component reuse, preservation, verification, explicit exceptions, and honest claims.
- [ ] Final report includes actual tests, measurements, preview status, defects, and unverified areas.
- [ ] Delete this temporary checklist only when every acceptance gate is verified; retain permanent docs/report/rules.

## Persistent risks and blockers

1. Visual desktop/tablet/mobile live inspection is incomplete because current tools provide rendered text but no browser viewport/runtime.
2. Authenticated/data-dependent live states require authorized credentials and disposable test data.
3. The working tree already contains a large prior patch (133 status entries at Phase 0); all Theme Center work must preserve it and avoid resets or indiscriminate cleanup.
4. Current required family taxonomy exceeds the five existing selectable families and needs schema/migration work, not aliases.
5. Existing global/material CSS is large; adding families without explicit cascade ownership would create another override layer.
6. `npm ci` reports 17 dependency audit findings; no force-fix is authorized.
7. Existing localization completeness status must be rechecked before final claims.
8. No commit or push is authorized by this master prompt; changes remain in the working tree until the user explicitly authorizes Git operations.
