# ProGlass Website Component Library

This is the approved website UI vocabulary. Components consume semantic tokens; material families repaint those tokens. A page must not fork markup by family.

The machine-readable registry is `src/theme/component-contracts.ts`.

## Core rules

1. Prefer native elements and preserve their keyboard/browser behavior.
2. Reuse an approved component before creating a page-local pattern.
3. Add a reusable variant to the central component when the same need occurs twice.
4. Family engines may change material, not semantics or accessible state.
5. Loading never introduces an artificial delay. Motion is optional feedback, not a gate.
6. Error/success/warning states always include text or an accessible name, never color alone.
7. All controls retain the global focus-visible contract, forced-colors support, and reduced-motion behavior.

## Approved primitives

| Need | Approved implementation | Contract |
|---|---|---|
| Button/link | `ui/Button.tsx` | primary/secondary/ghost/quiet; sm/md/lg; loading and disabled |
| Card/metrics | `ui/Glass.tsx` | primary/raised/card; one primary subject per section |
| Layout | `ui/Container.tsx`, `ui/Layout.tsx` | Section, Flow, Cluster, count-aware AutoGrid |
| Heading | `ui/SectionHeading.tsx`, `layout/PageHero.tsx` | editorial/document/showcase hierarchy |
| Badge/status marker | `ui/Badge.tsx` | neutral/accent/success/warning/danger |
| Field | `ui/Field.tsx` | native control + label/hint/message; default/error/success/readonly; sm/md/lg |
| Feedback | `ui/Feedback.tsx` | empty/error/success/unavailable/info with optional action |
| Loading | `ui/Loading.tsx` | decorative Skeleton, native Progress, named Spinner |
| Disclosure | `ui/Accordion.tsx` | native button, `aria-expanded`, controlled region |
| Empty state | `ui/EmptyState.tsx` | retained legacy wrapper; migrate toward Feedback where equivalent |
| Reveal | `ui/Reveal.tsx`, `motion/MotionReveal.tsx`, `motion/MotionStagger.tsx` | CSS lane by default; Motion only for coordinated/stateful sequences |
| Navigation | `layout/Navbar.tsx`, `layout/LanguageMenu.tsx` | desktop/touch, keyboard, Escape, locale-safe routing |
| Footer/legal | `layout/Footer.tsx`, `layout/LegalPage.tsx` | shared site/legal structures |
| Galleries | `gallery/ScreenshotGallery.tsx`, `gallery/ProductShowcaseViewer.tsx` | preserve image semantics and viewer controls |
| Operational controls | `operations/OperationsTabs.tsx`, `operations/ExportActions.tsx` | task-first, minimal decorative motion |
| Tables/filter/pagination | domain shared components under `records/`, `deliveries/`, `job-orders/` | preserve URL/data/auth behavior; candidates for later cross-domain extraction |

## Field contract

`Field` accepts a native `input`, `select`, or `textarea` element. It generates stable label/help/message relationships and publishes `data-field-state`. It does not own the value or replace validation. Server/backend errors remain authoritative.

## Feedback contract

`Feedback` provides visible text and appropriate live-region behavior. Error is assertive; success is polite; empty, unavailable, and informational surfaces are not announced repeatedly.

## Loading contract

- `Skeleton` is decorative (`aria-hidden`) and has a still reduced-motion state.
- `Progress` uses native `<progress>` semantics and clamps determinate values.
- `Spinner` has a visible and screen-reader-readable label.

## Known consolidation work

The codebase contains domain-specific dialogs, filters, tables, pagination, tabs, and skeletons. They are not to be mass-replaced. Phase 5 integration should migrate them only after contract comparison confirms that focus management, query state, authorization, and business behavior are preserved. Dialog/popover/tooltip extraction requires a dedicated interaction audit before an approved generic primitive is introduced.
