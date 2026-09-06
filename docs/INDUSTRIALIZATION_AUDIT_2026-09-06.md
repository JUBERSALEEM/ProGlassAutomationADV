# ProGlass Automation — Industrialization Audit & Safe Refactor

**Date:** 2026-09-06 · **Branch:** `arena/01a0733b-proglassautomationadv` · **Method:** full-repo static audit (read-only discovery → targeted, reference-verified changes). **Runtime verification (Windows build, test run, click-through) was NOT possible in this Linux sandbox** — the app targets `net8.0-windows` + WPF, which only compiles/runs on a Windows host. Every item that needs a Windows box is marked 🔵 MANUAL.

---

## A. Repository understanding

**What it is:** a glass-fabrication desktop ERP — WPF / .NET 8 / C# 12 / MVVM / SQLite + EF Core, 15 locales, Velopack installer + GitHub auto-update, RSA-PSS licensing, and an in-app local AI assistant.

**Scale (measured this session):**

| Asset | Count |
|---|---|
| C# files / LOC | 1,213 / ~236,600 |
| XAML files / LOC | 216 / ~105,800 |
| Projects (.slnx) | 5 — app, `Licensing/ProGlass.Licensing` (net8.0, portable), licensing tests, `Tools/LicenseGenerator` (vendor WPF), `Tests/` (xUnit) |
| SQLite DBs | 11 (one per bounded context: proglass, masterdata, inquiries, proforma, specifications, joborders, deliverynotes, taxinvoices, statements, audit, aimemory) |
| Test facts | ~1,056 (xUnit) |

**Business chain:** Inquiry → Proforma Invoice (9-step wizard) → LPO approval → Job Order → Production → Delivery Note → Tax Invoice → Statement → Job Profitability/Reports. Documents are **snapshot-based** (`FullSnapshotJson`) so master-data edits never retro-mutate issued documents; status transitions are centrally enforced (`Services/Workflow/StatusTransitions.cs`); money paths carry provenance.

**Architecture map (verified):**
- **Entry:** custom `[STAThread] Main()` runs `VelopackApp.Build().Run()` first (correct vpk pattern), then WPF boots via `App.xaml`.
- **UI:** rail navigation (`MainViewModel`), per-module partial-class ViewModels (MVVM), central `RelayCommand`, `SafeFireAndForget` via `Utilities/AsyncUtility` (the single sanctioned `async void`).
- **Data:** `SqliteResilience.ConfigureFor` (WAL + busy_timeout + FK on) on every context; `SchemaMigrationRunner` (`PRAGMA user_version`, transaction-wrapped, backup-before-migrate); `DocumentOperationCoordinator` + `TaxInvoiceDnReconciler` for cross-DB integrity; `ReadOnlyGate.Demand` on every repository write.
- **Backup:** `BackupService` v2 — full dataset sets incl. whitelisted attachment trees (`Inquiries/`, `ProformaInvoices/`, `DeliveryNotes/`), pre-restore safety snapshot, retention, restore drill.
- **Updates:** `VelopackUpdateService` → `GithubSource(JUBERSALEEM/ProGlassAutomationADV)` channel `stable`; `UpdateService` (classic GitHub-Release check) as the dev/portable path; Authenticode verification via `UpdateSignatureVerifier`.
- **Licensing:** `ProGlass.Licensing` contract lib (RSA-2048-PSS, offline activation, trial ledger, machine fingerprint, feature gate). Vendor generator signs; the app verifies. Public/private key halves are pinned equal by a source test.
- **Theming:** `Themes/Styles/` (Light/Dark design tokens + shared styles) + `Themes/CrystalFluid/` (the shipped Crystal family) driven by `VisualThemeEngine` (4 parts), `ThemePipeline`, `WallpaperOrchestrator` (Wallpaper Studio), `SystemBackdropService` (Mica/Acrylic), and `CrystalFluidSettings` (single source of truth for the crystal tokens). The experimental **Theme Studio** (0–100 slider playground) and its Quick-Looks companion were **removed** (2026-09-06); the reduced-motion accessibility switch it hosted was preserved as a standalone Appearance tile.

