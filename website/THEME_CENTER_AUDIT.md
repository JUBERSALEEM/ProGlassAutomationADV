# ProGlass Automation Theme Center — Phase 0 Audit

**Audit date:** 2026-10-18 (Asia/Dubai)  
**Scope:** `website/` only  
**Live deployment inspected:** `https://proglassautomationadv-website-tkim.vercel.app/`  
**Repository baseline:** branch `arena/23bacfef-proglassautomationadv`, base HEAD `324b4f0`, with a large pre-existing working patch (133 status entries) that must be preserved  
**Audit status:** source architecture, route manifests, representative live text responses, and automated baseline inspected. Visual viewport/browser inspection remains unresolved.

## 1. Executive finding

This is not a greenfield redesign. The site already has a substantial design system and a capable public-facing Appearance Studio:

- semantic CSS variables and a Tailwind semantic bridge;
- dark/light mode, pre-paint boot logic, persisted appearance selection, and validated studio dials;
- five current families (`studio`, `monet`, `industrial`, `crystal`, `clay`);
- separate Clay and Crystal material engines;
- shared components for surfaces, buttons, headings, layout, disclosures, status, navigation, footer, forms, galleries, tables, filters, pagination, dialogs, support, and motion;
- `motion` 14.2.0 plus a lightweight CSS/shared-IntersectionObserver reveal lane;
- system catalogue routes for Clay, Crystal, and Liquid Glass.

The requested Theme Center should therefore consolidate and formalize this system rather than replace it. The main architectural gaps are: no technology-neutral authoritative theme schema; incomplete requested family taxonomy; no dedicated full Theme Center route with component comparison/responsive preview; no named preset CRUD or JSON configuration export; incomplete automated governance over arbitrary visual values; and no permanent agent instruction file in `website/`.

## 2. Technology and repository summary

- Next.js 16.3.6 App Router, React 19.2, TypeScript 5.7, Tailwind 3.4.
- Motion/Framer Motion implementation: `motion` 14.2.0 (`motion/react`). No second animation library is present.
- Supabase client for authenticated operational/inquiry workflows.
- Self-hosted variable fonts: Inter, IBM Plex Sans, JetBrains Mono, Source Serif 4.
- Vercel Speed Insights present.
- Vitest, Testing Library, Node test scripts, ESLint, TypeScript, contrast/material guards, density guards, pointer-state guard, inquiry/security tests, and production-build freshness checks.
- Public optimized image assets ship as AVIF/WebP with selected PNG originals.
- Localization supports 15 locale codes; Arabic and Urdu are RTL. English URLs are prefix-free via proxy rewrite, while non-English selection uses explicit localized URLs.

### Baseline dependency note

`npm ci` completed with a Vite/Node type peer warning and reported 17 audit findings (2 low, 3 moderate, 10 high, 2 critical). No automatic audit fix was applied. Findings require dependency-path investigation before any upgrade because forced upgrades could break Next/Vitest compatibility.

## 3. Complete discovered page-template inventory

There are **35 page templates**. Generated locale/blog variants are not unique designs.

### Public product, marketing, editorial, and legal

| Route template | Purpose |
|---|---|
| `/` | Product positioning, interactive product preview, developer story, product evidence |
| `/about` | Product identity, provenance, modules, language/product facts |
| `/features` | Searchable/filterable complete module catalogue |
| `/how-it-works` | Product workflow explanation |
| `/optimization` | Cutting optimization explanation and evidence |
| `/worked-example` | End-to-end worked commercial/production example |
| `/screenshots` | Product screenshot/gallery presentation |
| `/pricing` | Pricing/licensing/commercial explanation |
| `/resources` | Documentation and resource index |
| `/blog` | Editorial index |
| `/blog/[slug]` | Individual article template; three current slugs × locales in build output |
| `/developer` | Developer/product-author story |
| `/collaborate` | Contribution/collaboration pathway |
| `/sponsors` | Sponsorship information |
| `/contact` | Demo/contact conversion flow |
| `/legal/privacy` | Privacy policy |
| `/legal/terms` | Terms |
| `/legal/license` | License terms |

### Operational/authenticated

| Route template | Purpose |
|---|---|
| `/records` | Authenticated live records dashboard |
| `/records/login` | Alias/reuse of reports authentication |
| `/reports` | Authenticated operational reports dashboard |
| `/reports/login` | Operator/customer authentication entry |
| `/deliveries` | Delivery-balance dashboard and movement ledger |
| `/job-orders` | Confirmed-proforma release/job-order workflow |

### Inquiry/customer/operator/estimator workflows

| Route template | Purpose |
|---|---|
| `/inquiries` | Internal inquiry workspace |
| `/inquiries/[id]` | Inquiry detail |
| `/inquiries/submit` | Authenticated customer submission entry/form |
| `/inquiries/submissions/[id]` | Customer submission tracking/detail |
| `/inquiries/customer-submissions` | Internal customer-submission review queue |
| `/inquiries/customer-submissions/[id]` | Internal submission review detail |
| `/inquiries/estimator` | Estimator assignment queue/workspace |
| `/inquiries/estimator/[id]` | Estimator inquiry detail |

