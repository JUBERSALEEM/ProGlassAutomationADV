<div align="center">

<img src="README_ASSETS/hero.png" alt="ProGlass Automation" width="900"/>

# ProGlass Automation

### Industrial glass-fabrication ERP — from first inquiry to final statement

**WPF · .NET 8 · C# 12 · MVVM · SQLite + EF Core · 15 languages**

<br>

![Version](https://img.shields.io/badge/version-2.0.0-0E6A8A?style=for-the-badge)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/Platform-WPF-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![C%23](https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp&logoColor=white)
![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)
![Tests](https://img.shields.io/badge/tests-1%2C148-2E7D32?style=for-the-badge)
![Languages](https://img.shields.io/badge/languages-15-C2185B?style=for-the-badge)
![License](https://img.shields.io/badge/license-Proprietary-lightgrey?style=for-the-badge)

<br/>

`Inquiry ➜ Quote ➜ Confirm ➜ Produce ➜ Deliver ➜ Invoice ➜ Settle`

**Every step carries the last one forward. Nothing is re-keyed.**

[🧭 Application Workflow](docs/WORKFLOW.md) ·
[📚 All Documents](docs/README.md) ·
[🗺️ Upgrade Roadmap](docs/ERP_UPGRADE_ROADMAP.md) ·
[🎨 Design System](docs/DESIGN_SYSTEM.md) ·
[⚠️ Money-Path Policy](docs/MONEY_PATH_ERROR_POLICY.md)

</div>

---

## What this is

ProGlass Automation is a Windows desktop ERP built for **glass fabrication
businesses** — estimation, production, inventory, dispatch, VAT invoicing,
statements and reporting.

Its defining property is **traceability**. A quotation becomes a job order
becomes a delivery becomes an invoice becomes a settled balance, and each
step is a *conversion of the previous record*, not a fresh data entry.
Every money figure persists the calculation context it was computed with,
and reporting reads that persisted figure rather than recomputing it.

That last point is the whole product. An ERP that quietly recalculates
yesterday's quote with today's glass prices is not an ERP.

---

## The business chain

```
┌──────────┐  convert   ┌────────────────┐  confirm  ┌───────────┐
│ INQUIRY  │───────────▶│ PROFORMA (PI)  │─────────▶│ JOB ORDER │
│  + BOQ   │            │  9-step wizard │          │ (factory) │
└──────────┘            └───────┬────────┘          └─────┬─────┘
                                │                         │ dispatch
                                ▼                         ▼
                    Step 9 P&L snapshot ◀───────  DELIVERY NOTE
                                │                         │
                                ▼                         ▼
                       JOB PROFITABILITY ◀──── TAX INVOICE (5% VAT)
                        (staleness-flagged)             │
                                                        ▼
                                             STATEMENT + REPORTS
```

| Link | What crosses | What is guaranteed |
|---|---|---|
| **Inquiry → PI** | BOQ rows: build, spec, colours, W×H, qty, expected rate | Atomic copy. A row that cannot import stays on the inquiry as a note — never dropped, never guessed. Permission-gated and logged. |
| **PI → Job Order** | Real grid geometry, not a summary | Step 4/5 consistency: sheet selections and spacer gaps survive the handoff |
| **PI ↔ Step 9** | Profit & loss snapshot | Cost basis pinned via `GlassPriceStamp`. **Reporting never recomputes money — it flags drift.** |
| **Job → Delivery** | Pending pieces | Multi-movement dispatch accumulates; pending-piece maths is double-count-safe and lives in one place |
| **Company ⇄ PI** | All 8 legal-entity fields, both directions | Preserve-blank on invoice save. A missed invoice field can never erase the master record. |

Full detail, with the code path for each handoff: **[`docs/WORKFLOW.md`](docs/WORKFLOW.md)**.

---

## At a glance

Numbers below were measured against this tree on 2026-09-09, not copied from
an earlier release note. The measurement is stated so you can reproduce it and
disagree with it.

| | |
|---|---|
| **Production code** | 1,112 C# files · 225,915 lines |
| **UI** | 229 XAML files · 114,658 lines |
| **Tests** | 186 files · **1,396 `[Fact]`/`[Theory]`** · 33,405 lines |
| **Test-to-production ratio** | 1 : 6.8 |
| **Modules** | 24 view areas · 38 service domains |
| **Data** | 12 SQLite databases, one `DbContext` each, additive-only migrations |
| **Authorisation** | 8 roles (`AppRoles.All`) + 1 legacy · 29 granular `Module.Capability` permissions |
| **Localisation** | 15 locales as pure XAML dictionaries — adding one needs no C# change |
| **Debt markers in shipped code** | **0** TODO · **0** FIXME · **0** `NotImplementedException` · **0** empty `catch` |

That last row is the one to look at. It is policed by a test
(`Tests/Security/SilentCatchAuditTests.cs`), not by discipline — which is
the only way it stays true.

**How to reproduce these.** Production C# is every `.cs` outside `Tests/`,
`obj/` and `bin/`; tests are everything under `Tests/`; the
`[Fact]`/`[Theory]` figure counts live attributes (zero are commented out).

> **Correction, 2026-09-08.** An earlier revision of this table reported
> *1,273 production C# files*. That number counted **every** `.cs` in the
> tree, tests included. At the time that was 1,106 production plus 167 test
> files. The ratio was therefore computed against an inflated production
> figure. Both numbers above are production-only, and the honest ratio at the
> time was 1 : 7.4, not 1 : 7.6. It has since improved to **1 : 6.8** as the
> 2026-09-09 work added 88 tests against 1,331 new production lines.
> The class count was dropped rather than restated: "test classes" has no
> stable definition here (nested helper classes and shared fixtures make any
> count arbitrary), and a number that cannot be defined cannot be measured.

---

## Proforma Invoice — the 9-step wizard

```
Step 1 Company ──▶ Step 2 Client ──▶ Step 3 Project ──▶ Step 4 Pricing
     ▲                                                    ▼
Step 9 Profit & Loss ◀── Step 8 Attachments ◀── Step 7 Summary
     ▲                        ▲                     ▲
     └────────── Step 6 Charges ◀── Step 5 Spec Grid
```

| Step | Industrial behaviour |
|---|---|
| **1 — Company** | Hard-linked to Company Center: loading a card copies all 8 legal-entity fields (name, TRN, location, representative, mobile, e-mail, bank account, bank name, logo). Step 1 edits **enrich the master at save** under a preserve-blank contract. The old default-mark mechanism was removed 2026-09-04 — the selected company is the only authority that can land on a money document. |
| **2 — Client** | Customer, TRN, billing profile; suggestion dedup and auto-registration. |
| **3 — Project** | Project identifier, contract reference, site mapping. |
| **4 — Pricing** | Rates per build type (SGU / DGU / LAM / DGU+LAM), sheet selection, air-spacer, processing, per-section capture. **Per-group production provenance** — each section keeps its own inputs, so a multi-section quote never costs every group with section 1's panel. |
| **5 — Spec Grid** | Multi-spec grid (W1/H1/W2/H2/qty → SQM + value), inquiry-BOQ imports, manual overrides carrying provenance fields. |
| **6 — Charges** | Predefined + wastage + custom. **Auto-imports sheet colours from Step 5** — 7 colours produce 7 rows, never hardcoded. |
| **7 — Summary** | VAT 5% round-safe, discount, totals. |
| **8 — Attachments** | Any-file evidence chain, SHA-256 manifest dedup. |
| **9 — Profit & Loss** | **The single pricing engine.** Sheet cost from Bin Store buy-prices, production rows from the spec's own provenance, target-price KPIs, side-by-side buy-vs-sell cost basis, wastage accounting. Its snapshot feeds Job Profitability, Statements and Reports. |

---

## Modules

| | Module | Notes |
|---|---|---|
| 🏠 | **Dashboard** | Seven module dashboards on one shared grammar ([`Styles/DashboardKit.xaml`](Styles/DashboardKit.xaml)) + an order-to-cash pipeline rail on Overview: Quote → Production → Delivery → Invoice → Payment. Every page renders all four states — loading, error, empty, data. |
| 📨 | **Inquiries** | Pre-sales register with scope-of-supply (BOQ), attachments with SHA-256 dedup, lifecycle log. |
| 🧾 | **Proforma Invoice** | The 9-step wizard above. |
| 🏭 | **Job Order** | Factory workload, priority, production status. |
| 🚚 | **Delivery Note** | Multi-movement dispatch → signed → received, with pending-piece tracking. |
| 🧮 | **Tax Invoice** | VAT-compliant conversion from PI; `Issued` → `Paid` / `Overdue`; version history. |
| 📄 | **Statement** | Customer balances grouped from invoices. |
| 📦 | **Sheet Store** | Stock, buy/sell prices, suppliers, purchase/use history. Every write stamps a price-version signal. |
| 🏺 | **Specifications** | Reusable spec library, live in Step 5. |
| 📈 | **Job Profitability** | Reads the approved-PI snapshot; flags `⚠` when sheet-store prices moved. |
| 📊 | **Reports** | Data-driven reporting on persisted figures. |
| 🏢 | **Company Center** | Legal-entity profiles with field-diff audit on every change set. |
| 👥 | **System Center** | Users, roles, operation-based permissions. |
| 🧰 | **Logs & Activity** | App log, activity log, audit DB with GitHub-style diffs, crash reports with error IDs. |
| ⚙️ | **Settings** | Appearance, themes, module wallpapers, feature toggles, backup/restore, updates, licensing. |
| 🤖 | **AI Assistant** | Cloud providers plus an always-offline local brain — no key and no network still answers from the knowledge base and live data. |
| ✂️ | **Optimization** | Cutting-layout module (placeholder, V2). |

---

## Architecture

```
ProGlassAutomationADV/
├── Views/ + ViewModels/       MVVM; one folder per business module
├── Services/                  38 domains — repositories, workflows, export,
│   ├── ProformaInvoice/       audit, licensing, theme, AI, dashboard metrics
│   ├── Audit/                 OperationGuard + AppRoles + AppPermissions
│   ├── Dashboard/Core/        aggregate DTOs; ViewModels never see EF entities
│   └── Database/              12 DbContexts, additive-only migrations
├── Controls/ Themes/ Styles/  design system, chart controls, themes
├── Models/                    domain models, one folder per module
├── Tests/                     xUnit — 1,394 tests
├── docs/                      140 documents, indexed at docs/README.md
└── scripts/                   verification and generation tooling
```

**Platform properties**

- 🔐 **Authorisation at the service boundary** (`OperationGuard`). Hiding a
  button is not a permission check; the repository enforces the rule.
- 📝 **Audit on every meaningful write** — before/after diffs with session,
  actor and module.
- 💾 **SQLite, WAL, busy-timeouts**, migrations wrapped
  backup → transaction → rollback. Never destructive.
- 🔄 **OTA updates** from GitHub Releases; `<Version>` in the csproj is the
  single version source.
- 🧩 **Partial-class decomposition** for large ViewModels — the split is
  deliberate, not accidental.
- 🧪 **Workflow regression net** pins company semantics, inquiry→PI
  conversion, price-basis determinism and status-vocabulary funnels.

---

## The honesty contract

A professional ERP must never silently invent money. These are enforced
rules, not aspirations.

| Rule | Enforced in |
|---|---|
| Reporting **reads** money from the persisted Step-9 snapshot; it never recomputes it | `JobProfitabilityViewModel`, `ReportingDataService` |
| A stale price basis is **flagged `⚠`**, never silently rescored | `GlassPriceStamper` |
| Statuses enter through one normalising home. **Unknown statuses pass through — they are never remapped to something plausible** | `ProformaInvoiceStatus`, `InquiryStatuses` |
| Every money-path `catch` is classed A–D and documented | [`docs/MONEY_PATH_ERROR_POLICY.md`](docs/MONEY_PATH_ERROR_POLICY.md) |
| Company profile: preserve-blank on invoice save, field-diff trail on every change | `MasterDataRepository` |
| No silent catches anywhere in shipped code | `Tests/Security/SilentCatchAuditTests.cs` |

---

## Engineering health — an honest assessment

> **Read this before trusting any number below.** Two self-assessments exist
> in this repository and they disagree: `docs/ERP_UPGRADE_ROADMAP.md` claims
> **9.7/10** (after Phase 4) while `docs/CODE_RATING_REPORT.md` (2026-09-02) rates
> **9.0/10** after an independent deep audit. Neither was produced by a green
> `dotnet build` + `dotnet test`, because **this repository has no CI**. Treat
> both as engineering judgement, not as measured results. A first manual build
> was run on 2026-09-09 — see the P1 row in the table below — which is evidence
> about the code, but it is not a green build and it is not repeatable.

### What is genuinely strong

| Area | Evidence |
|---|---|
| **Domain correctness** | One pricing engine, one status vocabulary per module, pinned cost basis. The money path is designed, not accumulated. |
| **Error discipline** | Zero empty catches and zero raw `ex.Message` to users in shipped code; `ExceptionMessages.Deep()` everywhere; correlation IDs end-to-end. |
| **Data safety** | Migrations run backup → transaction → rollback and are additive-only. Delete guards check downstream documents first. |
| **Security** | RSA-2048-PSS with no v1.5 fallback, DPAPI for secrets, PBKDF2 for passwords, machine fingerprinting, rate-limited key generation. |
| **Test intent** | 1,394 tests targeting the *workflow invariants* rather than getters — company semantics, conversion, price-basis determinism. |
| **Hygiene** | Zero TODO/FIXME/`NotImplementedException` in 225,915 lines of production code. Rare at this size. |

### What is open

Every item below has a phase, a concrete action and an acceptance test in
**[`docs/REMEDIATION_PLAN.md`](docs/REMEDIATION_PLAN.md)**.

| Priority | Issue | Evidence |
|---|---|---|
| **P0** | **Stock counter race — glass sheets fixed, consumables still open.** `SheetStoreRepository.UseStockAsync` read, checked and wrote stock in three steps, so two operators issuing from the same sheet both passed a check against the same stale balance and one movement was lost silently. The check now lives in the UPDATE's WHERE clause, so SQLite evaluates it and applies the increment under one write lock; 6 regression tests run against a real SQLite file. **Still open:** `InventoryItemRepository.ConsumeAsync` and `ReceiveAsync` have the identical race — both read `item`, check, then do `item.Used += quantity` / `item.Stock += quantity`, so the second writer overwrites the first. **Corrected 2026-09-09:** the claim that this "needs a migration" is wrong. `IsConcurrencyToken()` is model-only and a guarded `ExecuteUpdateAsync` needs no schema change at all — only a `rowversion`/`[Timestamp]` column would need one, and SQLite has no native rowversion. Both fixes are genuinely available. What actually blocks them is narrower and worth stating precisely: the SQL-arithmetic fix is ruled out on measured evidence (the columns are TEXT affinity, so `Used + @q` routes through a REAL and loses precision above 15 significant digits, while the schema permits 18,2), and the two that remain each carry a risk that cannot be retired without running the app — an equality guard on a TEXT column can fail permanently against rows whose stored formatting differs from EF's (a CSV-import path exists), and `BEGIN IMMEDIATE` cannot be assumed because the one transaction in the tree is a raw deferred `BeginTransaction()` with no isolation level. | `docs/phases/PHASE-01-transactions-and-concurrency.md` |
| ~~P0~~ | ~~**The VAT submission report validates nothing.**~~ **Fixed 2026-09-09.** `IsValid` is now derived from `Errors.Count` so the flag cannot drift from the list beside it. `ValidateInvoice` rejects on irreconcilable VAT and on VAT charged to a zero total, warns (but still files) on negatives, zero-rated supplies and future dates, and `ReportingDataService.Vat.cs` logs *which* invoice was excluded and why instead of only a count. 9 tests. | `docs/phases/PHASE-03-vat-validation.md` |
| **P0** | **Two rounding policies in one money pipeline — structural fix applied, migration open.** `Services/Reporting/MoneyRounding.cs` is now the single declaration, the misleading "banker's rounding" comment is corrected, and the VAT and invoice layers route through it (behaviour-identical — they already rounded `AwayFromZero`). Policy: [`docs/MONEY_ROUNDING_POLICY.md`](docs/MONEY_ROUNDING_POLICY.md). **What remains:** 334 call sites in 62 files still inherit the framework default `ToEven`. They are *not* converted blind, because the same call rounds square metres to 3-4 dp elsewhere in the same files and there is no compiler here to catch a misclassification. `MoneyRoundingRatchetTests` pins the count at 334 and fails on any growth, so the migration proceeds one file at a time. | `docs/phases/PHASE-02-rounding-policy.md` |
| **P0** | **No CI — deliberately deferred, not overlooked.** `.github/workflows/` does not exist. Four complete workflow files sit in `docs/ci/` and `docs/CI/`, ready to drop in. Phase 0 is **skipped by the owner's instruction** until a machine is available; the GitHub App token in this environment also lacks the `workflows` scope, so the files could not be committed from here regardless. Until then nothing proves this tree builds automatically — the 2026-09-09 build below was run by hand. | `ls .github/` → `CODEOWNERS`, `dependabot.yml`, `pull_request_template.md` |
| **P1** | **Whole-table loads — 11 sites left, and six of them must *not* be paginated.** Down from 25 / 18. Every lookup, dashboard aggregate, picker and full-text search is done: those were the categories where adding a page would have silently dropped the row being looked for. **Corrected 2026-09-09:** "all of them real list screens" was wrong. Read individually, six of the eleven are lookups or complete-set consumers where adding a page is a regression — three feed `RebuildFilterOptions()`/`ApplyItemFilters()` so pagination silently drops filter choices, one feeds a progressively-narrowed stock match that would report "no matching sheet" for a sheet that exists, one is a duplicate check that would start creating duplicate statements, and one is full-text search that would stop finding anything past page one. None of those fails loudly. **Four** are genuine list screens (`OrderReportListViewModel`, `StatementListViewModel`, `TaxInvoiceGodViewViewModel`, `HistoryViewModel`); the eleventh file is both, with a picker source at line 719 and a list at line 797. Those four need a pager in XAML, which is a change to make with a compiler open. `PagedResult<T>` and `GlobalQueryEngine.ExecutePagedQueryAsync` already exist; `InquiryListView.xaml:1403-1430` is the pager to copy. The ratchet holds at 11 and fails on growth. | `docs/phases/PHASE-08-pagination.md` |
| ~~P1~~ | ~~**UI virtualization gaps on list screens.**~~ **Resolved 2026-09-09.** Pagination (above) bounds how many rows are *loaded*; virtualization bounds how many are *realized into visual containers*, and the two are independent — a fully-loaded list still renders smoothly for hundreds of thousands of rows if it recycles containers. An audit of all 103 view XAMLs found four genuine large-data lists that set `VirtualizingPanel.IsVirtualizing="True"` but omitted `ScrollViewer.CanContentScroll="True"`, so WPF silently fell back to realizing every row (the classic "virtualization declared but not engaged" trap): `OrderReportListView`, `SheetStoreView` (Items), `SheetDetailWindow` (Movements) and `OrderReport/PreviewStep`. All four now carry `CanContentScroll=True` + `ScrollUnit=Pixel` + a `400,800`px recycling cache, matching the already-correct `ModuleReportListBoxStyle` that every report list shares. Deliberately **not** touched: the PI Step-5 line-items grid (intentionally non-virtualized for Excel-style cell navigation, and bounded per-invoice), and `ItemsControl`s nested inside an outer `ScrollViewer` (Settings, System Center activity feeds) where virtualization needs a structural change a compiler must verify. | commit below |
| **P1** | **First real build happened 2026-09-09 — 9 errors, 2 root causes, both fixed.** This row was **P0 "nothing has been compiled"** and it is now materially different. The owner built on Windows and reported nine errors; every one traced to two mistakes made while writing blind. **(1)** `SearchPattern.EscapeChar` was declared `const char`, but the overload is `Like(this DbFunctions, string, string, string? escapeCharacter)` — a string. One declaration change cleared all eight CS1503s. The error said *argument 4* for a three-argument call because an extension method's implicit `this` receiver counts as the first argument. **(2)** `TaxInvoiceDetailViewModel.InitNew.cs:187` still referenced `allDns`, the whole-table load that `GetByProformaIdAsync` replaced — a rename that missed one use (CS0103). Fixed in `0673ffd`. Rather than patch only what was reported, every changed file was diffed against the base for removed declarations with surviving references: six candidates, five false positives, one real. The **absence** of IDE0005 in that error list is evidence too, not luck — it is not in `WarningsNotAsErrors` while `TreatWarningsAsErrors` is on, so a single orphaned `using` would have appeared. **Still unproven:** whether the tree is now clean. Rebuild and report. | commit `0673ffd` |
| **P2** | **Test ratio 1 : 6.8.** Improved from 1 : 7.4 as the 2026-09-09 work added 88 tests, but still thin for software that computes money. The pricing engine in particular deserves property-based coverage. The earlier *1 : 7.6* figure was computed with test files counted as production — see the correction in [At a glance](#at-a-glance). | 33,405 test lines vs 225,915 |
| **P2** | **Large classes.** 13 files exceed 990 lines; `JobProfitabilityViewModel.cs` is 1,933 and contains **no `#region` markers at all**. The real target is not the file but `LoadAsync` — **350 lines** (1187-1536), larger than every method in most files, and the entry point where every data dependency is visible at once. Extracting its stages would do more than splitting the file. Four of the 13 already carry `Part1` in the name: those splits were started and not finished. | `find . -name '*.cs' \| xargs wc -l` |
| **P2** | **No obfuscation.** Release binaries are readable in dnSpy. Acceptable for desktop, a raised bar for a commercial product. | `docs/CODE_RATING_REPORT.md` |
| **P3** | **One acknowledged hack, still open — and the blocker is now precise.** `OtherChargeRow.MetricQty` hardcodes delivery quantity to `1m` per invoice (`if (IsDeliveryRow) return 1m;`), so a multi-trip delivery cannot be billed without a manual Custom Charge. Documented, deliberate, and marked *"proper fix planned for Chat 8"*; Chat 8 happened, the fix did not. **Step 1 is in place:** characterisation tests pin current behaviour for every branch. What the real fix needs is now stated rather than assumed, and it is why this stays open: a settable per-trip quantity means a **persisted field**, an **editable UI column**, and **PDF/Excel export parity** — the comment says the current `1m` "matches PDF/Excel export behavior", so all three must change together or the printed invoice stops agreeing with the screen. That is a money-path change across schema, UI and export with no compiler; the characterisation tests are the precondition, not the fix. | `OtherChargeRow.cs:316` · `OtherChargeRowMetricQtyCharacterisationTests.cs` |
| ~~P3~~ | ~~**25 `.md` files in the repository root.**~~ **Resolved 2026-09-08** — 24 audit/analysis documents moved to `docs/` with `git mv`, all inbound references rewritten, links re-verified. `README.md` is now the only root document. | `ls *.md` |
| **P3** | **No anti-debug checks.** A debugger can patch memory to bypass licensing. Acceptable for desktop ERP; not zero-risk. | `docs/CODE_RATING_REPORT.md` |
| ~~P1~~ | ~~**Status vocabularies disagreed between screens.**~~ **Resolved 2026-09-08** — see the section below. | commits `122a32b`, `fdfea80`, `5c10f90` |
| ~~P1~~ | ~~**The largest status-literal offender still had 17 raw comparisons.**~~ **Resolved 2026-09-09** — all 17 in `DeliveryNoteRepository.Part1.cs` now read from `DeliveryNoteStatusValues`. The ratchet baseline falls **60 → 43 across 11 files**, and its stale-entry test forced the allow-list update rather than letting the cleaned file linger. Safe without a build because the constants are `const string`: the compiler inlines them into the LINQ tree, so EF sees the same literal and **the generated SQL is byte-identical**. | `StatusLiteralRatchetTests` · commit below |
| ~~P1~~ | ~~**Every dashboard loaded its whole table to compute four numbers.**~~ **Resolved 2026-09-08** — all seven dashboards now read aggregates from `IDashboardMetricsService`. Migrating the last three turned up a second copy of the Master-DN rule: the metrics service counted notes that no list screen shows, in both its aggregates *and* the Overview activity feed. Both now filter, and a new ratchet fails if an unfiltered delivery query is added. | commit `52f69ee` · `MasterDnFilterParityTests.cs` |

### The fifteen-phase programme, and what it actually changed

Everything above is the residue of a systematic pass, not a backlog discovered
by accident. Fifteen phases were written from a full read of the code, then each
was applied as far as this environment safely allows — one commit per phase, in
[`docs/phases/`](docs/phases/README.md). **Phase 0 (CI) is skipped by
instruction.** Two phases are complete; the rest are partial, and in every case
the phase file records exactly which piece stopped and why.

| | Phase | Outcome |
|---|---|---|
| ✅ | 3 VAT validation · 12 one home per rule | **Complete** |
| ◐ | 1 transactions · 2 rounding · 4 licensing · 5 tax/currency · 6 UTC · 7 startup · 8 pagination · 9 security · 11 themes · 13 test depth · 14 observability · 15 release | **Partial** — each with a ratchet or a characterisation test holding the line |
| ▣ | 10 decomposition | **Mapped, not moved** — the premise was wrong and the real target is now identified |
| — | 0 CI | **Skipped by instruction** |

The blocking reason is worth stating precisely, because "blocked" is doing real
work here and it is not a euphemism for *difficult*. This workspace has no .NET
SDK and no network, so nothing written in it has ever been compiled, and four
specific classes of change cannot be made safely without one:

| class | why | phases |
|---|---|---|
| signature changes that ripple | a `const` is inlined into every call site, so changing one is not a local edit | 1, 5 |
| schema migrations | a migration can silently change a reported number through one missing `WHERE` | 1, 5, 14 |
| XAML layout edits | a control that cannot be seen cannot be checked | 8, 11 |
| file and method splits | "behaviour unchanged" depends on the compiler catching a lost `using` — and `TreatWarningsAsErrors` makes that a hard error | 10 |

**Three premises in the original analysis were wrong**, and each would have
produced confident, plausible, broken work. They are recorded here rather than
quietly fixed, because the pattern is more useful than the corrections:

- **Phase 12** was scoped to give proforma status a canonical home.
  `ProformaInvoiceStatus.cs` already existed with all six constants, an `All`
  list and a `Normalize`. The genuine duplicate was elsewhere — a low-stock
  threshold that read **2** to the AI agent and **5** on the dashboard, from the
  same column, so a sheet holding 3 or 4 was simultaneously low stock and fine.
- **Phase 10** was scoped to a "12,162-line `MainViewModel`". No such file
  exists: `MainViewModel` is 1,467 lines across three partials and the largest
  file here is 1,933.
- **Phase 8** was scoped as "20 call sites to paginate". Six of the eleven that
  remain are lookups, filter sources or search, where pagination is a regression
  rather than a fix.

The lesson the programme keeps producing: **a count is not a finding.** Each of
those three came from counting something — literals, lines, call sites — and
stopping there. In every case the authority (a transition table, a partial-class
split, what the loaded collection is actually *used for*) said something
different.

### One defect class, three screens, one afternoon

The single most useful thing found in the 2026-09-08 work was not a feature
gap. It was that three modules each declared a status or a threshold more than
once, in two spellings or two values, with no canonical home — and each
disagreement was silently wrong rather than loudly broken.

| | The split | What the user saw |
|---|---|---|
| Job Order | `"In Production"` vs `"InProduction"` | Overview under-counted jobs in production; the Job Order screen disagreed with it |
| Delivery Note | `"Draft"` vs `"Dispatching"` | `Preparing + Dispatched` did not equal `TotalNotes` |
| Target pricing | `0.005m` vs `0.05m` | One invoice read **ABOVE TARGET** on the quotation and **AT TARGET** in Job Profitability |
| VAT | `0.05m` / `1.05m` hardcoded in three money paths | Nothing wrong *today* — all three said 5%. It would have gone wrong the day the UAE rate changed |

The root cause was structural: `SavedJobOrder.Status` and
`SavedDeliveryNote.Status` are `string`, not the enums sitting next to them, so
nothing stopped a call site from inventing its own literal. Each module now has
one canonical home — `JobOrderStatusValues`, `DeliveryNoteStatusValues`,
`TargetMarginTolerance` — and a source-scan ratchet test that fails the build
if a second declaration appears.

`StatusLiteralRatchetTests` generalises the lesson. It does not know about
`"In Production"` or `"Dispatching"`; it flags the *shape* — a persisted status
compared against an inline literal — which is the shape all three bugs had. The
baseline was 60 such comparisons across 12 files, capped per file, so the number
can only fall.

**The first migration is done (2026-09-09).** `DeliveryNoteRepository.Part1.cs`
was the largest offender at 17 — all of them per-status filters in the list
query — and every one now reads from `DeliveryNoteStatusValues`. The baseline is
**43 across 11 files**, and the ratchet's own stale-entry test forced the
allow-list update rather than letting a cleaned-up file linger.

That this was safe is worth recording, because it is why the remaining 43 can be
migrated the same way: the canonical constants are `const string`, so the
compiler inlines them into the LINQ expression tree and EF sees the same literal
it saw before. **The generated SQL is byte-identical; only the source changed.**
The next targets, in order: `ReportingDataService.TaxInvoice.cs` (9),
`AiContextBuilder.cs` (5), then four files at 4.

Two things worth saying plainly:

- **The tolerance value is a business decision, not a technical one.** It was
  unified on `0.005m` because the Proforma side classifies first and persists
  the result. Margins between 0.005 and 0.05 now read ABOVE or BELOW where they
  previously read AT. If the rule should be ±0.05, change one constant.
- **Tax Invoice was checked and is *not* affected.** Nothing in the repository
  stores `"Partially Paid"`; only the PascalCase form is written. It gained a
  canonical home anyway, because the absence of one is what let the other three
  drift.

### The rating, stated plainly

If you want one number: **9.0/10 is the defensible figure**, because it came
from an audit rather than a phase sign-off. The missing point is not missing
features — it is missing *verification*.

That gap narrowed on 2026-09-09, when the tree was compiled for the first time
since the analysis work began. It came back with **nine errors from two root
causes**, both from code written blind, both now fixed. That is a better result
than it sounds — 225,914 lines with nine errors, no architectural surprises, and
not one of them a logic error in the money path — but it is also exactly why the
number is not higher: the errors existed because nothing had been compiled, and
the tree has not been proven clean since.

To move the number from judgement to evidence: get a green build and keep it
green with the CI files already sitting in `docs/ci/`; paginate the four list
views that genuinely need it; and deepen pricing-engine coverage, which is where
the money is computed and where 1 : 6.8 is still thin.

---

## Build & run

Requires **Windows 10/11** and the **.NET 8 SDK**. The project targets
`net8.0-windows` with `UseWPF`, so it will not build on Linux or macOS.

```powershell
dotnet restore
dotnet build
dotnet run --project ProGlassAutomation.csproj
```

The solution file is `ProGlassAutomation.slnx` (the newer XML format). Older
8.0.x SDK CLIs do not resolve it, so build the projects explicitly if you
hit that.

```powershell
# tests
dotnet test Tests --filter "FullyQualifiedName~CompanyUpdateSemanticsTests"
dotnet test Tests --filter "FullyQualifiedName~PriceBasisSignalTests"
dotnet test Tests --filter "FullyQualifiedName~InquiryBoqConversionTests"
dotnet test Tests --filter "FullyQualifiedName~SilentCatchAuditTests"

# publish
dotnet publish -c Release -r win-x64
```

User data lives at `%AppData%\ProGlassAutomation\`. Upgrades never touch it.

### Verifying without a build

Several checks run without the SDK and are safe to run on any machine:

```bash
python3 scripts/verify-readmes.py       # every doc link resolves, index fresh
python3 scripts/verify-dashboard.py     # XAML well-formedness, resource and
                                        # binding resolution, style discipline
python3 scripts/gen-docs-index.py --check

# balance-check the C# touched since a known-good commit
git diff --name-only BASE..HEAD | grep '\.cs$' \
  | xargs python3 scripts/check-csharp-balance.py
```

These are static. They catch broken references, malformed markup and truncated
blocks; they do **not** prove the application compiles or behaves.

> **Nothing since `b86a3bc` has been compiled.** There was no .NET SDK in the
> environment it was written in. **[`docs/BUILD_VERIFICATION_GUIDE.md`](docs/BUILD_VERIFICATION_GUIDE.md)**
> is the checklist for the first real build — the exact commands, where errors
> are most likely, and the on-screen numbers to sanity-check afterwards. Read
> its §1 before typing anything: the repo has only `ProGlassAutomation.slnx`
> and no `.sln`, so a bare `dotnet build` fails for a reason unrelated to the
> code.

`check-csharp-balance.py` is worth a word, because the obvious version of it is
wrong. Counting `{` and `}` characters also counts the ones inside string
literals, character literals, verbatim strings, interpolated strings and
comments — and this codebase has regex literals containing an escaped `\(`,
which makes the naive count disagree with itself. The script tracks whether it
is inside a string or a comment and reports the line where depth first goes
negative. It catches one class of mistake — a block truncated or duplicated by
scripted find-and-replace — and nothing else.

---

## Documentation

| Start here | |
|---|---|
| [`docs/WORKFLOW.md`](docs/WORKFLOW.md) | The end-to-end business chain with the real code path per handoff |
| [`docs/README.md`](docs/README.md) | Index of all 140 documents, generated from the tree |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Layering, boundaries, the data path |
| [`docs/DESIGN_SYSTEM.md`](docs/DESIGN_SYSTEM.md) | Design tokens, brushes, motion, wallpapers |
| [`docs/MONEY_PATH_ERROR_POLICY.md`](docs/MONEY_PATH_ERROR_POLICY.md) | What may and may not happen on a money path — read before touching pricing |
| [`docs/ERP_UPGRADE_ROADMAP.md`](docs/ERP_UPGRADE_ROADMAP.md) | Phase history and acceptance gates |
| [`docs/REMEDIATION_PLAN.md`](docs/REMEDIATION_PLAN.md) | Phased plan for every open issue above, with acceptance criteria |
| [`docs/RELEASE_GATE.md`](docs/RELEASE_GATE.md) | Release checklist |
| [`Tests/README.md`](Tests/README.md) | Test filters and gate conventions |

Every significant folder carries its own `README.md`; all 30 are linked from
the documentation index.

Documents whose filename carries a date are point-in-time audits — accurate
for that day, **not** a description of the current build. When a dated audit
and an undated document disagree, the undated one wins.

---

<div align="center">

### Build once. Preserve what works. Trace everything.

**Designed for a floor where one wrong sheet costs real money.**

<br/>

`WPF` · `.NET 8` · `C# 12` · `MVVM` · `SQLite + EF Core` · **v2.0.0**

<br/>

*Built by Juber Saleem*

</div>