---

## B. Critical problems found (P0/P1)

Ranked; corroborated against the repo's own `CURRENT_PRODUCT_AUDIT_2026-09-05.md` and re-verified with fresh scans this session.

| # | Sev | Finding | Evidence |
|---|---|---|---|
| 1 | P0 | **No payment/receipt model.** Tax Invoices support only MarkPaid/PartiallyPaid *status*; statements derive "outstanding" from status flags, not amounts. No way to record "customer paid X on date". | No `Payment`/`Receipt` entity; `StatementAggregator` status-based |
| 2 | P0 | **Committed production signing private key.** `Tools/LicenseGenerator/ProductionSigningKey.cs` embeds the full RSA private key (P/Q/DP/DQ/D + D) as a string constant. Acceptable only while the repo is private and the generator never ships; must move to a secret store/HSM before distribution. | Verified this session (full key present) |
| 3 | P1 | **Default admin/admin seeded.** Mitigated by forced password-change + last-admin protection, but a factory-default credential exists until first login. | `AuditDbContext` seed path |
| 4 | P1 | **LPO is a free-text field, not a module.** No LPO entity, uniqueness, amount-match, or persistent link. | `LpoNumber` string fields |
| 5 | P1 | **CI not active.** Build/test/release workflows live under `docs/ci/` because the sandbox GitHub App lacks `workflows` permission; activation is a manual admin copy. *(Addressed in this engagement — see C.1.)* | `docs/ci/*.yml` |
| 6 | P1 | **Optimization module is a "coming soon" stub.** It is license-gated in nav but does nothing. | Stub views |
| 7 | P1 | **Runtime unverified.** No Windows build/run/DPI/license/update matrix has been executed in this environment. | 🔵 MANUAL |
| 8 | ~~P2~~ | **Theme Studio / experimental theme surface** — an in-app theme playground (axis studio, wallpaper orchestration, backdrop effects) that exceeds industrial-ERP needs. **Resolved 2026-09-06:** the Theme Studio slider system and its Quick-Looks companion were removed; wallpaper orchestration/backdrop/crystal token system retained as the shipped design system. | `Services/Theme/*`, `SettingsViewModel.ThemeStudio.cs`, `SettingsAppearanceSection.xaml` |

---

## C. Problems fixed (this engagement)

### C.1 CI / release pipeline (prior turns of this session)
- Created `.github/workflows/dotnet-desktop.yml` (canonical copy in `docs/ci/dotnet-desktop.yml`): `build-test` gate (windows-latest, restore→build→test) + tag/`workflow_dispatch`-gated `release` job (publish self-contained win-x64 → `vpk pack` with `--signParams` Authenticode signing → `vpk upload github --publish`).
- **Fixed a latent OTA bug in the old template:** pinned the Velopack feed channel to `stable` (matches `installer/velopack.json`) and vpk to `1.2.0`; the old `docs/CI/release.yml` used the obsolete `vpk publish` command and omitted `--channel` (default `win`), which would have made the in-app updater silently report "no update".
- **Removed the broken `.github/workflows/dotnet.yml`** (ubuntu-latest can never build a `net8.0-windows` WPF app).
- Version/tag agreement enforced: tag `vX.Y.Z` must equal `<Version>` in the csproj, and the version is stamped into `Version`/`AssemblyVersion`/`FileVersion`.

