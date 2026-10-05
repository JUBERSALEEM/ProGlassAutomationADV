# ProGlass Automation — Architecture (v2.0)

> Concise, current-state architecture reference. Last verified 2026-09-06
> (post Theme-Studio removal). For the ranked problem ledger and remaining
> issues see `docs/INDUSTRIALIZATION_AUDIT_2026-09-06.md`; for the exhaustive
> module-by-module audit see `docs/CURRENT_PRODUCT_AUDIT_2026-09-05.md`.

## 1. What it is

A Windows desktop ERP for glass-fabrication businesses, built as a **WPF /
.NET 8 / C# 12** application with **MVVM**, **SQLite + EF Core**, and **15
languages**. The defining property of v2 is **traceability**: every record
carries who/what/when, every status mutation is audited, every money figure
persists its calculation context, and the journey from inquiry to the
profitability of the delivered job is one unbroken chain of records.

| | |
|---|---|
| Platform | Windows desktop (`net8.0-windows`, WPF) |
| UI pattern | MVVM (partial-class ViewModels, `RelayCommand`, `ViewModelBase`) |
| Persistence | SQLite via EF Core, one bounded context per file |
| Entry point | custom `[STAThread] Main()` → `VelopackApp.Build().Run()` → `App.xaml` |
| Deployment | Velopack OTA updates (channel `stable`) + classic GitHub-release fallback |
| Licensing | RSA-2048-PSS signed offline licences, trial ledger, feature gates |

## 2. Projects

No `.sln` file — the five projects build independently (the CI workflow
builds/tests the Tests project, which references the app, then publishes the
app project).

| Project | Role |
|---|---|
| `ProGlassAutomation.csproj` | The WPF application (all `Views/`, `ViewModels/`, `Services/`, `Models/`, `Controls/`, `Agent/` …) |
| `Tests/ProGlassAutomation.Tests.csproj` | >1,000 xUnit test methods (static source-pins + executable unit tests) |
| `Licensing/ProGlass.Licensing.csproj` | The licence **contract** library (verify side; ships with the app) |
| `Licensing/ProGlass.Licensing.Tests.csproj` | Licensing library tests |
| `Tools/LicenseGenerator/ProGlassAutomation.LicenseGenerator.csproj` | The vendor-side **signing** tool (never ships) |

## 3. The business chain

```
INQUIRY (+BOQ) ──convert──▶ PROFORMA INVOICE ──confirm──▶ JOB ORDER ──deliver──▶ DELIVERY NOTE ──▶ TAX INVOICE ──▶ STATEMENT
     │                        (9-step wizard)              (factory)            (partial, balanced)     (5% VAT)        + REPORTS
     └───────────────────────────────▶ JOB PROFITABILITY (stale-flagged when upstream numbers change) ◀──┘
```

Supporting modules: **Master Data** (companies/clients/projects/salesmen),
**Specifications** (glass catalogue + usage), **Sheet Store** (inventory),
**Production** (8 stage views), **Statements**, **Job Profitability**, **7
dashboards**, **Reports** (PI/JO/DN/TI/ST + Sales + VAT), **Settings**,
**Licensing/Login/Users/Roles**, **Audit log**, **AI Agent**, **Crash
reporting**, **Backup/Restore**. **Optimization** is a licence-gated
"coming soon" placeholder (not implemented).

## 4. Layered architecture

```
Views (XAML, code-behind = pure glue)
   │  {Binding} → DataContext
ViewModels (per-module partial classes; INPC; RelayCommand; SafeFireAndForget)
   │
Services (domain services + repositories; one DbContext per operation)
   │
SQLite files (11 bounded contexts) + attachments on disk
```

- **State lives in ViewModels**, not code-behind; views are XAML + `Loaded`/
  `Unloaded` glue. Navigation is a rail of `NavRow` items in
  `MainViewModel`; `CurrentModuleKey` selects the active module VM.
