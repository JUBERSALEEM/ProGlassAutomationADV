# ProGlassAutomation — Pre-Publication Security, Privacy, Memory & Reliability Audit

**Audit date:** 2026-08-17
**Commit audited:** `775fafc`
**Scope:** 408 C# files (82,608 LOC), 117 XAML files, 11 SQLite DbContexts, 13 NuGet packages.
**Auditor note:** every finding below was reached by reading the actual code. Where a
check could not be performed in this environment (no .NET SDK, no Windows runtime), it is
listed under *Tests Not Possible* rather than claimed as passed.

---

## 1. Overall status

**CONDITIONALLY READY** — not "Store ready".

The codebase is markedly better than typical for its size: no secrets in source or git
history, no SQL injection, DPAPI-protected API keys, per-operation `DbContext` lifetimes,
a pre-restore safety snapshot in the backup service, and no telemetry of any kind. The
blocking items are a small number of specific defects, listed below as C-1 … C-3.

---

## 2. Critical findings

| ID | Severity | File | Issue | Risk | Status |
|----|----------|------|-------|------|--------|
| C-1 | CRITICAL | `Services/Updates/UpdateService.cs:333,409,218` | Downloads a release asset from the URL supplied in the GitHub API JSON and `Process.Start`s the `.exe` with **no hash check, no signature check, and no validation that the URL host belongs to GitHub**. | Remote code execution if the API response is tampered with, if a release asset is replaced, or if the repo/account is compromised. The app runs the binary with the user's privileges. | **FIXED** |
| C-2 | CRITICAL | `App.xaml.cs:126` | `Application_Startup` is `async void` and `DatabaseInitializer.Initialize()` + 9 parallel DB inits run with no try/catch. `DispatcherUnhandledException` **cannot** catch a fault thrown after the first `await`. | A corrupt/locked/read-only DB file kills the app at launch with a raw OS crash dialog and no log entry — unrecoverable for a non-technical user. | **FIXED** |
| C-3 | HIGH→CRITICAL (privacy) | `Services/Export/Csv/CsvExportEngine.cs` | No CSV formula-injection guard. Customer names and free-text fields are written raw. | A customer named `=cmd\|'/c calc'!A0` becomes a live formula when the exported CSV is opened in Excel. Classic CSV injection → command execution on the accountant's machine. | **FIXED** |

---

## 3. Privacy findings

| ID | Issue | Data involved | Local / external | Risk | Status |
|----|-------|---------------|------------------|------|--------|
| P-1 | `AiContextBuilder` sends live business data to a third-party LLM: customer names, discounts, VAT, grand totals, job/delivery/invoice figures (`:523,531-533,592,667,722,741-745`). | Customer PII + financial | **External** (Google / Groq / Cohere) | Business-confidential data leaves the machine. Mitigated: requires the user to paste their own API key; `IsConfigured` returns false without one, so nothing is sent out of the box. | **DOCUMENTED** — consent already required in practice; see note below |
| P-2 | Gemini API key placed in the **URL query string** (`GeminiAiService.cs:114,326`). | Credential | External | URLs are the most commonly logged/proxied part of a request. Google supports the `x-goog-api-key` header instead. | **FIXED** |
| P-3 | Crash reports (`logs/crash-latest.txt`) contain full exception detail incl. file paths. | Diagnostics | **Local only** — verified never uploaded | Acceptable. No telemetry exists anywhere in the codebase. | ACCEPTED |
| P-4 | Logs retained 14 days, pruned at startup, stored under `%AppData%`. | Diagnostics | Local | Rotation exists and is correct. | ACCEPTED |