### C.2 Dead-code / cleanup (this turn)
- **Deleted `Themes/CrystalFluid/incoming/`** — 20 `.txt` scratch files (theme build staging). Not compiled, not referenced anywhere (verified via grep across `.cs`/`.xaml`/`.csproj`), pure dead weight. −~3,200 lines of dead text.
- **Removed two `[Obsolete]` synchronous attachment-open overloads** (`OpenInDefaultApp`, `OpenInDefaultAppWithResult`) from `Services/DeliveryNote/Attachments/AttachmentService.cs`. The only caller already awaited `OpenInDefaultAppAsync`; the sync forms blocked the UI thread via `GetAwaiter().GetResult()`. Security documentation was preserved on the async method, and two newly-unused `using` directives were removed.

### C.3 Verified already-fixed (no change needed)
- **Backup attachment gap (was P0)** — confirmed `BackupService` v2 already copies `Inquiries/`, `ProformaInvoices/`, `DeliveryNotes/` trees recursively on backup *and* restore, with a whitelist that exactly matches the real attachment storage paths (verified against all three attachment services).

### C.4 Theme Studio / Quick Looks removal (this turn)
- **Removed** the Theme Studio subsystem: `Services/Theme/ThemeStudioService.cs`, `Services/Theme/ThemeStudioSettings.cs`, `ViewModels/Settings/SettingsViewModel.ThemeStudio.cs` (659 LOC), `Views/Settings/Controls/StudioSliderControl.xaml(.cs)`, and the studio-only tests (`ThemeStudioSourceTests`, `StudioAxisManifestTests`). −~2,300 source/test lines.
- **Unwired every hook at the root**, not by stubbing: startup restore (`App.StartupCore.cs`), accent-change re-tint (`AppearanceService.ApplyAccent`), family/density/perf re-apply (`ThemeManager.Part2`, `PerformanceProfileService`, `VisualThemeEngine.Part1`), backdrop snapshot (`SystemBackdropService`), pipeline log field (`ThemePipeline`), and the persisted `AppearanceSettings.Studio` field.
- **Removed the studio-coupled Quick Looks** one-click row (its only purpose was to apply a studio profile) along with `AppearanceSettings.QuickLook`; no orphaned command/getter remained (grep-verified).
- **Preserved the §43 reduced-motion accessibility toggle** (it lived inside the studio tile): moved to a standalone Appearance tile bound to a new `SettingsViewModel.ReducedMotion` property (persist + live `CrystalReducedMotion` resource + audit log), and `CrystalFluidSettings.AnimationIntensity` now derives from the persisted flag (1.0 baseline, 0.5 when reduced motion is on).
- **Kept** the professional design system: Light/Dark token switch, the nine shipped visual families, the Crystal Fluid token surface (`CrystalFluidSettings` baselines), Wallpaper Studio (`WallpaperOrchestrator`, `BuiltinWallpaperScenes`), and Mica/Acrylic backdrop (`SystemBackdropService`) — these are the ERP's design system, not the playground.
- **Localization:** removed 344 orphaned studio/quick-look strings across all 15 language files; renamed `Settings.StudioReducedMotion*` → `Settings.ReducedMotion*` (en-US + ar-AE).
- **Tests:** trimmed studio-only assertions (`ThemeRegistryParityTests` F-03 preset pins + `DefaultsFor` range test; `CrystalFluidSettingsBridgeTests` studio master-mapping + default-profile tests; `CrystalFluidPreviewAdoptionTests` now pins the fixed preview `WaterLevel="0.4"`); added `ReducedMotion` to the `SettingsRedesignTests` binding pin.

### C.5 P3 hardening (this turn)
- **Converters no longer throw in `ConvertBack`**: `ColorStringConverter`, `PercentToWidthConverter` (both `IValueConverter` and `IMultiValueConverter` forms), and `SliderValueLabelConverter` now return `Binding.DoNothing` (per-source `DoNothing` for the multi-value forms) instead of `NotImplementedException`, so a stray two-way binding can never crash the UI.
- **`KeyProtection` silent catches made observable**: the rate-limit ledger read failure and the `MachineHash` fallback now log via the generator's `AppLogService.Warning` stub. Degradation is preserved (empty ledger / `UNKNOWN` fingerprint) but the vendor is told the limiter stopped tracking.
- **Velopack PDB policy resolved**: the shipped package now excludes `*.pdb` (`packagingScope.include` drops the glob; `**/*.pdb` added to `zipExclude`). PDBs remain in the publish output for the vendor's own symbolication; `docs/CI.md` documents the trade-off and how to reverse it.