- **Async**: the single sanctioned `async void` is `AsyncUtility.
  SafeFireAndForget`; all other async is awaited (no sync-over-async remains —
  verified).
- **DI**: `Services/DependencyInjection` wires service lifetimes (config,
  DB, repositories, domain services) at startup.

## 5. Data architecture

Eleven SQLite files under `AppDataRoot`, each owned by one `DbContext`
(document-store isolation; **no cross-file foreign keys**):

| File | Owned by |
|---|---|
| `proglass.db` | `AppDbContext` — users/roles/settings + Sheet Store inventory |
| `audit.db` | `AuditDbContext` — activity log, sessions, per-record history |
| `aimemory.db` | `AiMemoryDbContext` — agent memory |
| `masterdata.db` | `MasterDataDbContext` — companies/clients/projects/salesmen |
| `inquiries.db` | `InquiryDbContext` |
| `proformainvoices.db` | `ProformaInvoicesDbContext` |
| `specifications.db` | `SpecificationsDbContext` — glass catalogue/usage |
| `joborders.db` | `JobOrdersDbContext` |
| `deliverynotes.db` | `DeliveryNotesDbContext` |
| `taxinvoices.db` | `TaxInvoiceDbContext` |
| `statements.db` | `StatementDbContext` |

**Integrity rules applied uniformly:**
- `SqliteResilience.ConfigureFor` — WAL + `busy_timeout` + FK enforcement on
  every connection.
- `SchemaMigrationRunner` — `PRAGMA user_version`-tracked, transaction-wrapped,
  **backup-before-migrate**.
- `ReadOnlyGate.Demand` — every repository write passes a read-only gate
  (licence-expiry protection).
- `DocumentOperationCoordinator` + `TaxInvoiceDnReconciler` — coordinated
  cross-DB writes with Result/correlation/compensation and a startup
  reconciler for DN↔TI links.
- **Money** is `decimal` end-to-end; margin formula verified correct; document
  numbers are DB-derived sequences (not client-side).
- **Backup** (`BackupService` v2): full-dataset sets incl. whitelisted
  attachment trees (`Inquiries/`, `ProformaInvoices/`, `DeliveryNotes/`),
  pre-restore safety snapshot, retention, restore drill.

## 6. Cross-cutting services

| Concern | Implementation |
|---|---|
| Logging | `AppLogService` — daily structured log file (`%AppData%\ProGlassAutomation\logs\app-yyyyMMdd.log`) + in-memory buffer + `LogWritten` event for the diagnostics view; silent catches are banned and enforced by `Tests/Security/SilentCatchAuditTests.cs` |
| Audit | `AuditService` — global activity + per-record history in `audit.db` |
| Secrets | `SecretVault` — Windows DPAPI (CurrentUser) + app entropy into `secrets.dat` |
| Passwords | `PasswordHasher` — PBKDF2-HMAC-SHA256, 210,000 iterations, 16-byte salt; legacy SHA-256 hashes upgrade on login |
| Licensing | `ProGlass.Licensing` — RSA-2048-PSS offline activation, trial ledger, machine fingerprint, feature gate, read-only fallback |
| Updates | `VelopackUpdateService` → `GithubSource` channel `stable`; classic `UpdateService` as dev/portable path; Authenticode verified via `UpdateSignatureVerifier` |
| Localization | `LocalizationService` over 15 language dictionaries (`Themes/Languages/*.xaml`) |
| Reports/PDF | `Services/Export/Pdf` (`DocumentStyle`) + per-module exporters |
| AI Agent | `Agent/` — tool-calling agent with permissions/redaction/boundary; voice (Whisper + System.Speech); reads inventory/chain/profit/statement/reports; writes exactly one thing (sheet-stock quantity) |
| Demo | `Agent/Demo` — demo director, WASAPI/mic/screen recorders, ffmpeg pipeline, TTS (developer tooling) |
| Crash reporting | global handlers → crash dialog, listing, send/upload |

