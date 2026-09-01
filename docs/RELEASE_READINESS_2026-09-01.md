# ProGlass Automation — Production Release Readiness (Deep Audit)

**Date:** 2026-09-01
**Branch:** `arena/01a0578f-proglassautomationadv`
**Method:** static senior analysis only (no .NET SDK / Windows runtime in
this sandbox — the Windows build + `dotnet test` + visual smoke remain the
final gate, as with every prior audit).

**Scope honoured:** the God files (3 God Views + their VMs, `DataGrid*` /
`DataGridView*`, the six 1,900+ line ViewModels and the big detail/list
views) are **reported but not touched**. Everything else was scanned for
release-blocking defects and fixed where a safe, low-risk patch existed.

---

## 1. What the deep scan found and FIXED

### 1.1 Export files respected the operator's OS culture (REAL BUG, fixed)

**Defect.** Every customer-facing export (PI / JO / DN / Statement CSV,
Excel and PDF + the central report CSV/PDF) formatted money, SQM and dates
with the *current OS culture*: `value.ToString("N2")` with no
`IFormatProvider`.

On a UAE or European machine this silently changes the **content of
delivered files**:

- `de-DE` / `fr-FR` / `nl-NL`: `1234.56` → `1.234,56` in PDFs,
  and in CSV the decimal separator becomes the thousands separator of the
  writer — the file cannot be re-imported or read by Excel reliably.
- `ar-AE`: renders **Eastern Arabic digits** (`١٬٢٣٤٫٥٦`) and Arabic
  month names in `dd MMM yyyy` — the customer's own PDF shows numerals a
  finance team in another office cannot reconcile.
- The build suppresses `CA1305` (specify IFormatProvider), so a green
  build never catches this class.

**Fix.** Pinned every export-boundary `ToString(…)` to
`CultureInfo.InvariantCulture` across the complete export surface
(12 files):

- `Services/Reporting/ReportingExportService.cs` — all `PiMap/JoMap/TiMap/
  GlassMap/CustomerMap/CompanyMap/SalesmanMap/ProjectMap/StatementMap/
  StatementLineMap` (CSV **and** PDF maps) + the Excel date cells.
- `Services/ProformaInvoice/Export/ProformaInvoiceCsvLayout.cs`
- `Services/ProformaInvoice/Export/ProformaInvoiceExcelLayout.cs`
- `Services/ProformaInvoice/Export/ProformaInvoiceListExcelLayout.cs`
- `Services/ProformaInvoice/Export/ProformaInvoicePdfLayout.cs`
- `Services/DeliveryNote/Export/DeliveryNoteCsvLayout.cs`
- `Services/DeliveryNote/Export/DeliveryNoteExcelLayout.cs`
- `Services/DeliveryNote/Export/DeliveryNotePdfLayout.cs`
- `Services/JobOrder/Export/JobOrderCsvLayout.cs`
- `Services/JobOrder/Export/JobOrderExcelLayout.cs`
- `Services/JobOrder/Export/JobOrderPdfLayout.cs`
- `Services/Statement/Pdf/StatementPdfDocument.cs`

**Guard added.** `Tests/Agent/ExportCultureInvarianceTests.cs` — source
invariant that fails the build if any export file formats `N1/N2/N3`,
`0.000/0.##` or a written date without an explicit culture. UI display
strings deliberately keep the user's culture (that is correct — they were
left alone).

---

## 2. What was deeply reviewed and found SOUND (no fix needed)

| Area | Verdict |
|---|---|
| XAML ↔ code-behind wiring (all 179 files) | **0 missing event handlers, 0 missing `ElementName`, 0 forward `StaticResource`, 0 duplicate keys** (3 independent scanners) |
| `{Binding}` paths vs root ViewModels | **0 missing root bindings** — every reported hit was a false positive (`RelativeSource`, `Source=` or a `DataTemplate` item binding) |
| MVVM boundary | Code-behind uses the repo's deliberate **delegate-dialog pump** (VM stays UI-free, view supplies `MessageBox`/`SaveFileDialog`/windows). Not an anti-pattern. |
| Storage paths | All data under `%AppData%\ProGlassAutomation`; every `DbContext` / service creates its folder; no writes to `BaseDirectory` / CWD (no Program-Files breakage) |
| SQLite resilience | `SqliteResilience.ConfigureFor` applies WAL + `busy_timeout=5s` + `foreign_keys=ON` on **every** context connection |
| Licensing fingerprint | WMI query is deterministic (stable winner by sort key), `property`/`wmiClass` are compile-time constants (no injection), 5s timeout, never throws |
| Update service | Download/install/signature-verify paths are logged with correlation IDs; version metadata consistent |
| Startup / crash handling | 4 sinks → single `CrashReportService`; session-ending guard; permission-denied handler |
| In-flight profitability/wastage WIP | Reviewed line-by-line: `GetByStatusAsync("Approved")` + in-memory case-insensitive re-check is coherent; net/profit/margin now read from the persisted Step 9 snapshot (no header recompute, no VAT divisor); average-vs-portfolio margin distinction documented; `SheetQuantity` persists with old-snapshot fallback 1; glass-build resolution (`spec first, type second, SGU last-resort`) matches between Step 5/6/automation rules; `SideDisplay` raises INPC on the paths that can change it |