### C.6 Localization parity back-fill (this turn)
- **Closed the K.13/K.14 gap** by back-filling the 35 English-only keys into every non-English runtime with hand-authored translations per language (no machine fallback): 5 `ProformaInvoice.Colour*` colour-detection strings + 15 `Settings.Wallpaper*` studio strings + 15 `Settings.LocaleFallback*` / `ReducedMotion*` / `Preview*` / `WallpaperNone` / `WallpaperNamed` strings.
- **Runtime XAML (`Themes/Languages/*.xaml`):** all 14 non-English dictionaries now carry the full key set — 2,523 keys each, exact parity with `en-US.xaml` (ar-AE was missing only the 20 K.13 strings; the other 13 were missing all 35).
- **Canonical sources (`scripts/i18n/*.py`):** the same 35 keys added to all 15 `STRINGS` dicts (en-US included) so the build-parity invariant stays green at 2,440 keys per language. English values taken verbatim from `en-US.xaml`; non-English values are authored translations.
- **Translation authoring corpus (`scripts/i18n/translations/*.json`):** 30 `Settings.*` strings added to each `-settings.json` and the 5 `ProformaInvoice.*` strings to each `-pi.json` (their correct area chunks per `chunk-split.py`), keeping `merge-keys.py` able to re-apply them.
- **Non-destructive by design:** insert-only script — 57 files changed, +1,490 / −0 lines; **no `build.py` regeneration was run**. Static validation passed: every XAML/JSON parses, `.py` modules compile and stay sorted/unique, key parity holds across all 15 runtimes, and no duplicate keys.
- **Still open (pre-existing reverse drift, out of scope this phase):** `en-US.py` ↔ `en-US.xaml` drift is now 83 keys (was 118) — 83 XAML keys absent from the `.py` sources plus one drifted value (`ProformaInvoice.NoCostedLinesYetPressRecalculateOnceStep5`). `build.py` regeneration stays off-limits until that drift is resolved, since it would delete 83 shipped strings.

### C.7 Global water material (this turn)
- **Closed CRYSTAL_FLUID_DESIGN.md deviation #4** (water previously bound only to the studio preview / StatCard / previews). Added `Controls/CrystalFluid/CrystalWaterAttach.cs` — an attached `CrystalWaterAttach.Attach` property that wraps a card `Border`'s child in a `Grid` layering `CrystalWaterControl` behind content (glass → water → content), self-gated on `CrystalWaterDefaultEnabled` + `CrystalDefaultWaterLevel` via `SetResourceReference` (live family switches, no reload). Layout fidelity preserved by transferring `Border.Padding` onto the content margin.
- **Wired into all card styles** — 27 styles across 19 files (shared `BaseCardStyle`/`TaskCardStyle`/`ListRowCardStyle`/`ModuleSectionCardStyle`/`DialogCardStyle`/`LiquidGlass*`/`SpecGroupCardStyle`/`DashCardStyle`/`SoftCardStyle` + dashboard tiles/wells `DashTileStyle`/`SoftTileStyle`/`SoftChartWellStyle`/`CompactStatTileStyle`/`PiCostTileStyle` + per-module `KpiCardStyle`/`TgtCardStyle`/`AboutCardStyle`/`SectionCardStyle`/`PdfOptionCardStyle`/`WizardCardStyle`/`LicenseMetricCardStyle`/`PendingRowCardStyle`). Opaque `HeroCard*` banners, workspace canvases, and non-card chrome deliberately excluded. Regression pin added (`Tests/Agent/CrystalFluidGlobalWaterTests.cs`).
- **Trade-off:** one extra `CrystalWaterControl` per card; inert (Collapsed, no storyboards) under non-crystal families, pauses off-screen / on reduced motion under Ios-17pm. Full analysis: `docs/WATER_ON_CARDS_ANALYSIS_2026-09-06.md`.