### Internal design diagnostics

| Route template | Purpose |
|---|---|
| `/sys/clay` | Clay engine catalogue |
| `/sys/crystal` | Crystal/glass engine catalogue |
| `/sys/liquid-glass` | Liquid-glass interaction prototype/catalogue |

### API boundary

There are **27 `route.ts` API templates**, covering signout, health, demo requests, records, deliveries, exports, job-order list/progress/release, inquiries, private documents, communications, estimator registration/assignment/queues, customer drafts/uploads/finalization/profile/projects, and operator review. The build also exposes the existing `/api/og` handler.

## 4. Live-site inspection performed

Fetched and inspected rendered live responses for:

- `/` — product is accurately represented as installed Windows software with local SQLite, not SaaS; primary links resolve to contact/features; product preview and developer evidence are present.
- `/features` — live module catalogue exposes real WPF/product terminology, search/filter content, navigation labels, and 48 targets across nine groups.
- `/sys/crystal` — confirms the internal catalogue and shared-material model, including surfaces, forms, table seams, fallback doctrine, reduced motion, and Theme Studio access.
- `/reports` — correctly gates restricted operational data behind authentication and retains a return path.
- `/inquiries/submit` — correctly gates customer inquiry submission behind email verification.

The fetch tooling exposes rendered content but not visual viewport screenshots, computed styles, browser console/network traces, hover/touch behavior, or accessibility-tree output. Therefore desktop/tablet/mobile visual inspection is **not** claimed complete.

## 5. Existing theme architecture

### Authoritative pieces today

- `src/app/globals.css` — core semantic variables, typography, layout, surface grammar, states, responsive/accessibility rules, and some family mappings.
- `src/app/clay-engine.css` — Clay-specific material behavior.
- `src/app/crystal-engine.css` — Crystal/glass material behavior.
- `tailwind.config.ts` — semantic utility bridge (`ink`, `line`, `brand`, `text`, `state`, money, visualization, radii, shadows).
- `src/data/themes.ts` — mode/family registry, accent presets, persistence keys, and pre-paint boot script.
- `src/lib/useTheme.ts` — resolved mode/family external store and persistence.
- `src/lib/useStudio.ts` — large validated studio configuration, CSS-variable application, storage healing, sharing, typography, density, Clay and Crystal controls.
- `src/components/layout/ThemeStudio.tsx` — public live appearance drawer.
- `WEBSITE_DESIGN_SYSTEM.md` — current doctrine and component/surface inventory.

### Current family mapping

Current selectable families are Studio, Monet, Industrial, Crystal, and Clay. Crystal is the current glass/liquid identity and Clay is a separate engine. The requested taxonomy is broader and must not be faked by aliases:

- Claymorphism: substantially implemented.
- Glassmorphism: partly represented by Crystal/core glass grammar, but not independently defined from Liquid Glass.
- Liquid Glass: prototype/catalogue and Crystal behavior exist, but taxonomy and portable mapping need separation.
- Skeuomorphism: not a complete selectable family.
- Neumorphism: not a complete selectable family.
- Flat/Material-inspired: Studio/Industrial/Monet overlap these concepts but are not documented as the requested complete mapping.
- Hybrid: behavior exists informally but there is no typed hybrid composition schema.

### Current studio capabilities

Implemented: family selection, light/dark/system, accent presets/custom color, color strength, typography selection and attributes, UI scale/density, tint/radius/edges, money colors, Clay plate/depth controls, Crystal frost/body/glint controls, reset, persistence, share encoding, validation/self-healing, and immediate live application.

Missing or incomplete relative to the requested Theme Center: dedicated full-page editor, component comparison matrix, responsive preview frames, explicit motion intensity/speed controls, component-variant selection, named preset save/duplicate/rename/restore, valid technology-neutral JSON export/import, independently specified requested families, and clear separation of developer-grade controls from public marketing chrome. The current bottom-corner public drawer is useful but should not become the entire Theme Center architecture.

## 6. Shared component and interaction architecture

Existing reusable primitives include:

- `Button`/`ButtonLink`, badges, icon/index chips;
- `GlassCard`, metrics and dividers;
- `Container`, `Section`, `Flow`, `Cluster`, `AutoGrid`;
- `PageHero`, `PageBanner`, `SectionHeading`, CTA and legal layout;
- accordion/disclosure, slider, spotlight, empty state, stats, marquee;
- navigation, dropdowns/mobile menu, language controls, footer, support bubble;
- operational tabs, exports, tables, filters, pagination and detail modals;
- screenshot/product galleries;
- CSS `Reveal`, Motion `MotionReveal`, coordinated `MotionStagger`, route template motion.

The library is broad but enforcement is incomplete. Source census found approximately 257 hex occurrences, nine arbitrary Tailwind shadow declarations, and 115 backdrop-filter utility/style references. These numbers include legitimate token definitions, catalogue samples, computed palettes, and guarded engines; they are audit candidates, not automatic defects. A future check must classify allowlisted token/engine files separately from component/page violations.

