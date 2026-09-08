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

Numbers below were measured against this tree on 2026-09-08, not copied from
an earlier release note. The measurement is stated so you can reproduce it and
disagree with it.

| | |
|---|---|
| **Production code** | 1,109 C# files · 224,583 lines |
| **UI** | 229 XAML files · 114,658 lines |
| **Tests** | 171 files · **1,306 `[Fact]`/`[Theory]`** · 30,272 lines |
| **Test-to-production ratio** | 1 : 7.4 |
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
> figure. Both numbers above are production-only, and the honest ratio is
> 1 : 7.4, not 1 : 7.6.
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
├── Tests/                     xUnit — 1,306 tests
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
> `dotnet build` + `dotnet test`, because **this repository currently has no
> CI**. Treat both as engineering judgement, not as measured results.

### What is genuinely strong

| Area | Evidence |
|---|---|
| **Domain correctness** | One pricing engine, one status vocabulary per module, pinned cost basis. The money path is designed, not accumulated. |
| **Error discipline** | Zero empty catches and zero raw `ex.Message` to users in shipped code; `ExceptionMessages.Deep()` everywhere; correlation IDs end-to-end. |
| **Data safety** | Migrations run backup → transaction → rollback and are additive-only. Delete guards check downstream documents first. |
| **Security** | RSA-2048-PSS with no v1.5 fallback, DPAPI for secrets, PBKDF2 for passwords, machine fingerprinting, rate-limited key generation. |
| **Test intent** | 1,306 tests targeting the *workflow invariants* rather than getters — company semantics, conversion, price-basis determinism. |
| **Hygiene** | Zero TODO/FIXME/`NotImplementedException` in 223k lines of production code. Rare at this size. |

### What is open

Every item below has a phase, a concrete action and an acceptance test in
**[`docs/REMEDIATION_PLAN.md`](docs/REMEDIATION_PLAN.md)**.

| Priority | Issue | Evidence |
|---|---|---|
| **P0** | **No CI.** `.github/workflows/` does not exist. Four complete workflow files sit in `docs/ci/` and `docs/CI/`, unused. Nothing proves this tree builds. | `ls .github/` → `CODEOWNERS`, `dependabot.yml`, `pull_request_template.md` |
| **P1** | **No pagination.** 28 real `GetAllAsync()` call sites across 20 ViewModels, now locked by a ratchet test so the number cannot grow while the migration happens. Correct at 500 records, painful at 50,000. A working pattern already exists — `GlobalQueryEngine` + `InquiryList` (`PageSize=50`) — it is simply not used everywhere. | `grep -rn GetAllAsync ViewModels/ \| grep -v ///` |
| **P0** | **Nothing since `b86a3bc` has been compiled.** No SDK and no reachable NuGet feed in the environment the 2026-09-08 work was written in. Logic was simulated in Python and every assertion re-run, but that is *reviewed*, not *proven*. Build on Windows before shipping. | commits `041a231`…`HEAD` |
| **P2** | **Test ratio 1 : 7.4.** Still thin for software that computes money. The pricing engine in particular deserves property-based coverage. The earlier *1 : 7.6* figure was computed with test files counted as production — see the correction in [At a glance](#at-a-glance). | 30,272 test lines vs 224,583 |
| **P2** | **Large classes.** 13 files exceed 990 lines; `JobProfitabilityViewModel.cs` is 1,930. Partial-class splitting contains the file size but not the responsibility count. | `find . -name '*.cs' \| xargs wc -l` |
| **P2** | **No obfuscation.** Release binaries are readable in dnSpy. Acceptable for desktop, a raised bar for a commercial product. | `docs/CODE_RATING_REPORT.md` |
| **P3** | **One acknowledged hack, still open.** `OtherChargeRow.MetricQty` hardcodes delivery quantity to `1m` per invoice — a multi-trip delivery cannot be billed without a manual Custom Charge. Documented, deliberate, and marked *"proper fix planned for Chat 8"*; Chat 8 happened, the fix did not. **Step 1 of the fix is now in place**: characterisation tests pin current behaviour for every branch, so the replacement can be made against a net rather than against hope. | `OtherChargeRow.cs:320` · `OtherChargeRowMetricQtyCharacterisationTests.cs` |
| ~~P3~~ | ~~**25 `.md` files in the repository root.**~~ **Resolved 2026-09-08** — 24 audit/analysis documents moved to `docs/` with `git mv`, all inbound references rewritten, links re-verified. `README.md` is now the only root document. | `ls *.md` |
| **P3** | **No anti-debug checks.** A debugger can patch memory to bypass licensing. Acceptable for desktop ERP; not zero-risk. | `docs/CODE_RATING_REPORT.md` |
| ~~P1~~ | ~~**Status vocabularies disagreed between screens.**~~ **Resolved 2026-09-08** — see the section below. | commits `122a32b`, `fdfea80`, `5c10f90` |

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

The root cause was structural: `SavedJobOrder.Status` and
`SavedDeliveryNote.Status` are `string`, not the enums sitting next to them, so
nothing stopped a call site from inventing its own literal. Each module now has
one canonical home — `JobOrderStatusValues`, `DeliveryNoteStatusValues`,
`TargetMarginTolerance` — and a source-scan ratchet test that fails the build
if a second declaration appears.

`StatusLiteralRatchetTests` generalises the lesson. It does not know about
`"In Production"` or `"Dispatching"`; it flags the *shape* — a persisted status
compared against an inline literal — which is the shape all three bugs had. The
baseline is 60 such comparisons across 12 files, capped per file, so the number
can only fall. The largest offender
(`DeliveryNoteRepository.Part1.cs`, 17) is the natural first migration.

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
features — it is missing *verification*. Get CI green, paginate the list
views, and deepen pricing-engine coverage, and the higher number becomes
something you can evidence rather than assert.

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