---

## D. Architecture improvements

- Single, purpose-built release pipeline that is the one true OTA wiring point (build → sign → pack → publish feed), removing the split between a build workflow, a stale release template, and an un-buildable ubuntu workflow.
- Removed the last sync-over-async public API in the attachment path, so the entire app now has **zero** real `GetAwaiter().GetResult()`/`.Wait()` calls (verified: the only remaining hits are comments).

---

## E. Removed systems

| System | Disposition |
|---|---|
| `Themes/CrystalFluid/incoming/` (20 scratch files) | **Removed** — dead staging artifacts |
| `AttachmentService.OpenInDefaultApp` / `OpenInDefaultAppWithResult` (obsolete sync API) | **Removed** — dead; async form is the only caller path |
| `.github/workflows/dotnet.yml` (Ubuntu .NET workflow) | **Removed** — un-buildable for a WPF app; superseded by `dotnet-desktop.yml` `build-test` |
| Theme Studio (slider playground) + Quick Looks | **Removed** — `ThemeStudioService`, `ThemeStudioSettings`, `SettingsViewModel.ThemeStudio`, `StudioSliderControl`, studio tests; §43 reduced-motion toggle preserved as a standalone tile |

**Still deferred (needs a UX decision + Windows build — see K):** the decorative-animation behaviors (`BounceHoverBehavior`, `ContentTransitionBehavior`, `SmoothScrollBehavior`, `GridLengthAnimation`) are referenced by active views, so removing them is a UX change, not dead-code cleanup.

---

## F. Database improvements

- **Verified healthy:** 11-DB bounded-context topology is intentional (document-store isolation, no cross-file FK). WAL + busy_timeout + FK enforcement on every connection via `SqliteResilience`. Schema migrations are `user_version`-tracked, transaction-wrapped, backup-before-migrate. Cross-DB writes go through `DocumentOperationCoordinator` (Result + correlation + compensation) with a startup reconciler for DN↔TI links.
- **Remaining risks (honest):** (1) mid-session crash between two coordinated writes leaves a one-session divergence repaired only on next launch; (2) backup sets are manually-triggered + on-exit (no scheduled daily backup); (3) attachment trees are now covered, but restore correctness should be proven with a restore drill on a non-dev machine 🔵.

---

## G. Business logic improvements

**Verified correct (spot-checked this session):**
- **Money is `decimal` everywhere** — a scan for `double/float` money fields returned zero hits.
- **Margin vs markup not mixed** — `CalculateCostBasedProfitMarginPercent` computes `(Selling − Cost) / Selling × 100` (true margin, cost basis incl. base + U-insert), guarded against divide-by-zero/incomplete specs.
- **Document numbering is DB-derived and persistent** — Tax Invoice sequence uses `MaxAsync(SequenceNumber) ?? 0` over the family table (not a UI counter), so it survives restart/recovery.
- **No recompute drift** — issued documents persist `FullSnapshotJson`; Job Profitability reads Step-9 numbers rather than recomputing money.

**Not changed:** PI and TI use separate money-total implementations by design (snapshots + `TaxInvoiceValidationService` control the boundary). Consolidating them is a medium-risk refactor that needs a test run to verify; left intact.

---

## H. Performance improvements

- **Verified:** list DataGrids use row-model projections (not entity materialisation); dashboards use metric projection queries (N+1 addressed for 6/7); TI attachment blobs are excluded from list queries via a lazy `HasContent` door; no real sync-over-async remains (zero `GetAwaiter().GetResult()` calls).
- **Unknowns requiring a Windows load test 🔵:** 10k-row Step-5 grid, 500+ DN balance sweep, statement over 1k invoices. No changes made without measurement (per the "no blind caching" rule).

