<div align="center">

<img src="README_ASSETS/hero.png" alt="ProGlass Automation" width="900"/>

# ProGlass Automation

### Industrial glass-fabrication ERP — from first inquiry to final statement

**WPF · .NET 8 · C# 12 · MVVM · SQLite + EF Core · 15 languages**

<br>

![Version](https://img.shields.io/badge/version-2.0.11-0E6A8A?style=for-the-badge)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/Platform-WPF-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![C%23](https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp&logoColor=white)
![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)
![Tests](https://img.shields.io/badge/tests-2%2C369-2E7D32?style=for-the-badge)
![Languages](https://img.shields.io/badge/languages-15-C2185B?style=for-the-badge)
![License](https://img.shields.io/badge/license-Proprietary-lightgrey?style=for-the-badge)

<br/>

`Inquiry ➜ Quote ➜ Confirm ➜ Produce ➜ Deliver ➜ Invoice ➜ Settle`

**Every step carries the last one forward. Nothing is re-keyed.**

[🧭 Application Workflow](docs/WORKFLOW.md) ·
[🏗️ Build, Package & Release](#build-run-package-and-release) ·
[📚 All Documents](docs/README.md) ·
[🗺️ Upgrade Phases](docs/UPGRADE_PHASES_2026-10-05.md) ·
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

Every number below was **measured against this tree on 2026-10-05**
(commit `694c4247`) with the commands shown under the table. They are not
carried forward from an earlier release note, and the earlier note was
wrong — see the correction box.

| | |
|---|---|
| **Production code** | 1,391 C# files · 299,696 lines |
| **UI** | 346 XAML files · 143,570 lines |
| **Tests** | 288 files · **2,369 `[Fact]`/`[Theory]`** · 56,463 lines |
| **Test-to-production ratio** | 1 : 5.3 (by lines) |
| **Modules** | 27 view areas · 43 service domains |
| **Data** | 15 SQLite `DbContext`s, additive-only migrations |
| **Authorisation** | 8 roles (`AppRoles.All`) + 1 legacy · 34 granular `Module.Capability` permissions |
| **Localisation** | 15 locales as pure XAML dictionaries — adding one needs no C# change |
| **Buildable projects** | 6 (app, licensing library, licensing tests, app tests, license generator, engine regression) |
| **Debt markers in shipped code** | **0** TODO · **0** FIXME · **0** `NotImplementedException` · 1 legacy `HACK` banner (documented, `Models/ProformaInvoice/Step6OtherCharges/OtherChargeRow.cs:309`) |
| **Empty `catch { }` blocks** | 101 in audited folders, **55 of them with no allow-list entry** — the audit test that polices this is currently red. See [Engineering health](#engineering-health--an-honest-assessment). |

> **Correction, 2026-10-05.** The previous revision of this table reported
> *1,112 production files / 225,915 lines*, *1,396 tests*, *12 databases*,
> *38 service domains*, *24 view areas*, *29 permissions* and *140
> documents*, and a badge claiming *1,148 tests* that disagreed with the
> table beside it. None of those matched the tree. The table above is
> reproducible with the commands below; the old one was not. It also
> claimed **"0 empty `catch`"**, which is false today: the audit finds 101,
> and 55 are not covered by `SilentCatchAuditTests.AllowedSilentCatches`.
> That claim is now stated as measured, not as a badge.

**How to reproduce these** (bash, repo root):

```bash
# production C# (everything outside Tests/, obj/, bin/, .claude/)
find . -name '*.cs' -not -path './.git/*' -not -path '*/obj/*' -not -path '*/bin/*' \
       -not -path './Tests/*' -not -path './.claude/*' | wc -l

# live [Fact]/[Theory] attributes (commented-out ones do not count)
grep -rho --include='*.cs' -E '^\s*\[(Fact|Theory)' Tests | wc -l

# XAML, DbContexts, service domains, view areas, locales
find . -name '*.xaml' -not -path './.git/*' -not -path './.claude/*' -not -path './Tests/*' | wc -l
grep -rl ": DbContext" --include='*.cs' Services | wc -l
ls -d Services/*/ | wc -l ; ls -d Views/*/ | wc -l ; ls Themes/Languages | wc -l
```

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
| 🏠 | **Dashboard** | Seven module dashboards on one shared grammar ([`Styles/DashboardStyles.xaml`](Styles/DashboardStyles.xaml)) + an order-to-cash pipeline rail on Overview: Quote → Production → Delivery → Invoice → Payment. Every page renders all four states — loading, error, empty, data. |
| 📨 | **Inquiries** | Pre-sales register with scope-of-supply (BOQ), attachments with SHA-256 dedup, lifecycle log. |
| 🧾 | **Proforma Invoice** | The 9-step wizard above. |
| 🏭 | **Job Order** | Factory workload, priority, production status, material reservation and cutting completion. |
| 🚚 | **Delivery Note** | Multi-movement dispatch → signed → received, with pending-piece tracking. |
| 🧮 | **Tax Invoice** | VAT-compliant conversion from PI; `Issued` → `Paid` / `Overdue`; version history. |
| 📄 | **Statement** | Customer balances grouped from invoices. |
| 📦 | **Sheet Store** | Stock, buy/sell prices, suppliers, purchase/use history. Every write stamps a price-version signal. |
| 🏺 | **Specifications** | Reusable spec library, live in Step 5. |
| 📈 | **Job Profitability** | Reads the approved-PI snapshot; flags `⚠` when sheet-store prices moved. |
| 📊 | **Reports** | Data-driven reporting on persisted figures, plus the Statement and Order report generators (4-step wizards). |
| 🏢 | **Company Center** | Legal-entity profiles with field-diff audit on every change set. |
| 👥 | **System Center** | Users, roles, operation-based permissions. |
| 🧰 | **Logs & Activity** | App log, activity log, audit DB with GitHub-style diffs, crash reports with error IDs. |
| ⚙️ | **Settings** | Appearance, themes, module wallpapers, feature toggles, backup/restore, updates, licensing, shared LAN data root. |
| 🤖 | **AI Assistant** | Cloud providers plus an always-offline local brain — no key and no network still answers from the knowledge base and live data. |
| ✂️ | **Optimization** | 2D cutting-layout engine (guillotine packer, pattern search, capacity strips) with its own portable regression suite. |
| 🎬 | **Demo Center** | Scripted walkthroughs with screen recording, TTS narration and FFmpeg-composed MP4 export. |

---

## Architecture

```
ProGlassAutomationADV/
├── Views/ + ViewModels/       MVVM; one folder per business module (27 areas)
├── Services/                  43 domains — repositories, workflows, export,
│   ├── ProformaInvoice/       audit, licensing, theme, AI, dashboard metrics
│   ├── Audit/                 OperationGuard + AppRoles + AppPermissions
│   ├── Dashboard/Core/        aggregate DTOs; ViewModels never see EF entities
│   ├── Production/            material allocation, pairing, cutting, control
│   └── Database/              15 DbContexts, additive-only migrations
├── Agent/                     AI orchestrator, local LLM, demo director
├── Controls/ Themes/ Styles/  design system, chart controls, themes, 15 locales
├── Models/                    domain models, one folder per module
├── Licensing/                 ProGlass.Licensing — the license contract library
├── Tools/                     LicenseGenerator (vendor), EngineRegression (tests)
├── Tests/                     xUnit — 2,369 [Fact]/[Theory]
├── docs/                      308 documents, indexed at docs/README.md
├── scripts/                   verification, generation and packaging tooling
└── installer/                 Velopack manifest + MSI wizard content
```

**Platform properties**

- 🔐 **Authorisation at the service boundary** (`OperationGuard`). Hiding a
  button is not a permission check; the repository enforces the rule.
- 📝 **Audit on every meaningful write** — before/after diffs with session,
  actor and module.
- 💾 **SQLite, WAL, busy-timeouts**, migrations wrapped
  backup → transaction → rollback. Never destructive. A shared LAN/Wi-Fi
  data root is supported, and a share that is unreachable at startup
  degrades to local **as visible state** (`SharedRootFallbackActive`),
  never silently.
- 🔄 **OTA updates** from GitHub Releases via Velopack (installed builds)
  with a GitHub-Release fallback for portable builds; `<Version>` in the
  csproj is the single version source.
- 🧩 **Partial-class decomposition** for large ViewModels — the split is
  deliberate, not accidental.
- 🧪 **Workflow regression net** pins company semantics, inquiry→PI
  conversion, price-basis determinism, status-vocabulary funnels and the
  optimisation engine's cut patterns.

---

## The honesty contract

A professional ERP must never silently invent money. These are enforced
rules, not aspirations.

| Rule | Enforced in |
|---|---|
| Reporting **reads** money from the persisted Step-9 snapshot; it never recomputes it | `JobProfitabilityViewModel`, `ReportingDataService` |
| A stale price basis is **flagged `⚠`**, never silently rescored | `GlassPriceStamper` |
| Statuses enter through one normalising home. **Unknown statuses pass through — they are never remapped to something plausible** | `ProformaInvoiceStatus`, `InquiryStatuses`, `JobOrderStatusValues`, `DeliveryNoteStatusValues` |
| Every money-path `catch` is classed A–D and documented | [`docs/MONEY_PATH_ERROR_POLICY.md`](docs/MONEY_PATH_ERROR_POLICY.md) |
| Company profile: preserve-blank on invoice save, field-diff trail on every change | `MasterDataRepository` |
| Silent catches are policed by a test, not by discipline | `Tests/Security/SilentCatchAuditTests.cs` — **currently red**, see below |

---

## Build, run, package and release

This section is the complete operator guide: what to install, what to type,
what comes out, and what to do when it does not work. It covers the app, the
test suites, the license generator, and the five publish shapes including
the single-EXE build and the Velopack installer.

> **Platform.** The app targets `net8.0-windows` with `UseWPF`. Build,
> test, publish and run all require **Windows 10 (1809+) or Windows 11,
> x64**. Linux and macOS can run the *static* checks at the end of this
> section and nothing else.

### 1. Toolchain

| Need | Why | Check |
|---|---|---|
| Windows 10/11 x64 | WPF + `net8.0-windows` | — |
| **.NET 8 SDK** (8.0.x) | target framework for every project | `dotnet --list-sdks` |
| PowerShell 5.1 **or** 7+ | installer pipeline (`*.ps1`) | `powershell -Command $PSVersionTable` / `pwsh -v` |
| **Velopack CLI** (`vpk`) | `Setup.exe` / MSI / delta packages | `vpk --version` |
| Python 3.10+ | static repo checks and the i18n generator (optional) | `python3 --version` |
| Visual Studio 2022 **17.13+** | *only* if you want to open the solution (`.slnx`) | — |

```powershell
# one-time: Velopack CLI, pinned to the library version in the csproj (1.2.0)
dotnet tool install --global vpk --version 1.2.0
vpk --version
```

`dotnet tool install -g vpk` without `--version` also works, but the app
references `Velopack 1.2.0` (`ProGlassAutomation.csproj:171`) and a CLI from
a different major line writes a release feed the client may not read. Pin it.

> **The SDK is pinned.** [`global.json`](global.json) requires `8.0.100` with
> `"rollForward": "latestFeature"`, so any installed 8.0.x SDK satisfies it but
> a 9.x/10.x SDK is refused. That is deliberate: with
> `<AnalysisLevel>latest</AnalysisLevel>` and
> `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, a newer SDK can turn a
> brand-new analyzer warning into a hard build error on code nobody touched.
> If you *want* to build on a newer SDK, relax the pin (`"rollForward":
> "latestMajor"`) locally and expect analyzer fallout — do not commit it.
>
> One consequence: form B below (`dotnet build ProGlassAutomation.slnx` on a
> 9.x SDK) no longer works while the pin is in place. Use form A — name the
> project — which is the documented primary path anyway.

### 2. What is buildable in this repo

Six projects. `ProGlassAutomation.slnx` lists five of them.

| Project | Path | What it is |
|---|---|---|
| **ProGlassAutomation** | `ProGlassAutomation.csproj` | The WPF application. Lives at the repo **root**, so its csproj explicitly removes `Tests/**`, `Licensing/**`, `Tools/**` and `PRODUCTION_READINESS/**` from the SDK's default globbing. |
| **ProGlass.Licensing** | `Licensing/ProGlass.Licensing.csproj` | `net8.0` library. The single source of truth for the license format, canonical JSON, RSA-PSS sign/verify and validation. No WPF, no EF, no app services. |
| **ProGlass.Licensing.Tests** | `Licensing/ProGlass.Licensing.Tests/ProGlass.Licensing.Tests.csproj` | xUnit suite for the contract: tamper, plan catalogue, offline-activation protocol. |
| **ProGlassAutomation.Tests** | `Tests/ProGlassAutomation.Tests.csproj` | The main xUnit suite (2,369 `[Fact]`/`[Theory]`), including the source-scanning ratchet tests. |
| **ProGlassAutomation.LicenseGenerator** | `Tools/LicenseGenerator/ProGlassAutomation.LicenseGenerator.csproj` | Vendor-only WPF tool that signs license keys. **Never shipped to customers.** |
| **EngineRegression** | `Tools/EngineRegression/EngineRegression.csproj` | `net8.0` xUnit project that links the optimisation engine sources directly, with no WPF and no EF, so the packer can be tested on any OS. **Not in the `.slnx`** — build it by path. |

Dependency shape, and why it matters:

```
ProGlass.Licensing  (contract: format, canonical JSON, RSA-PSS, validation)
        ├──▶ ProGlassAutomation            (PUBLIC key — verifies)
        └──▶ ProGlassAutomation.LicenseGenerator  (PRIVATE key — signs)
```

The app and the generator never reference each other. The private signing
key must never reach the app project — that separation is enforced by the
`<Compile Remove="Tools/**" />` block in `ProGlassAutomation.csproj`.

### 3. The `.slnx` trap — read before typing anything

The repo has `ProGlassAutomation.slnx` and **no `.sln`**. `.slnx` is the newer
XML solution format and older 8.0.x SDK CLIs cannot resolve it, so:

```powershell
dotnet build        # ✗ fails: cannot resolve ProGlassAutomation.slnx
```

The failure message looks like a missing project file and has nothing to do
with the code. Two ways out:

```powershell
# A — name the project explicitly (what CI does; always works)
dotnet build ProGlassAutomation.csproj -c Release

# B — install the .NET 9 SDK alongside 8; it understands .slnx and can
#     still build net8.0-windows targets
dotnet build ProGlassAutomation.slnx -c Release
```

Everything below uses form A.

### 4. Everyday: restore, build, run

```powershell
# restore (slowest step the first time; needs internet for NuGet)
dotnet restore ProGlassAutomation.csproj --verbosity minimal

# build the app — Release is what CI and the installer use.
# TreatWarningsAsErrors is TRUE here, so a new warning fails the build.
dotnet build ProGlassAutomation.csproj -c Release --no-restore --verbosity minimal

# run it (Debug; the login window is the first thing you see)
dotnet run --project ProGlassAutomation.csproj

# or run the built binary directly
.\bin\Debug\net8.0-windows\ProGlassAutomation.exe
```

Build the **test project** rather than the app when you want both, because
the test project has a `ProjectReference` on the app and MSBuild will
compile the app first:

```powershell
dotnet build Tests/ProGlassAutomation.Tests.csproj -c Release --verbosity minimal
```

The reverse does **not** work: `dotnet build ProGlassAutomation.csproj`
never compiles the test project (its csproj excludes `Tests/**`), so a
following `dotnet test --no-build` fails with *"test source file not found"*.
This exact mistake is documented in `scripts/build-installer.ps1`, and it used
to live in a CI template as well; `.github/workflows/ci.yml` builds the test
project for precisely this reason.

### 5. Tests

```powershell
# the whole app suite (the release gate)
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release --no-build --verbosity normal

# the licensing contract suite
dotnet test Licensing/ProGlass.Licensing.Tests/ProGlass.Licensing.Tests.csproj -c Release

# the optimisation engine, with no WPF and no EF in the process
dotnet test Tools/EngineRegression/EngineRegression.csproj -c Release

# targeted regression filters (all four class names verified present)
dotnet test Tests --filter "FullyQualifiedName~CompanyUpdateSemanticsTests"
dotnet test Tests --filter "FullyQualifiedName~PriceBasisSignalTests"
dotnet test Tests --filter "FullyQualifiedName~InquiryBoqConversionTests"
dotnet test Tests --filter "FullyQualifiedName~SilentCatchAuditTests"

# ratchets — these fail when a guard-rail regresses, and the message
# names the offending file and the canonical home to use
dotnet test Tests --filter "FullyQualifiedName~MoneyRoundingRatchetTests"
dotnet test Tests --filter "FullyQualifiedName~StatusLiteralRatchetTests"
dotnet test Tests --filter "FullyQualifiedName~PaginationRatchetTests"

# machine-readable results + coverage
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release --no-build `
  --logger "trx;LogFileName=results.trx" --results-directory ./TestResults `
  --collect:"XPlat Code Coverage"
```

`dotnet test Tests` works because `Tests/` contains exactly one project.
`TreatWarningsAsErrors` is **false** for both test projects, so a warning
there will not block you.

> **Two tests are red on the current tree** — established by re-deriving each
> test's own scanning algorithm over this tree, not by running `dotnet test`.
> They are red for reasons worth knowing before you run anything:
> `SilentCatchAuditTests` (55 empty catches with no allow-list entry) and
> `MoneyRoundingRatchetTests.Only_the_money_home_may_declare_the_rounding_mode`
> (2 files allowed, 9 present). Both are baseline drift, not new defects —
> code landed after the baselines were pinned. Phases 18 and 19 of the
> [upgrade plan](docs/UPGRADE_PHASES_2026-10-05.md) close them without
> touching behaviour.

### 6. Publish — five shapes, pick one

All publish commands run from the repo root on Windows. `-r win-x64` is
required for every self-contained shape.

**6a. Framework-dependent** — smallest output, needs the .NET 8 Desktop
Runtime installed on the target machine.

```powershell
dotnet publish ProGlassAutomation.csproj -c Release -r win-x64 --self-contained false -o publish-fd
```

**6b. Self-contained folder** — the shape that actually ships. Bundles the
desktop runtime; the customer installs nothing. This is what
`Properties/PublishProfiles/Release.pubxml` produces.

```powershell
dotnet publish ProGlassAutomation.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishProfile=Release -o publish
```

The profile also sets `PublishReadyToRun=true` (+ composite),
`PublishSingleFile=false`, `DebugType=embedded`, deterministic build on, and
`PublishDir=publish\`. Do not rename that folder: both
`scripts/build-installer.ps1` and the release workflow read it by name.

**6c. Single-file EXE** — see §7.

**6d. Portable ZIP** — same binaries as 6b, zip-wrapped, for sysadmins who
block `Setup.exe`. In portable mode the Velopack updater is inactive and
the app falls back to the GitHub-Release check.

```powershell
Compress-Archive -Path .\publish\* -DestinationPath ProGlassAutomation-Portable.zip -CompressionLevel Optimal
```

**6e. Velopack installer** — `Setup.exe` + MSI + full/delta `.nupkg` + the
`RELEASES` feed the in-app updater reads. See §8.

> **Trimming is not available.** `<PublishTrimmed>` must stay `false`:
> the .NET trimmer does not support WPF. `Release.pubxml` already sets it
> false; do not "optimise" it.

### 7. The single-EXE build

One self-contained `ProGlassAutomation.exe` with the runtime and managed
assemblies bundled inside:

```powershell
dotnet publish ProGlassAutomation.csproj -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:PublishReadyToRun=true `
  -p:DebugType=embedded `
  -o publish-single
```

`IncludeNativeLibrariesForSelfExtract=true` is not optional here. This app
carries native payloads — `Whisper.net.Runtime` (GGML inference),
`LLamaSharp.Backend.Cpu`, `SkiaSharp`, and `System.Speech` — and without it
those assemblies are left beside the exe or fail to load at runtime with a
`DllNotFoundException`.

**Three things to know before you ship a single-file build:**

1. **It cannot go through the OTA pipeline.** Velopack patches individual
   assemblies to build deltas; `Release.pubxml` deliberately sets
   `PublishSingleFile=false` for exactly this reason. Single-file is for
   hand distribution (thumb drive, e-mail, kiosk), not for the update feed.
2. **It is slower to start** the first time: the bundle extracts native
   assets to a cache folder on launch.
3. **It is unverified in this environment.** The commands above were not
   executed on a Windows machine (see the verification note at the end of
   this section). Run §9's smoke test after the first build.

### 8. The installer pipeline

`scripts/build-installer.ps1` is the local mirror of the CI release job.
It restores, builds Release, runs the test gate, publishes self-contained
win-x64, bundles FFmpeg, paints the installer art, runs `vpk pack`, zips the
portable build, and prints the SHA-256 of every artefact.

```powershell
# from the repo root
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Version 2.0.11
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -SkipTests
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Clean -SkipFfmpeg
```

| Switch | Effect |
|---|---|
| `-Version <x.y.z>` | Package version. **Omit it** — the script now reads `<Version>` from `ProGlassAutomation.csproj`, so the package always matches the assemblies. Pass it only to build a hotfix under a different version than the source says (it also fails the run if the value is not `MAJOR.MINOR.PATCH`). |
| `-PackId` / `-PackTitle` / `-PackAuthors` | Velopack pack identity. |
| `-SkipTests` | Skips the test gate. The script itself warns: *do not publish this build to customers.* |
| `-SkipFfmpeg` | Omits `ffmpeg.exe`/`ffprobe.exe` from the payload; Settings can still download them later. |
| `-Clean` | Deletes `publish\` and `releases\` first. |

Exit codes: `1` toolchain missing · `2` build or test gate failed ·
`3` publish failed · `4` `vpk pack` failed.

Artefacts land in `releases\`:

| File | Purpose |
|---|---|
| `Setup.exe` | One-click Velopack installer (splash, per-user or per-machine, then launch). |
| `ProGlassAutomation-<ver>-full.nupkg` | Full payload; future deltas diff against it. |
| `ProGlassAutomation-<ver>-delta.nupkg` | Binary delta — only when a previous release exists in `releases\`. |
| `ProGlassAutomation-Portable-<ver>.zip` | No-installer build. |
| `RELEASES` | The feed the in-app updater reads. |

Supporting pieces, all wired up by the script:

| File | Role |
|---|---|
| `installer/velopack.json` | Durable pack metadata (id, title, authors, copyright, icon, rid, framework, channel `stable`). The script passes these as explicit flags because `vpk` 1.x does not accept `--config`. |
| `installer/content/` | Welcome / readme / licence / conclusion text for the MSI wizard. |
| `scripts/generate-installer-art.ps1` | Paints `installer/generated/splash.png` + the two MSI bitmaps. Requires Windows (`System.Drawing`). Generated, never committed. |
| `scripts/bundle-ffmpeg.ps1` | Downloads the Gyan FFmpeg essentials build into `publish\tools\`. |
| `Properties/PublishProfiles/Release.pubxml` | The publish profile shared by the script and CI. |

> **Path correction.** `docs/INSTALLER.md` and the `Release.pubxml` header
> both refer to `tools\build-installer.ps1`. The script lives at
> **`scripts\build-installer.ps1`**; there is no `tools\build-installer.ps1`
> in the tree.

**Publishing a release.** Draft the GitHub Release, then either attach the
contents of `releases\` by hand, or let the Velopack CLI publish the feed:

```powershell
vpk upload github --repoUrl https://github.com/JUBERSALEEM/ProGlassAutomationADV `
  --channel stable --outputDir releases --publish --pre false --tag v2.0.11
```

`--channel stable` is not optional. `installer/velopack.json` and the pack
step both use `stable`, and a feed packed into a different channel is
invisible to the updater.

### 9. Version discipline and OTA

There is **one** authoritative version: `<Version>` in
`ProGlassAutomation.csproj` (currently `2.0.11`, with `AssemblyVersion` and
`FileVersion` beside it). `AppInfo.Version` reads it from the assembly
identity at runtime, and About / Settings / the update checker / crash logs
all read `AppInfo`.

Bump the **patch digit on every behavioural fix**. `UpdateService.IsNewer`
is a strict greater-than compare, so two builds that share a version number
are indistinguishable to OTA — a machine on the older one is never offered
the newer one, and no log excerpt can say which build produced it. The
csproj carries this warning in a comment block at line 27; it was learned
the hard way when `<Version>` sat at `2.0.1` for nine days across several
behaviour-changing commits.

Four copies of this number existed. Phase 20 (committed) removed two of them
and redefined the third:

| Place | Value | Effect |
|---|---|---|
| `ProGlassAutomation.csproj:41` | **2.0.11** | authoritative |
| `scripts/build-installer.ps1` | *reads the csproj* | **fixed** — `-Version` now defaults to empty and is resolved from `<Version>`; the run fails if the csproj or the element is missing |
| `installer/velopack.json:5-6` | `2.0.11` / `2.0.11.0` | **fixed** — the pack step overrides both on the command line, so these values are documentation and no longer contradict the payload |
| `Services/Updates/AppInfo.cs` | `FallbackVersion = "2.0.4"` | **kept on purpose** — it is a *floor*, not a mirror. Used only when the entry-assembly identity cannot be read, which means an unreadable build reports an older version than it is: `UpdateService.IsNewer` is a strict greater-than compare, so that can offer an update the machine already has, but can never offer a downgrade or hide a newer release. The Velopack path never reads it. Its comment now says so and warns that raising it *above* the current release would suppress real updates. |

The durable fix — injecting the csproj value at build time so the constant
disappears — is still open; see Phase 20 of the
[upgrade plan](docs/UPGRADE_PHASES_2026-10-05.md).

**Two update paths, one behaviour.** Installed (Velopack) builds use
`VelopackUpdateService` → `UpdateManager(GithubSource(...))` against
`https://github.com/JUBERSALEEM/ProGlassAutomationADV`, and
`ApplyUpdatesAndRestart` swaps the app under itself. Portable and dev builds
use the classic GitHub-Releases check in `UpdateService` plus a manual
installer. Neither path can ever brick the running app: a failed check is
logged and ignored.

Unsigned releases are refused by the updater's Authenticode check
(`UpdateSignatureVerifier`). Signing must happen **during** `vpk pack` via
`--signParams`, not afterwards with `signtool` — a post-pack pass misses
`Setup.exe`/`Update.exe` and invalidates the feed checksums.

### 10. The license generator (vendor tool)

```powershell
dotnet build Tools/LicenseGenerator/ProGlassAutomation.LicenseGenerator.csproj -c Release
dotnet run  --project Tools/LicenseGenerator/ProGlassAutomation.LicenseGenerator.csproj

# single-EXE vendor build
dotnet publish Tools/LicenseGenerator/ProGlassAutomation.LicenseGenerator.csproj `
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true `
  -o publish-licensegen
```

Flow: pick a plan from `LicensePlanCatalog` (7 days → 2 years), press
GENERATE, hand the customer the armored key. The generator rate-limits to
10 keys/hour/machine and writes an audit record per generation
(`Tools/LicenseGenerator/KeyProtection.cs`).

> ⚠️ **The production signing key is currently committed in plaintext** at
> `Tools/LicenseGenerator/ProductionSigningKey.cs:52` (`PrivateKeyXml`), and
> its public half is embedded in the customer app — so anyone with
> repository or binary access can forge keys the app accepts.
> `Tools/LicenseGenerator/README.md` states the opposite ("no longer embeds
> a production private key in source"), and the DPAPI wrapper keeps the
> plaintext constant as a bootstrap fallback, which makes it advisory
> rather than protective. This is **Phase 18, the highest-priority item in
> the [upgrade plan](docs/UPGRADE_PHASES_2026-10-05.md)**: rotate the key
> pair, move the private half out of Git into a `.pgkey` file, and keep the
> old public half in the trust ring so issued keys stay valid. Until it is
> done, treat this repository as containing a live signing secret.

### 11. Static checks that need no SDK

Safe on any OS. They catch broken references, malformed markup and
truncated blocks; they do **not** prove the application compiles.

```bash
python3 scripts/verify-readmes.py       # every doc link resolves, index fresh, nav present
python3 scripts/gen-docs-index.py --check  # docs/README.md matches the docs/ tree
python3 scripts/verify-dashboard.py     # XAML well-formedness, resource + binding resolution
python3 scripts/xaml-resource-lint.py   # resource keys resolve
python3 scripts/binding-lint.py         # binding paths exist on the target type

# balance-check the C# touched since a known-good commit
git diff --name-only BASE..HEAD | grep '\.cs$' \
  | xargs python3 scripts/check-csharp-balance.py
```

`check-csharp-balance.py` counts `{`/`}` while tracking whether it is inside
a string, char, verbatim or interpolated literal or a comment — the naive
count disagrees with itself in this codebase, which has regex literals
containing an escaped `\(`.

### 12. When it does not build

| Symptom | Cause | Fix |
|---|---|---|
| *"project file does not exist"* on a bare `dotnet build` | `.slnx` is not resolved by older 8.0.x SDKs | Name the project explicitly (§3) |
| *"test source file not found"* after `dotnet test --no-build` | You built `ProGlassAutomation.csproj`, which excludes `Tests/**` | Build `Tests/ProGlassAutomation.Tests.csproj` first (§4) |
| `CS0017` (multiple entry points) | A `Main` was declared in `App.xaml.cs` | Remove it — `App.xaml` stays `ApplicationDefinition` and generates `Main` |
| `CS1061 InitializeComponent` | `App.xaml` was reclassified as `Page` | Restore `ApplicationDefinition` |
| `CS0246` on xUnit types in the app | `Tests/**` or `Licensing/**` crept back into the app's globbing | Check the `<Compile Remove>` block in the csproj |
| `CS5001` / `CS0234` about `Program` | `Tools/**` was compiled into the app | Same block — the generator is a separate executable |
| A `CAxxxx` analyzer error you did not write | `<AnalysisLevel>latest</AnalysisLevel>` + `TreatWarningsAsErrors` on a newer SDK | `global.json` pins 8.0.x, so this means the pin was relaxed — restore it, or add the code to `WarningsNotAsErrors` with a reason |
| `A compatible .NET SDK was not found` on startup | `global.json` pins `8.0.100` / `latestFeature` and only a 9.x+ SDK is installed | Install a .NET 8 SDK (recommended), or relax the pin locally without committing it |
| `DllNotFoundException` for GGML/llama at runtime | Native assets not extracted in a single-file publish | Add `-p:IncludeNativeLibrariesForSelfExtract=true` (§7) |
| `RG1000 same key already added` | Duplicate `<Resource>` entries for the same file | One entry per file; see the comment block in the csproj |
| `vpk` not on PATH | Velopack CLI not installed | `dotnet tool install -g vpk --version 1.2.0`, then reopen the shell |

Capture failures as text, not screenshots:

```powershell
dotnet build ProGlassAutomation.csproj -c Release --no-restore --verbosity normal > build.log 2>&1
Select-String -Path build.log -Pattern "error [A-Z]+[0-9]+" |
  ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique > errors.txt
```

[`docs/BUILD_VERIFICATION_GUIDE.md`](docs/BUILD_VERIFICATION_GUIDE.md) has
the longer walkthrough and the tiered list of where a first build is most
likely to fail.

### 13. Running it: data, portability, uninstall

| | |
|---|---|
| **Installed data** | `%AppData%\ProGlassAutomation\` — one SQLite file per module, `config.json`, `logs\`, `Backups\`, per-module attachment folders. Full layout in [`docs/INSTALL.md`](docs/INSTALL.md). |
| **Shared LAN/Wi-Fi root** | Configured in Settings → Data. If the share is unreachable at startup the app falls back to local **and says so** (`SharedRootFallbackActive`, red banner) instead of silently splitting the dataset. |
| **Demo mode** | A separate `ProGlassAutomation.Demo` data folder, so a demonstration can never touch production records. |
| **Upgrades** | Never touch the data folder. Migrations are additive-only and run backup → transaction → rollback. |
| **Uninstall** | Velopack removes the app; `%AppData%\ProGlassAutomation\` is left alone on purpose. Delete it to remove all data. |

> **Verification note for this section.** The commands above were written
> against this tree on 2026-10-05 and cross-checked against
> `scripts/build-installer.ps1`, `Properties/PublishProfiles/Release.pubxml`,
> `installer/velopack.json` and the workflows in `.github/workflows/`. They
> were **not executed**: the environment had no .NET SDK,
> no PowerShell and no network access to NuGet (`curl https://api.nuget.org`
> → HTTP 000). Every file path, flag and default quoted here was read from
> the file it came from. The publish matrix in §6c/§7 in particular needs a
> first run on Windows before it is trusted.

---

## Engineering health — an honest assessment

> Two self-assessments exist in this repository and they disagree:
> `docs/ERP_UPGRADE_ROADMAP.md` claims **9.7/10** while
> `docs/CODE_RATING_REPORT.md` (2026-09-02) rates **9.0/10** after an
> independent audit. Neither was produced by a green `dotnet build` +
> `dotnet test`, because when they were written this repository had no CI.
> CI is written and verified ([`docs/ci/activate/`](docs/ci/activate/README.md),
> 2026-10-05) and needs one command from a machine with the `workflows`
> permission to go live, so the next assessment can be a measurement instead
> of a judgement. Until a run is green, treat both numbers as engineering
> judgement: the defensible figure remains **9.0/10**, and the missing point
> is not missing features — it is missing *verification*.

### What is genuinely strong

| Area | Evidence |
|---|---|
| **Domain correctness** | One pricing engine, one status vocabulary per module (`JobOrderStatusValues`, `DeliveryNoteStatusValues`, `TargetMarginTolerance`), pinned cost basis. The money path is designed, not accumulated. |
| **Error discipline** | Correlation IDs end-to-end; `ExceptionMessages.Deep()` instead of raw `ex.Message` to users; four last-chance exception sinks; crash reports with error IDs. |
| **Data safety** | Migrations run backup → transaction → rollback and are additive-only. Delete guards check downstream documents first. 12 `BeginTransaction*` sites, 10 of them with an explicit isolation level, all in `Services/Production` and the schema runner. |
| **Security** | RSA-2048-PSS, DPAPI for secrets, PBKDF2 for passwords, machine fingerprinting, rate-limited key generation. |
| **Test intent** | 2,369 `[Fact]`/`[Theory]` aimed at *workflow invariants* — company semantics, conversion, price-basis determinism, cut-pattern stability — not at getters. |
| **Hygiene** | Zero TODO/FIXME/`NotImplementedException` in 299,696 lines of production code. Rare at this size. |

### What is open

Measured on 2026-10-05, each with a phase, a concrete action and an
acceptance test in **[`docs/UPGRADE_PHASES_2026-10-05.md`](docs/UPGRADE_PHASES_2026-10-05.md)**.

| # | Issue | Evidence | Status |
|---|---|---|---|
| **18** | **The license-signing private key is in Git, in plaintext.** Anyone with repo or binary access can forge keys the app accepts, and the tool's own README says this is not the case. | `Tools/LicenseGenerator/ProductionSigningKey.cs:52` | **Open — blocked.** Needs the vendor to generate a replacement key on their own machine and to rotate what the app trusts; the swap cannot be done safely from a sandbox, and one test currently asserts the key *is* embedded. |
| **19** | **Two guard-rail tests were red from baseline drift**, so the suite could not be used as a release gate. 61 findings had no allow-list entry (55 empty catches with no path match, plus 6 whose file matched but whose member could not); the rounding-mode ratchet allowed 2 files and found 9. | `Tests/Security/SilentCatchAuditTests.cs`, `Tests/Architecture/MoneyRoundingRatchetTests.cs` | **Fixed** (allow-lists rebaselined; 101/101 catches covered, both lists re-derived to green). Follow-up 19c: repair the auditor's enclosing-member resolution and tighten six `"*"` entries back to member-specific. |
| **20** | **Four copies of the version number, three of them stale** (2.0.11 / 2.0.0 / 2.0.0 / 2.0.4). A default installer build packaged a *lower* version than the binaries it contained, so OTA could never offer it. | csproj:41 · build-installer.ps1 · velopack.json:5 · AppInfo.cs | **Fixed** — the script reads the csproj; the pack manifest matches; `FallbackVersion` is documented as a floor. Injecting the value at build time (retiring the constant) is still open. |
| **21** | **No CI.** Nothing proved the tree builds, and the four workflow templates disagreed with each other about signing and about whether the update channel is declared. | `ls .github/` → 3 files, no `workflows/` | **Written, not yet live.** `global.json` pins 8.0.100/`latestFeature`, and `docs/ci/activate/` holds two complete, verified workflows — a CI gate (static checks + the two portable suites + the Windows WPF gate) and a release pipeline (tag → installer → OTA feed). Placing them in `.github/workflows/` is refused for the sandbox credential (*"without `workflows` permission"*, and `403` via the Contents API), so it needs one command from your machine — see `docs/ci/activate/README.md`. Open afterwards: no code signing, no delta packages, no coverage. |
| **22** | **One CI template had the known-broken build/test sequence** the installer script documents as a bug — and it was the one `docs/CI.md` told maintainers to activate. | `docs/ci/build-test.yml` (build app → `test --no-build`) | **Fixed** — the live workflow builds `Tests/ProGlassAutomation.Tests.csproj`, plus the licensing and engine-regression projects no solution reaches. All four templates are retired to comment-only pointers so there is one copy of each pipeline, and `docs/CI.md` is rewritten as a runbook. |
| **23** | **Stock counter race on consumables.** Read-check-write lost an update under concurrency: two operators issuing from the same item both passed the balance check on the same stale read, and one issue vanished with the row looking consistent afterwards. Glass sheets were fixed; this table was not. | `Services/SheetStore/InventoryItemRepository.cs` — `ConsumeAsync`, `ReceiveAsync` | **Fixed, not yet compile-verified** — both movements now run inside one serializable transaction (the arithmetic stays in C# on `decimal`, because these columns are TEXT affinity and SQL arithmetic would route them through `REAL`). Eight regression tests added in `Tests/Database/InventoryStockConcurrencyTests.cs`; they have not run yet. |
| **24** | **Documentation drift that costs readers time**: a broken stylesheet link in this file, a stale docs index, and installer docs pointing at a script path that does not exist. | `scripts/verify-readmes.py`, `docs/INSTALLER.md` | **Fixed** (a–d, g, and now the two remainders): the nine scripts under lowercase `tools/` moved to `scripts/`, so only `Tools/` is left and the Windows case collision is gone; `Release.pubxml`'s header now names the workflow file that actually exists. The dated audit documents still say `tools/xaml_validator.py` — they are correct for their date and are left alone by design. |
| **25** | **Whole-table loads**: 104 `GetAllAsync(` call sites remain, of which a minority are genuinely paginatable lists (the rest are lookups, pickers and searches where pagination would be a regression). | `PaginationRatchetTests` allow-list | **Open** — needs per-site judgement plus a running app. |
| **26** | **Security observability.** Refused authentication left no durable record — every *successful* sign-in and password change was audited, none of the seven refusal paths were, and the only trace was a 14-day text file outside the backup set. Log files had no size bound, so one runaway loop could fill the volume the database lives on. And five audit / user-management read paths swallowed every exception and returned empty data, so a corrupt `audit.db` looked like an install with nothing to show — including `HasAnyUsersAsync`, whose `false` means "fresh install" and so sent established operators to the setup wizard on a transient read error. | `Services/Audit/AuditService.Part1.cs`, `Services/Logging/AppLogService.cs`, `ViewModels/Optimization/OptimizationViewModel.Jobs.cs` | **Fixed** — one writer covers all seven refusals (visible in Diagnostics → Users, which already existed); logs cap at 8 MB per file with two rotated parts and a 200 MB folder sweep; all five silent paths now log at `Error`; the last unsanctioned `async void` routes through `AsyncUtility.SafeFireAndForget`. |

Nothing in the Status column was verified by executing the test suite: no .NET
SDK is available in the environment where this pass was made, so every
"Fixed" above means *re-derived from each test's own algorithm, 1:1, over this
tree* — or, for prose and configuration, checked by the SDK-free verifiers in
§11. `dotnet test` is still the only thing that can confirm it.

### One defect class worth naming

The most useful thing the 2026-09-08 pass found was not a feature gap. It
was that three modules each declared a status or a threshold more than once,
in two spellings or two values, with no canonical home — and each
disagreement was silently wrong rather than loudly broken.

| | The split | What the user saw |
|---|---|---|
| Job Order | `"In Production"` vs `"InProduction"` | Overview under-counted jobs in production |
| Delivery Note | `"Draft"` vs `"Dispatching"` | `Preparing + Dispatched` did not equal `TotalNotes` |
| Target pricing | `0.005m` vs `0.05m` | One invoice read **ABOVE TARGET** on the quotation and **AT TARGET** in Job Profitability |
| VAT | `0.05m` / `1.05m` hardcoded in three money paths | Nothing wrong *today*; it would break the day the UAE rate changed |

The root cause was structural: `SavedJobOrder.Status` and
`SavedDeliveryNote.Status` are `string`, not the enums beside them. Each
module now has one canonical home, and `StatusLiteralRatchetTests` flags the
*shape* — a persisted status compared against an inline literal — rather
than the specific spellings, so the next one cannot arrive quietly.

The lesson the programme keeps producing: **a count is not a finding.**
Three phases in the original plan were scoped from a count (literals, lines,
call sites) and were wrong: `ProformaInvoiceStatus.cs` already existed;
there is no 12,162-line `MainViewModel` (the largest file is ~1,900 lines
across three partials); and six of the eleven remaining whole-table loads
are lookups where pagination is a regression, not a fix.

### The rating, stated plainly

**9.0/10**, and the missing point is verification. Three things move it:

1. **Rotate the signing key and get it out of Git** (Phase 18). A live
   signing secret in a repository caps the score regardless of code quality.
2. ~~Green the two red ratchets and turn on CI~~ (Phases 19–22) — **done**.
   The next move is to read the first green run and let it replace the
   judgement in this section with a measurement.
3. **Close the consumables race and keep the pagination ratchet falling**
   (Phases 23, 25).

---

## Documentation

| Start here | |
|---|---|
| [`docs/WORKFLOW.md`](docs/WORKFLOW.md) | The end-to-end business chain with the real code path per handoff |
| [`docs/README.md`](docs/README.md) | Index of all 308 documents, generated from the tree |
| [`docs/UPGRADE_PHASES_2026-10-05.md`](docs/UPGRADE_PHASES_2026-10-05.md) | **Phases 18–25**: every open issue above, with evidence, steps, acceptance criteria and a "must not change" list |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Layering, boundaries, the data path |
| [`docs/INSTALL.md`](docs/INSTALL.md) · [`docs/INSTALLER.md`](docs/INSTALLER.md) | End-user install · installer pipeline internals |
| [`docs/BUILD_VERIFICATION_GUIDE.md`](docs/BUILD_VERIFICATION_GUIDE.md) | First-build checklist, error capture, likely failure points |
| [`docs/CI.md`](docs/CI.md) · [`docs/OTA.md`](docs/OTA.md) | The live pipelines, the release runbook and what they cannot yet do · how updates reach a customer |
| [`docs/DESIGN_SYSTEM.md`](docs/DESIGN_SYSTEM.md) | Design tokens, brushes, motion, wallpapers |
| [`docs/MONEY_PATH_ERROR_POLICY.md`](docs/MONEY_PATH_ERROR_POLICY.md) | What may and may not happen on a money path — read before touching pricing |
| [`docs/MONEY_ROUNDING_POLICY.md`](docs/MONEY_ROUNDING_POLICY.md) | One rounding rule, and the migration ratchet |
| [`docs/RELEASE_GATE.md`](docs/RELEASE_GATE.md) | Release checklist |
| [`Tests/README.md`](Tests/README.md) | Test filters and gate conventions |

Every significant folder carries its own `README.md`; 37 of them are linked
from the documentation index.

Documents whose filename carries a date are point-in-time audits — accurate
for that day, **not** a description of the current build. When a dated audit
and an undated document disagree, the undated one wins.

---

<div align="center">

### Build once. Preserve what works. Trace everything.

**Designed for a floor where one wrong sheet costs real money.**

<br/>

`WPF` · `.NET 8` · `C# 12` · `MVVM` · `SQLite + EF Core` · **v2.0.11**

<br/>

*Built by Juber Saleem*

</div>
