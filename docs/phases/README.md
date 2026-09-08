# Upgrade Phases

<!-- pg-nav -->
> **[Repository root](../../README.md)** · [All documents](../../docs/README.md) · [Application workflow](../../docs/WORKFLOW.md) · [Design system](../../docs/DESIGN_SYSTEM.md)
<!-- pg-nav -->

Fifteen phases taking ProGlass Automation from a strong WPF application to
industrial ERP grade. Produced by the deep analysis in
[`../DEEP_ANALYSIS_2026-09-09.md`](../DEEP_ANALYSIS_2026-09-09.md), which carries
the file-and-line evidence for every finding below.

## How to use these

**Ordered by risk of harm, not by effort.** Phases 1-3 fix defects that produce
wrong or lost data. Everything after them is structural.

Each phase file is self-contained and follows the same shape:

- **Evidence** — the code, quoted, with file and line
- **Steps** — ordered, with the reasoning for the order
- **Acceptance criteria** — checkable, not aspirational
- **Must not change** — the parts that are already right

That last section matters as much as the rest. This codebase contains genuinely
senior work — the SQLite hardening, the password hashing, the four exception
sinks, the licensing crypto, the pure pricing core. A blunt upgrade pass would
damage it. Every phase says what to leave alone.

## Prerequisite

**Nothing committed since `b86a3bc` has been compiled.** There was no .NET SDK
in the environment it was written in. Do not start any phase without a green
build — see [`../BUILD_VERIFICATION_GUIDE.md`](../BUILD_VERIFICATION_GUIDE.md).

Phase 15.1 is what produces that green build automatically; it needs a human
with GitHub admin rights, because the App pushing this repo lacks the
`workflows` scope.

## The phases

| # | Phase | Severity | Est. | Depends on |
|---|---|---|---|---|
| [1](PHASE-01-transactions-and-concurrency.md) | Transactions and optimistic concurrency | **C** | 4-6 d | green build |
| [2](PHASE-02-rounding-policy.md) | One rounding policy for all money | **C** | 2-3 d | green build |
| [3](PHASE-03-vat-validation.md) | Make VAT submission validation real | **C** | 1 d | green build |
| [4](PHASE-04-licensing-hardening.md) | Licensing hardening | M | 2-3 d | — |
| [5](PHASE-05-tax-and-currency-config.md) | Tax and currency as configuration | M | 5-8 d | 2, 3 |
| [6](PHASE-06-utc-everywhere.md) | UTC everywhere | M | 3-4 d | — |
| [7](PHASE-07-startup-and-di.md) | Startup resilience and DI completeness | M | 2 d | — |
| [8](PHASE-08-pagination.md) | Finish pagination | M | 4-5 d | — |
| [9](PHASE-09-security-hardening.md) | Security hardening | m | 2 d | — |
| [10](PHASE-10-decomposition.md) | Decompose the god files | m | 5-7 d | 7 helps |
| [11](PHASE-11-theme-consolidation.md) | Theme and design-system consolidation | m | 3-4 d | — |
| [12](PHASE-12-rules-single-home.md) | One home for every rule | m | 3 d | — |
| [13](PHASE-13-test-depth.md) | Test depth where money is computed | M | 4-6 d | 1, 2, 3 |
| [14](PHASE-14-observability.md) | Observability and audit completeness | m | 3 d | 1, 7 |
| [15](PHASE-15-release-hardening.md) | Release hardening | L | 2-3 d | all |

**C** critical · **M** major · **m** moderate · **L** low

## If you only do three things

1. **Phase 1.** Two operators can silently corrupt stock quantities today.
   `SheetStoreRepository.UseStockAsync` reads, checks and writes with no
   transaction and no concurrency token, on an app that ships a shared LAN data
   folder and a colleague-presence service.
2. **Phase 3.** One day of work. The VAT submission report prints
   "0 invalid" on every filing because the validator's `IsValid` is initialised
   to `true` and never set to anything else.
3. **Phase 2.** 122 call sites round money half-to-even; 8 round half-away-from-
   zero. The pricing engine and the invoice layer disagree, so
   `subtotal + vat ≠ grand` on a predictable subset of invoices.

## What was already fixed, and should not be redone

Recorded so a fresh pass does not re-derive it:

- All seven dashboards read aggregates from `IDashboardMetricsService`; none
  loads a table.
- The Master-DN filter gap — present in two places in the metrics service,
  absent from every repository query — is fixed and pinned by
  `MasterDnFilterParityTests`.
- Single-row lookups, pickers and full-text searches are off whole-table loads.
  13 sites across 11 ViewModels remain, all real list screens (Phase 8).
- Six instances of the "one rule, two homes" defect class are fixed, each with a
  ratchet that fails in both directions.
- The CSV import that reloaded the entire sheet table once per imported row.
- The proforma picker that read a snapshot blob to bind six scalar columns.

## Two claims that could not be reproduced

Both appear in earlier notes for this repo and both are marked **UNVERIFIED** in
the analysis. Establish the facts before acting:

- **~51 self-referencing resource keys in `DashboardStyles.xaml`** — two
  independent passes over all 229 XAML files returned zero.
- **A fourth design system alongside three** — `Themes/CrystalFluid/` exists, but
  whether it is a parallel system or a layered family was not established.

A consolidation built on a wrong premise deletes working code. → Phase 11.

<!-- pg-nav -->
---

*This README documents `docs/phases/`. For the end-to-end business chain see [`docs/WORKFLOW.md`](../../docs/WORKFLOW.md); for every other document see [`docs/README.md`](../../docs/README.md).*
<!-- pg-nav -->