## 7. Motion and performance findings

Strengths:

- One animation dependency (`motion`) rather than overlapping libraries.
- Central JS motion tokens mirror CSS tokens.
- Shared one-shot observer buckets for ordinary reveals.
- Reduced-motion and no-JS visibility safeguards.
- Stagger ceiling and fail-safe visibility.
- Transform/opacity preference and pointer capability gates.
- Production build succeeds with 505 generated static pages.

Risks requiring measurement:

- `globals.css` and material engines are large and historically layered; cascade ownership needs formal boundaries before adding families.
- The live Crystal catalogue states hundreds of blurred surfaces on `/features`; coarse-pointer folding exists, but GPU/paint cost needs browser profiling.
- 115 backdrop-filter references require classification and budget enforcement.
- `useStudio.ts` and `ThemeStudio.tsx` are very large; adding presets/export/comparison directly would worsen maintainability.
- The current studio writes many root variables; schema-first validation must precede expansion.
- No browser lab baseline, JS bundle report, CLS trace, or image waterfall was captured in this environment.

## 8. Accessibility and responsive findings

Existing strengths include localized `lang`/`dir`, semantic landmarks, skip link, visible focus grammar, keyboard/Escape-tested menus, semantic accordion relationships, reduced motion, coarse-pointer target floors, forced-colors fallback, logical properties, and responsive count-aware grids.

Unverified or at risk:

- Theme Studio has `role="dialog"` and Escape/outside dismissal, but source inspection did not establish a complete modal focus trap/inert-background contract; because it behaves as a non-modal drawer, semantics should be reviewed rather than blindly trapping focus.
- Public drawer controls are extensive in a narrow 24rem panel; mobile keyboard, zoom, and screen-reader traversal need real-browser verification.
- Glass/Clay contrast has automated pair tests, but new families need complete pair matrices before release.
- Long translated strings and RTL need representative visual inspection for every shared preview/editor control.
- Authentication-dependent forms, tables, errors, uploads, dialogs, and live regions require credentials/test data for end-to-end accessibility verification.

## 9. SEO, localization, data, and functionality to preserve

- Shared metadata helper, canonical and language alternates, OG/Twitter metadata, route schemas, sitemap, robots, and web manifest.
- 15-locale URL generation, prefix-free English behavior, visible localized redirects, remembered locale, country and `Accept-Language` resolution, and RTL direction.
- Contact/demo submission and all authenticated inquiry workflows.
- Supabase authorization, RLS assumptions, private document handling, staged uploads, finalization, audit events, estimator assignment and communications.
- Operational filters, pagination, exports, URL state, loading/empty/error states, login redirects, and desktop-authoritative write boundaries.
- Product wording that distinguishes the Windows WPF application from the website.

## 10. Baseline evidence

After restoring dependencies with `npm ci`:

- ESLint passed.
- TypeScript passed.
- Modernization contracts: 7/7 passed.
- Navbar source guards: 2/2 passed.
- Vitest: 8 files, 100/100 tests passed.
- Density/card tests: 19/19 passed; census passed.
- Production build passed and generated 505/505 static pages.
- Expected Motion reduced-motion informational warnings appeared in tests that intentionally emulate reduced motion.

Not rerun in this Phase 0 command: Clay/Crystal full material suites, pointer guard, job-order tests, inquiry/security suite, and i18n verification. They remain required phase gates and have existing scripts.

## 11. Risks and implementation order

1. **Protect the existing patch.** The working tree already contains extensive prior work; no reset, cleanup, or broad replacement is acceptable.
2. **Define a portable schema first.** Add a technology-neutral typed theme specification and versioned JSON mappings before creating more CSS families.
3. **Separate concepts.** Mode, visual family, accent, density, component variants, and motion intensity must be independently typed; Glassmorphism and Liquid Glass must not be aliases.
4. **Adapt current families.** Map existing Studio/Monet/Industrial/Crystal/Clay into the new schema without invalidating saved preferences.
5. **Build engines incrementally.** Add Skeuomorphic, Neumorphic, Flat/Material, and approved Hybrid mappings one at a time, each with contrast/fallback tests and catalogue evidence.
6. **Extract Theme Center modules.** Keep state/schema/persistence/export separate from preview panels and from the existing compact public drawer.
7. **Integrate through shared primitives.** Pages should not gain family-specific class branches.
8. **Automate governance.** Enforce family IDs, token completeness, portable schema validity, page-level hard-coded values, contrast pairs, and required variants with scoped allowlists.
9. **Verify in browser before completion.** The final phase requires real responsive, keyboard, reduced-motion, console/network, performance, and authenticated-state evidence.

## 12. Phase 0 conclusion

Phase 0 source and representative live-response audit is documented. It does **not** authorize broad redesign edits yet. Architecture work may begin only from the phased checklist, preserving the current theme engines and working functionality. Visual live-site inspection at desktop/tablet/mobile remains an explicit blocker rather than an assumed success.