---

## 3. Residual findings — triaged, deliberately NOT changed

| # | Finding | Severity | Why left |
|---|---|---|---|
| R1 | `MessageBox.Show` / `new Window()` inside ~20 non-god ViewModels (no `IDialogService`) | Maintainability | Repo-wide convention with a working delegate pump in several views; introducing a dialog abstraction on a release branch without a Windows build is high-risk, low-reward. Track post-release. |
| R2 | `AttachmentService.OpenInDefaultApp*` uses sync-over-async (`GetAwaiter().GetResult()`) on `ValidateAttachmentAsync` | Low today | Default `IVirusScanner` is the **NoOp** scanner (task already complete — no block, no deadlock thanks to `ConfigureAwait(false)`). Becomes a UI freeze only when a real AV scanner is injected; conversion would touch the God-scoped delivery-note VM. |
| R3 | `MachineFingerprint` builds WQL via string interpolation | Low | Values are internal constants, never user input; sealed by the deterministic-order fix. |
| R4 | `InquiryListViewModel` 3× silent `catch {}` on `CancellationTokenSource` dispose | Low | CTS dispose is the repo's documented benign teardown carve-out (SilentCatchAuditTests allow-list). |
| R5 | 16 `async void` in views | Low | All are `OnLoaded`/`_Click` event handlers (or the central fire-and-forget helpers) — the accepted WPF pattern; already centralised via `AsyncUtility`. |
| R6 | 17 "SQL interpolation" hits | False alarm | Log/`MessageBox` message strings, not SQL (only `MachineFingerprint` WQL — see R3). |
| R7 | 27 silent catches | Mostly tests | 22 are `File.Delete`/`Directory.Delete` cleanup in tests; production hits are the allowed teardown set. |
| R8 | God files carry 700+ findings of the same classes (missing bindings 0, but code-behind logic + 1,900-line VMs) | Architecture | **User directive: untouched.** Re-file the `Docs/AUDIT_REPORT_2026-08.md` item 7 (God-ViewModel decomposition) as the top post-release item. |

---

## 4. Pre-ship checklist (Windows machine, one deploy pass)

1. `dotnet build` + `dotnet test` (test project includes the new
   `ExportCultureInvarianceTests`).
2. **Locale smoke (the fixed bug):** set Windows display/format language to
   Arabic (`ar-AE`) or German, export a PI CSV + PDF + central report →
   amounts must read `1,234.56` and `dd MMM yyyy` in English.
3. Visual smoke: Normal (cream, no blue), iOS Light/Dark (Settings rail
   glassy + visible, main drawer legible), Ultra over busy wallpaper.
4. Licensing: replace the placeholder public key in
   `Services/Licensing/LicensePublicKey.cs` before the first customer tag
   (already flagged by `RELEASE_GATE.md` §2).
5. Admin: add `CODESIGN_PFX_BASE64` / `CODESIGN_PFX_PASSWORD` GitHub secrets
   so the release workflow signs the artifacts.

---

## 5. Verification of this pass

- `tools/xaml-guard.py` → clean (179 files)
- `tools/xaml-resource-lint.py` → clean (7 benign pre-existing L2 warnings)
- `tools/publish-audit.py` (new) → **P1 = 0** (missing handlers / missing
  ElementName / missing root bindings / XAML parse), P2 = 56 (all the
  deliberate code-behind pump + crash paths), P3 = 61 (all triaged as
  allowed, test-cleanup or false-positive — table §3)
- `tools/xaml-xref-audit.py` (new) → forward refs 0, duplicate keys 0
- New `ExportCultureInvarianceTests` mirrors against the real files → 0 offenders
- `git diff --check` → clean; all patched C# files brace/paren-balanced
- Theme changes carried in this worktree are the previously emulator-verified
  cream/Liquid pass (PASS 219) and are pinned by `LiquidGlassThemeSourceTests`.