---

## I. Security improvements

**Verified strong:** PBKDF2-HMAC-SHA256 (210k iters) password storage; DPAPI-encrypted API-key vault with redaction; RSA-PSS license verification before install; `ProcessStartGuard` + attachment launch allow-list; `ReadOnlyGate` on every repository write; no hardcoded API keys/tokens in `.cs` (grep clean).

**Open items (ranked):**
1. **P0/P1 — signing private key committed** (`Tools/LicenseGenerator/ProductionSigningKey.cs`). Recommended: externalize to an env-var/user-secret/HSM read at generator build time, keep the public half only in the repo, and rotate before any public distribution. **Not changed** — key rotation invalidates existing licenses and is a vendor decision, so it requires explicit approval.
2. **P1 — default admin/admin** with forced change. Recommend moving to a first-run-created admin (no seeded default) before first customer.
3. **P1 — CI secrets** (`CODESIGN_PFX_*`, `GITHUB_TOKEN`) — documented in the workflow; store them in repo settings, never in the repo.

---

## J. Testing

**Honest status: no tests were executed.** The test project targets `net8.0-windows` (WPF) and cannot build/run on this Linux sandbox (no Windows Desktop targeting pack). ~1,056 xUnit facts exist, including negative-path pins (silent-catch audit, read-only gate coverage, demo isolation, source-pins for the license public-key equality, price-basis, invoice rounding, DGU/LAM pricing, delivery balance).

The changes in this engagement were verified by **static reference analysis** (every removed symbol grep-confirmed unreferenced across `.cs`/`.xaml`/`.csproj`), not by compilation. A Windows `dotnet build` + `dotnet test` run is the required next gate (🔵).

---

## K. Remaining issues (complete, honest)

**P0**
1. Payments/receipts model absent (TI → "paid" is status-only). *Fix: receipts table + allocate-to-invoices + status roll-up with amounts, or explicitly de-scope "TI is end of chain" and re-label statements.*
2. Run the full Windows matrix before any customer: clean build, 1,056 tests, clean install → first-run → license activation → update → restore drill (incl. attachments).

**P1**
3. Move the signing private key out of the repo (rotate + secrets store).
4. Activate CI by copying `docs/ci/dotnet-desktop.yml` (and `build-test.yml`) into `.github/workflows/` from an admin account (the bot cannot push workflow files — this is a platform permission, not a code problem).
5. Replace seeded admin/admin with first-run admin creation.
6. LPO minimum viable (uniqueness, amount-vs-PI warning, link record) or explicit de-scope.
7. Verify Production-stage roll-up to Job Order (8 stage views exist; status roll-up unverified 🔵).

**P2 (selected)**
8. ~~Theme Studio / experimental-theme removal~~ **Done 2026-09-06** (see C.4/E). Remaining sibling decision: the decorative-animation behaviors (see K.15).
9. Statement aging by amounts (post-payments).
10. Backup scheduling (auto-daily + prune).
11. Doc pruning: ~130 root+docs markdown files → a single `/docs/INDEX.md` entry point.
12. Accessibility/keyboard map over the PI wizard.
13. ~~Localization gap: the 20 strings introduced by Wallpaper Studio + ProformaInvoice colour detection are English-only (missing in `ar-AE.xaml`; parity scan lists them). Back-fill translations or accept en fallback.~~ **Done 2026-09-06** — back-filled with authored translations in all 14 non-English runtimes (see C.6).
14. ~~Reduced-motion string coverage: `Settings.ReducedMotion*` exists only in en-US/ar-AE; the other 13 languages fall back to English for the new tile (consistent with their pre-existing studio coverage).~~ **Done 2026-09-06** — `Settings.ReducedMotion*` plus the related `LocaleFallback*`/`Preview*`/`WallpaperNone`/`WallpaperNamed` strings back-filled in the other 13 languages (see C.6).

