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

| # | Phase | Severity | Est. | Depends on | Status |
|---|---|---|---|---|---|
| [1](PHASE-01-transactions-and-concurrency.md) | Transactions and optimistic concurrency | **C** | 4-6 d | green build | **Part applied** `f683602` — glass-sheet race closed, proven against SQLite 3.40.1; consumables + tokens open |
| [2](PHASE-02-rounding-policy.md) | One rounding policy for all money | **C** | 2-3 d | green build | **Part applied** `d3c2b29` — single home + ratchet; 334 sites queued |
| [3](PHASE-03-vat-validation.md) | Make VAT submission validation real | **C** | 1 d | green build | **DONE** `601a76a` |
| [4](PHASE-04-licensing-hardening.md) | Licensing hardening | M | 2-3 d | — | **Part applied** `a04632f` — 4.1 done; 4.2-4.4 open |
| [5](PHASE-05-tax-and-currency-config.md) | Tax and currency as configuration | M | 5-8 d | 2, 3 | **Part applied** `3b056a5` — 5.3 precision tied to the books currency and tested; 5.1/5.2/5.4/5.5 need a green build |
| [6](PHASE-06-utc-everywhere.md) | UTC everywhere | M | 3-4 d | — | **Part applied** `75778dd` — 224 sites classified, in-memory intervals converted, ratchet pinned; 89 persisted sites need a data migration |
| [7](PHASE-07-startup-and-di.md) | Startup resilience and DI completeness | M | 2 d | — | **Part applied** `d5df6a8` — 3 of 4; splash delay open |
| [8](PHASE-08-pagination.md) | Finish pagination | M | 4-5 d | — | **Part applied** `48090b6` — all 11 sites classified: six must NOT be paginated, four are real list screens needing a pager |
| [9](PHASE-09-security-hardening.md) | Security hardening | m | 2 d | — | **Part applied** `deec7dd` — 9.1, 9.2 done; 9.3, 9.4 open |
| [10](PHASE-10-decomposition.md) | Decompose the god files | m | 5-7 d | 7 helps | **Mapped** `e88cbcf` — premise corrected (no 12,162-line file); `LoadAsync` at 350 lines identified as the first move |
| [11](PHASE-11-theme-consolidation.md) | Theme and design-system consolidation | m | 3-4 d | — | **Part applied** `ee5cee7` — dark/light parity proven and enforced (225/225); consolidation deferred |
| [12](PHASE-12-rules-single-home.md) | One home for every rule | m | 3 d | — | **DONE** `4c5b53c` — 12.1/12.2 were already complete; low-stock threshold given one home |
| [13](PHASE-13-test-depth.md) | Test depth where money is computed | M | 4-6 d | 1, 2, 3 | **Part applied** `dfa8c0b` — build-type gap closed |
| [14](PHASE-14-observability.md) | Observability and audit completeness | m | 3 d | 1, 7 | **Part applied** `972f445` — 14.1 log levels; health panel open |
| [15](PHASE-15-release-hardening.md) | Release hardening | L | 2-3 d | all | **Part applied** `56a6244` — 15.3 measured; 15.1/15.4/15.5 open |

## Every phase now has real work behind it

All fifteen have been taken as far as this environment safely allows, each with
its own commit. Two are complete (3 and 12); the rest are partial, and in every
case the remaining piece is recorded in the phase file with the reason it was
stopped.

The blocking reason is the same almost everywhere, and it is worth being exact
about what it is and is not. It is **not** "these phases are too hard" or "the
code is unclear". It is that this workspace has no .NET SDK and no network, so
nothing here has ever been compiled, and four classes of change cannot be made
safely without a compiler:

| class | why a compiler is required | phases |
|---|---|---|
| signature changes rippling across namespaces | a `const` is inlined at every call site; changing it is not a local edit | 1, 5 |
| schema migrations | a migration can silently change a reported number via a missing WHERE | 1, 5, 14 |
| XAML layout edits | a control that cannot be seen cannot be checked | 8, 11 |
| file and method splits | "behaviour unchanged" depends entirely on the compiler catching a lost `using` — and `TreatWarningsAsErrors` makes that a hard error | 10 |

What has been done instead is the part that does not need a compiler, and that
is worth more than it sounds: measured classifications rather than estimates,
ratchets that hold a line while the rest waits, and three premises corrected
against the actual code.

Three of those corrections matter enough to repeat here, because each one would
have produced confident, plausible, wrong work:

- **Phase 12** claimed proforma status had no canonical home.
  `Models/ProformaInvoice/Common/ProformaInvoiceStatus.cs` already existed with
  all six constants, an `All` list and a `Normalize`. The real duplicate was
  elsewhere — a low-stock threshold that was 2 in one place and 5 in another.
- **Phase 10** was scoped to a "12,162-line MainViewModel". That file does not
  exist; `MainViewModel` is 1,467 lines across three partials and the largest
  file in the repository is 1,933.
- **Phase 8** was scoped as "20 call sites to paginate". Six of the eleven that
  remain are lookups, filter sources or search, where adding pagination is a
  regression rather than a fix.

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