**Telemetry search result:** no Application Insights, Sentry, Crashlytics, analytics, or
any outbound POST of logs/crash data. The application is genuinely offline-first: the only
outbound hosts are the three AI providers (opt-in, user's own key), `api.github.com`
(update check), and `huggingface.co` (optional Whisper model download).

---

## 4. Security findings

| ID | Issue | Severity | Status |
|----|-------|----------|--------|
| S-1 | Secrets in source / git history — **581 history blobs scanned**, plus full working tree, for `AIza…`, `sk-…`, `gsk_…`, `ghp_…`, `AKIA…`, PEM private keys. | — | **CLEAN — no secret found** |
| S-2 | SQL injection — all `FromSqlRaw`/`CommandText` sites reviewed. Every interpolated value is a hardcoded developer constant (`PRAGMA table_info(Companies)`, fixed column DDL). No user input reaches raw SQL. | — | **CLEAN** |
| S-3 | TLS — no `ServerCertificateCustomValidationCallback`, no cert-validation bypass, no `http://` endpoints. All 5 network callers use HTTPS with explicit timeouts. | — | **CLEAN** |
| S-4 | API keys at rest — DPAPI `CurrentUser` scope + app-specific entropy in `SecretVault`. Correct choice; no home-rolled crypto. | — | **CLEAN** |
| S-5 | 22 multi-line silent `catch { }` blocks swallow exceptions with no logging. | MEDIUM | OPEN — listed in §11 |
| S-6 | `Velopack` updater (the second update path) performs its own signature/integrity validation and is safe; the hand-rolled `UpdateService` was not. | — | see C-1 |

---

## 5. Memory-leak findings

| ID | Object | Root cause | Evidence | Fix | Status |
|----|--------|-----------|----------|-----|--------|
| M-1 | `SectionActivityViewModel` | Subscribes `AuditService.ActivityLogged += OnActivityLogged` (`:121`) and **never unsubscribes**; the class did not implement `IDisposable`, so a caller had **no way at all** to release an instance. Instantiated at **12 sites**. | Static event → strong GC root to every instance ever created. | Implement `IDisposable`, unsubscribe. | **FIXED** |
| M-2 | 16 static-event subscriptions vs **0** unsubscribes across the codebase (`ThemeChanged` ×2, `CurrentUserChanged` ×4, `DataSeeded` ×4, `ActivityLogged` ×2, `AccentChanged`, `LogWritten`, `StateChanged`, `AiProviderChanged`). | No unsubscribe discipline. | Measured by script over all 408 files. | See honest assessment below. | PARTIALLY FIXED |

**Honest assessment of M-2 — this is where I must not overstate the finding.**
I traced the ownership of every subscriber. 14 of the 16 subscribers are properties of
`MainViewModel`, which is created **once** as the `MainWindow` DataContext (`MainWindow.xaml:115`),
and `MainWindow` is constructed exactly once (`App.xaml.cs:311`). The 7 dashboard pages are
likewise created once in the `DashboardViewModel` constructor. Navigation in this app swaps
the *content* of long-lived ViewModels; it does **not** recreate them.

Therefore these 14 subscriptions are **app-lifetime by design, not unbounded leaks**. Memory
will not grow when the user navigates between modules 50 times, because no new subscriber is
created. Calling them "leaks" would be technically fashionable but factually wrong, and
"fixing" them would add teardown code that never runs.

**M-1 — corrected after tracing ownership.** My first reading called this an active leak
because the type is instantiated at 12 sites. I then traced all 12 owners
(`DiagnosticsViewModel` ×9, `DeliveryNoteViewModel`, `JobOrderViewModel`,
`TaxInvoiceViewModel`) and **every one of them is itself a `MainViewModel` property
initializer — created exactly once per process.** So the instances do not currently
accumulate either.

The accurate statement is therefore: **this codebase has no measured, reproducible memory
leak from static events, because navigation reuses long-lived ViewModels rather than
recreating them.** What it had was a *latent* hazard — `SectionActivityViewModel` offered no
disposal path whatsoever, so the moment anyone creates one per-navigation (an obvious future
change: a dialog, a per-document activity panel) it would leak with no way to stop it. The
`IDisposable` implementation closes that door before it opens.

I am stating this plainly rather than claiming to have "fixed 16 memory leaks", because the
evidence does not support that claim. **Phases 7 and 35 (runtime heap measurement over 50
navigations) were not executed — no Windows runtime here — so no leak claim in either
direction is empirically confirmed.** Run them before release.

---

## 6. Crash findings

| ID | Scenario | Exception / risk | Recovery | Status |
|----|----------|------------------|----------|--------|
| X-1 | Corrupt / locked / read-only SQLite file at launch | `SqliteException` on a background thread after `await` | Was: raw OS crash. Now: logged, user-facing dialog, app continues to a usable state or exits cleanly. | **FIXED** (C-2) |
| X-2 | Faulted `Task` never awaited | `UnobservedTaskException` — **no handler registered** | Process could terminate on GC of the faulted task. | **FIXED** |
| X-3 | Exception on a non-UI thread | `AppDomain.UnhandledException` — **no handler registered** | Crash with no log. | **FIXED** |
| X-4 | Exception on the UI thread | Handled — logs, writes `crash-latest.txt`, shows Error ID, sets `Handled = true`. | Already correct. | PASS |
| X-5 | Printer unavailable / PDF path locked | Guarded by try/catch in `PdfExportEngine`. | Already correct. | PASS |

---

## 7. Data-integrity findings

| ID | Workflow | Problem | Risk | Status |
|----|----------|---------|------|--------|
| D-1 | All repositories | `using var ctx = new XDbContext()` per method — no shared context across threads. | None. This is the correct pattern and avoids the classic EF threading bug. | PASS |
| D-2 | `DeliveryNoteListViewModel:672-682`, `DnDashboardViewModel:197` | `.Result` used after `await Task.WhenAll(...)`. | **Not a defect** — the tasks are already completed, so there is no blocking wait and no deadlock. Flagged by a naive grep; verified by reading. | PASS |
| D-3 | Backup / restore | Restore snapshots current state to `pre_restore` first, clears SQLite pools, requires caller confirmation. | Correctly engineered. | PASS |
| D-4 | 11 separate SQLite databases | Cross-database operations cannot be transactional. | Inherent to the design; out of scope for this pass. Documented as a known limitation. | NOTED |

---

## 8. Dependency findings

| Package | Version | Risk | Recommendation |
|---------|---------|------|----------------|
| ClosedXML | 0.105.0 | MIT | Keep |
| CsvHelper | 33.1.0 | MS-PL / Apache-2.0 | Keep |
| EFCore.Sqlite / Design / Tools | 8.0.10 | MIT, matches net8.0 | Keep |
| NAudio | 2.2.1 | MIT | Keep |
| QuestPDF | 2024.12.2 | **Community MIT below USD 1M revenue; commercial licence required above it** | **Commercial-licence check required before paid distribution** |
| PdfSharp-WPF | 6.1.1 | MIT | Keep |
| SkiaSharp (+ Views.WPF) | 2.88.8 | MIT | Keep |
| System.Security.Cryptography.ProtectedData | 8.0.1 | MIT | Keep |
| System.Speech | 8.0.0 | MIT | Keep |
| Whisper.net + Runtime | 1.7.4 | MIT | Keep — versions correctly matched |
| Velopack | 1.2.0 | MIT | Keep |

**QuestPDF is the one licensing item that needs a decision before commercial release.**

---

## 9. Microsoft Store concerns

### MUST FIX BEFORE PACKAGING
1. **`UpdateService` self-update is incompatible with Store distribution.** The Store owns
   updates; an app that downloads and launches its own installer will fail certification
   (Policy 10.1.1 / 10.2). Gate it off when packaged.
2. **QuestPDF licence** — confirm tier (§8).
3. **Whisper model download** (`huggingface.co`, up to ~1.5 GB) must be user-initiated and
   disclosed in the privacy statement.
4. **AI data egress** must be declared in the Store privacy policy (customer names +
   financial data leave the device when the user configures a key).

### PACKAGING / STORE CONFIGURATION — LATER
MSIX identity, publisher certificate, capability declarations, `%AppData%` virtualisation
behaviour, Store listing assets, age rating, privacy-policy URL.

**Not assessed:** whether `%AppData%` writes behave correctly under MSIX container
virtualisation. That requires an actual packaged build.

---

## 10. Tests executed

- Full-tree secret scan (regex: Google/OpenAI/Groq/GitHub/AWS keys, PEM blocks) — clean.
- Git-history secret scan across **581 blobs** in all reachable commits — clean.
- SQL-injection review of all 23 raw-SQL / `CommandText` sites — clean.
- TLS / certificate-validation-bypass search — clean.
- Static-event subscribe-vs-unsubscribe balance across 408 files — 16 vs 0; ownership of
  each traced to determine real impact.
- Timer, `CancellationTokenSource`, `IDisposable` inventory.
- Blocking-async (`.Result` / `.Wait()`) review — 8 hits, all verified non-blocking.
- Empty-catch census — 44 total, split into 22 deliberate one-liners and 22 multi-line.
- `Process.Start` call-site review (18 sites).
- Filename-sanitisation review — whitelist `[A-Z0-9-]`, path traversal impossible.
- Log rotation / retention verification.
- Telemetry / outbound-data search.
- XAML gate: 6 automated checks over 125 files.

## 11. Tests NOT possible in this environment

State plainly: **no .NET SDK, no Windows, no NuGet access** in the audit sandbox. Therefore:

- **No Release build was run.** Compiler/analyzer/nullable warnings are unverified.
- **No runtime memory profiling.** Phases 7 and 35 (open module ×50, measure working set,
  Gen0/1/2) **were not executed**. The memory analysis above is static reference-graph
  analysis, not a measured heap. This must be run on your machine before release.
- **No large-data performance test** (Phase 22: 100 → 10,000 rows) — requires a running app.
- **No PDF/printer failure testing** (Phase 15) — requires Windows print subsystem.
- **No import/export fuzzing with malformed CSV/Excel** (Phases 13/14).
- **No packaged-MSIX behaviour testing** (Phase 31).
- **No NuGet vulnerability database lookup** — `api.nuget.org` unreachable; licence/risk
  column above is from package knowledge, not a live CVE feed.

## 12. Remaining risks

1. 22 multi-line silent `catch { }` blocks (S-5) — each hides a real failure. Low individual
   risk, but they are why a bug in this app is hard to diagnose. Recommend converting to
   `AppLogService.Warning` incrementally.
2. Cross-database transactional integrity (D-4).
3. P-1: business data egress to AI providers is user-gated but the in-app disclosure could
   be more explicit than a settings toggle.
4. Everything in §11 that could not be tested here.

## 13. Final recommendation

Fix C-1, C-2, C-3, P-2 and M-1 (done — see `Docs/SecurityAudit-Fixes.md`), then run the
runtime tests in §11 on Windows. Do **not** package for Store until the update mechanism is
gated and the QuestPDF licence is confirmed.