**P3**
15. Decorative-animation behaviors (`BounceHoverBehavior`, `ContentTransitionBehavior`, `SmoothScrollBehavior`, `GridLengthAnimation`) are still referenced by active views — removal is a UX change needing sign-off + a Windows visual pass.
16. ~~`ConvertBack` in 3 converters throws `NotImplementedException`~~ **Done 2026-09-06** — `ColorStringConverter`, `PercentToWidthConverter`, `SliderValueLabelConverter` now return `Binding.DoNothing` (per-source `DoNothing` for the multi-value forms) instead of throwing.
17. ~~`KeyProtection.LoadRateLimitEntries` bare `catch { }`~~ **Done 2026-09-06** — the ledger read failure and the `MachineHash` fallback now log via the generator's `AppLogService.Warning` stub (the benign empty-ledger / `UNKNOWN` degradation is preserved, but observable).
18. ~~Velopack `*.pdb` include vs zipExclude inconsistency~~ **Done 2026-09-06** — policy chosen: **exclude PDBs from the shipped package** (removed `*.pdb` from `packagingScope.include`, added `**/*.pdb` to `zipExclude`). PDBs are still produced in the publish output for the vendor's own symbolication; trade-off is less detail in end-user crash stacks, which can be reversed by re-adding the glob.

---

## Theme Studio removal — executed (2026-09-06)

The in-app Theme Studio was the one genuine "theme playground" (§5 target). It was removed as a controlled refactor (commit `24a6282`), with the plan's over-broad engine step deliberately narrowed:

- **Removed** (the playground): `ThemeStudioService`, `ThemeStudioSettings`, `SettingsViewModel.ThemeStudio`, `StudioSliderControl`, the studio tile + Quick-Looks tile in `SettingsAppearanceSection.xaml`, `AppearanceSettings.Studio`/`QuickLook`, and the studio-only tests.
- **Kept** (the ERP design system — correcting the plan's step 2, which would have deleted the shipped Wallpaper Studio and Crystal family): `ThemePipeline`, `WallpaperOrchestrator`, `WallpaperProviderService`, `BuiltinWallpaperScenes`, `SystemBackdropService`, `CrystalFluidSettings`, `WallpaperModules`, and `VisualThemeEngine` — these drive the nine shipped visual families, the wallpaper scenes, and the Mica/Acrylic backdrop. Only the studio-specific hooks inside them were stripped.
- **Kept** (not studio): `LiquidGlassThemeSourceTests.cs` and the bulk of `CrystalFluidSettingsBridgeTests.cs` / `ThemeRegistryParityTests.cs`, which pin the retained design system.
- **Preserved** the §43 reduced-motion accessibility toggle as a standalone Appearance tile (new `SettingsViewModel.ReducedMotion`).

**Not executed (still open):** the decorative-animation behaviors (plan step 5) — they are referenced by active views and their removal is a UX decision (see K.15).

---

## Bottom line

The repository is structurally a **late-stage, well-audited ERP** — the business chain, data integrity, licensing, and money math are sound and, in most places, already hardened by prior passes. This engagement:
- **fixed** the CI/OTA release wiring and removed a broken workflow and stale template commands,
- **removed** confirmed dead theme scratch files, the last sync-over-async attachment API, and the experimental Theme Studio / Quick Looks playground (while preserving the professional design system and the reduced-motion accessibility toggle),
- **back-filled** the 35 English-only localization strings across all 14 non-English runtimes, their canonical `.py` sources, and the translation authoring corpus (K.13/K.14 → done),
- **verified** (rather than re-broke) the backup P0, decimal money, margin math, and DB-derived numbering,
- and **produced this honest ledger** of what remains.

The single highest-value next step is a **Windows build + test + runtime-matrix pass**, followed by the payments decision and moving the signing key out of the repo.