## 7. Theming & design system

- **Tokens**: `Themes/Styles/` (Light/Dark design tokens + shared card/table/
  dialog/input/typography styles) and `Themes/CrystalFluid/` (the shipped
  Crystal family: tokens, controls, `CrystalWaterControl`, stat-card).
- **Engine**: `VisualThemeEngine` (hub + 3 part files) paints the **three
  shipped visual families** — **Normal** (default), **Monet** (accent-derived
  Material-You), and **Crystal Fluid** (displayed **Ios-17pm**, light-only via
  `ModePairFor`) — with a managed-key contract that keeps crash-critical
  brushes always present. Professional, iOS Liquid Glass, Ocean and the
  Industrial trio were removed 2026-09-06; saved configs referencing any
  removed family resolve to Normal.
- **Pipeline**: `ThemePipeline` is the single rebuild order (family paint →
  accent → outline preference). `WallpaperOrchestrator` + `BuiltinWallpaperScenes`
  drive the Wallpaper Studio; `SystemBackdropService` provides optional Mica/
  Acrylic (Windows 11).
- **`CrystalFluidSettings`** is the single source of truth for crystal token
  baselines (engine and XAML defaults pinned equal by tests).
- **`ThemeCustomization`** (Settings → **Themes**) is the bounded, persisted,
  family-aware replacement for the Theme Studio: six live Crystal Fluid axes
  (water opacity/speed, wave amplitude, caustic, bubble, reflection opacity),
  named presets **Default / Calm / Intense**, and a per-family **Reset**. It
  is the SINGLE writer of the six `Crystal*` tokens (`Apply`), delegated to
  from `ApplyCrystalFluid`; `CrystalReducedMotion` stays a separate §43 flag.
  Normal/Monet keep their Appearance card controls, and the Crystal group is
  only visible while **Ios-17pm** is the active family.
- **Removed 2026-09-06**: the experimental **Theme Studio** (0–100 slider
  playground) and its Quick-Looks companion. The §43 **reduced-motion**
  accessibility toggle was preserved as a standalone Appearance tile.
- **Deferred**: decorative-animation behaviors (`BounceHoverBehavior`,
  `ContentTransitionBehavior`, `SmoothScrollBehavior`, `GridLengthAnimation`)
  remain referenced by active views — removal is a UX decision (K.15 in the
  industrialization report).

## 8. Build / release / OTA

- **Canonical workflow** `docs/ci/dotnet-desktop.yml` (activate by copying to
  `.github/workflows/` as admin): a `build-test` gate (windows-latest:
  restore → build → test) and a tag/`workflow_dispatch`-gated `release` job
  (publish self-contained win-x64 → `vpk pack --signParams` → `vpk upload
  github --publish`).
- Single version source of truth: `<Version>` in `ProGlassAutomation.csproj`;
  tag `vX.Y.Z` must match.
- Feed channel pinned to `stable`; `vpk` pinned to 1.2.0.

## 9. Testing strategy

Tests are static-source pins + executable unit tests, safe on any runner
(no WPF `Application` is constructed). Notable suites: silent-catch audit,
read-only-gate coverage, licence public-key parity, money/rounding pins,
registry-parity (theme families), and bridge tests pinning XAML-token ↔
engine-baseline equality. **No test run is possible on the Linux sandbox** —
a Windows `dotnet build` + `dotnet test` is the required gate before release.

## 10. Deeper references

- `README.md` — product overview + business chain
- `ERP_UPGRADE_ROADMAP.md` — historical RCA ledger
- `docs/DESIGN_SYSTEM.md` — visual design system
- `docs/MONEY_PATH_ERROR_POLICY.md` — money error policy
- `docs/CURRENT_PRODUCT_AUDIT_2026-09-05.md` — exhaustive module audit
- `docs/INDUSTRIALIZATION_AUDIT_2026-09-06.md` — ranked remaining issues
- `docs/CI.md` — CI/release handbook
