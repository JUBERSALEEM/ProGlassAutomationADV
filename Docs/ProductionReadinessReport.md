# PROGLASSAUTOMATION PRE-PUBLICATION REPORT

Structure follows Phase 41 of the brief exactly. Covers Phases 0–41.
Commit audited: `59c42a3`. Codebase: 408 C# files (82,608 LOC), 117 XAML,
11 SQLite DbContexts, 13 NuGet packages.

---

> **UPDATE — second remediation round complete.** A-1, A-2, X-6, D-4 visibility,
> PERF-1 and part of S-5 are now fixed. See §16a for the revised recommendation.
> Status remains **NOT READY** for one reason only: the runtime test phases have
> still never been executed, and that cannot be done from this environment.

## 1. Overall Status

# **NOT READY**

Downgraded from my earlier "CONDITIONALLY READY" after completing the phases I had
skipped. The reason is **R-1**: `RelayCommand.Execute` reported failures only via
`Debug.WriteLine`, which is `[Conditional("DEBUG")]` and is **removed by the compiler in
Release**. Your shipped build has been discarding every command failure silently. That is
now fixed, but it means **no production behaviour of this app has ever been observable**,
and the runtime test phases (7, 22, 34, 35, 36) have still never been executed.

Status becomes CONDITIONALLY READY once the runtime matrix in §11 is run on Windows.

---

## 2. Critical Findings

| ID | Severity | File | Issue | Risk | Status |
|----|----------|------|-------|------|--------|
| C-1 | CRITICAL | `Services/Updates/UpdateService.cs` | Downloaded the asset at whatever URL the GitHub API returned and `Process.Start`'d the `.exe`. No hash, no signature, no host validation. | Remote code execution as the logged-in user. | **FIXED** |
| C-2 | CRITICAL | `App.xaml.cs` | `async void` startup; 9 parallel DB inits with no try/catch. Faults after the first `await` bypass `DispatcherUnhandledException`. | Corrupt/locked DB = raw OS crash at launch, no log, unrecoverable for a normal user. | **FIXED** |
| R-1 | CRITICAL | `Commands/RelayCommand.cs` | Catch block reported only through `Debug.WriteLine` → compiled out in Release → **empty catch in production**. | Every failed save / export / status change silently discarded. No diagnosability at all. | **FIXED** |
| C-3 | HIGH | `Services/Export/Csv/CsvExportEngine.cs` | CSV formula injection; protection shipped disabled. | `=cmd\|'/c calc'!A0` as a customer name executes on the accountant's machine. | **FIXED** |
| V-1 | HIGH | `ProformaInvoiceRepository.cs:102` | `int.Parse` on an unbounded `\d+` capture. `PI-99999999999` → `OverflowException`, swallowed by R-1. | Invoice creation stops working with no message. | **FIXED** |
| X-2/X-3 | HIGH | `App.xaml.cs` | No `AppDomain.UnhandledException`, no `TaskScheduler.UnobservedTaskException`. | Background crashes and faulted fire-and-forget tasks invisible. | **FIXED** |
| A-1 | HIGH | 18 call sites | Authorization enforced **only in ViewModels**; 0 checks at the service/repository layer. | Brief §26: "never rely only on hiding a button". Any future caller bypasses permissions. | **OPEN** |
| A-2 | MEDIUM-HIGH | `AuditService.cs:500` | Passwords hashed with **single-round SHA-256** + 16-byte random salt. | Salt is correct, but SHA-256 is fast — GPU brute-force of weak passwords is cheap. Needs PBKDF2/Rfc2898. | **OPEN** |

---

## 3. Privacy Findings

| ID | Issue | Data involved | Local/External | Risk | Status |
|----|-------|---------------|----------------|------|--------|
| P-1 | `AiContextBuilder` sends customer names, discounts, VAT and grand totals to Google/Groq/Cohere | Customer PII + financials | **External** | Gated: user must supply their own key; `IsConfigured` false ⇒ nothing sent | DOCUMENTED |
| P-2 | Gemini key in URL query string | Credential | External | Query strings land in proxy logs / TLS-inspection appliances | **FIXED** (header) |
| P-3 | Crash reports contain full exception + paths | Diagnostics | **Local only, never uploaded** | Acceptable | ACCEPTED |
| P-4 | Logs: 14-day retention, pruned at startup | Diagnostics | Local | Correct | ACCEPTED |

**Telemetry: none.** No App Insights, Sentry, Crashlytics, analytics, or outbound log/crash
upload anywhere. Outbound hosts are only: 3 AI providers (opt-in), `api.github.com`
(updates), `huggingface.co` (optional Whisper model).

---

## 4. Security Findings

| ID | Issue | Severity | Status |
|----|-------|----------|--------|
| S-1 | Secrets in source or git history — working tree + **581 history blobs** scanned | — | **CLEAN** |
| S-2 | SQL injection — all 23 raw-SQL sites use hardcoded constants, no user input | — | **CLEAN** |
| S-3 | TLS — no cert-validation bypass, no `http://`, all 5 callers HTTPS + timeouts | — | **CLEAN** |
| S-4 | API keys at rest — DPAPI CurrentUser + app entropy, no home-rolled crypto | — | **CLEAN** |
| S-5 | Path traversal — `ExportFileNamer` whitelists `[A-Z0-9-]`; `..` and separators cannot survive | — | **CLEAN** |
| S-6 | 22 multi-line silent `catch { }` blocks | MEDIUM | OPEN |
| S-7 | No registry writes; 27 `SpecialFolder.ApplicationData`, 2 `UserProfile` | — | CLEAN (Store-friendly) |

---

## 5. Memory Leak Findings

| ID | Object | Root cause | Evidence | Fix | Status |
|----|--------|-----------|----------|-----|--------|
| M-1 | `SectionActivityViewModel` | Subscribes static `AuditService.ActivityLogged`, never unsubscribes, no `IDisposable` — callers had **no** release path | 12 instantiation sites | `IDisposable` | **FIXED** |
| M-2 | 16 static-event subscriptions, **0** unsubscribes | No unsubscribe discipline | Script over all 408 files | See below | ASSESSED |

**M-2 honest verdict.** I traced ownership of all 16. Fifteen belong to `MainViewModel`
property initializers or the 7 dashboard pages — each created **exactly once** per process
(`MainWindow` is constructed once at `App.xaml.cs:311`). Navigation swaps *content* inside
long-lived ViewModels; it does not recreate them. **These are app-lifetime by design, not
unbounded leaks.** Repeated navigation will not grow memory because no new subscriber is
created. Calling them "16 leaks" would be inaccurate.

---

## 6. Crash Findings

| ID | Scenario | Exception/Risk | Recovery | Status |
|----|----------|----------------|----------|--------|
| X-1 | Corrupt/locked DB at launch | `SqliteException` after `await` | Logged + dialog + clean exit | **FIXED** |
| X-2 | Faulted unawaited Task | `UnobservedTaskException` | Handler added | **FIXED** |
| X-3 | Background-thread throw | `AppDomain.UnhandledException` | Handler added | **FIXED** |
| X-4 | UI-thread throw | Handled, crash report + Error ID | Already correct | PASS |
| V-1 | 11-digit invoice number | `OverflowException` | `TryParse` fallback | **FIXED** |
| X-5 | Printer unavailable / PDF locked | try/catch in `PdfExportEngine` (5 sites) | Already correct | PASS |
| X-6 | Close during save | **No `Closing`/`SessionEnding` handler on MainWindow** | In-flight save can be cut mid-write | **OPEN** |

---

## 7. Data Integrity Findings

| ID | Workflow | Problem | Risk | Status |
|----|----------|---------|------|--------|
| D-1 | All repositories | `using var ctx` per method | None — correct pattern, avoids EF threading bug | PASS |
| D-2 | `.Result` after `await Task.WhenAll` | Tasks already completed — **not** a blocking wait | No deadlock. Grep false positive; verified by reading | PASS |
| D-3 | Document numbering | **Unique index on all 5 doc types** (`ProformaId`, `JobOrderId`, `DeliveryNoteId`, `InvoiceNumber`, statements) | DB rejects duplicates — corruption impossible | PASS |
| R-2 | Double-click on Generate Invoice | Async command, `CanExecute` can't update in time → fired twice | Symptom was a spurious error dialog (DB blocked the duplicate), not corruption | **FIXED** |
| D-4 | 11 separate SQLite DBs | Cross-DB operations cannot be transactional | Inherent to design; PI→JO→DN→TI chain is not atomic | **OPEN (architectural)** |
| D-5 | `while(true)` in JO numbering | Bounded at 99999 + GUID fallback | Safe | PASS |

---

## 8. Performance Findings

| Area | Finding | Result |
|------|---------|--------|
| List virtualization | `ScrollViewer.CanContentScroll="False"` on the main list screens **defeats UI virtualization** even though `IsVirtualizing="True"` is set beside it. The two settings contradict; pixel-scrolling wins. | **OPEN — will degrade at ~1,000+ rows** |
| Data loading | 27 `GetAllAsync()` calls with no paging | Whole table into memory per screen |
| Startup | 9 DB inits already parallelised via `Task.WhenAll` | Good |
| Dashboard | `.AsNoTracking().Select()` projections | Good |

**Before/after numbers cannot be given — no runtime environment. Not measured.**

---

## 9. Dependency Findings

| Package | Version | Licence / Risk | Recommendation |
|---------|---------|----------------|----------------|
| QuestPDF | 2024.12.2 | **Community MIT only below USD 1M revenue.** Code sets `LicenseType.Community` in 2 places. | **Confirm eligibility before commercial sale** |
| ClosedXML | 0.105.0 | MIT | Keep |
| CsvHelper | 33.1.0 | MS-PL / Apache-2.0 | Keep |
| EFCore.Sqlite +Design +Tools | 8.0.10 | MIT, matches net8.0 | Keep |
| NAudio | 2.2.1 | MIT | Keep |
| PdfSharp-WPF | 6.1.1 | MIT | Keep |
| SkiaSharp (+Views.WPF) | 2.88.8 | MIT | Keep |
| ProtectedData | 8.0.1 | MIT | Keep |
| System.Speech | 8.0.0 | MIT | Keep |
| Whisper.net + Runtime | 1.7.4 | MIT, versions matched | Keep |
| Velopack | 1.2.0 | MIT | Keep — safer updater than the hand-rolled one |

**No live CVE lookup was possible** (`api.nuget.org` unreachable). Licence column is from
package knowledge, not a vulnerability feed.

---

## 10. Microsoft Store Concerns

### MUST FIX BEFORE PACKAGING
1. **Disable `UpdateService` when packaged.** The Store owns updates; an app that downloads
   and launches its own installer fails certification (Policy 10.1.1 / 10.2).
2. **QuestPDF licence tier** — confirm.
3. **A-1 service-layer authorization** — UI-only permission checks.
4. **Privacy policy must disclose** that customer names + financial totals leave the device
   when the user configures an AI key, and that Whisper models download from huggingface.co.

### PACKAGING / STORE LATER
MSIX identity, publisher certificate, capability declarations, `%AppData%` behaviour under
container virtualisation, listing assets, age rating, privacy-policy URL.

**Positive:** no registry use, all data under `ApplicationData` — the app is structurally
Store-compatible.

---

## 11. Tests Executed

Static/analytical, all performed in this session:

- Secret scan: working tree + **581 git-history blobs** (Google/OpenAI/Groq/GitHub/AWS/PEM)
- SQL-injection review: all 23 raw-SQL / `CommandText` sites
- TLS / cert-bypass search across all 5 network callers
- Static-event subscribe/unsubscribe balance across 408 files + ownership tracing of all 16
- Timer / CTS / `IDisposable` inventory
- Blocking-async review (`.Result`, `.Wait()`) — 8 hits, all verified non-blocking
- Empty-catch census — 44 total, split 22 deliberate one-liners vs 22 multi-line
- **Command re-entry audit — 38 async mutating commands: 11 guarded, 27 unguarded**
- Unique-index verification across all 5 document types
- Numeric-parse audit — 34 `TryParse` vs 1 unsafe `Parse` (fixed)
- `Process.Start` review (18 sites)
- Filename-sanitisation review
- Log rotation / retention verification
- Telemetry search
- Password hashing review
- Virtualization review across 64 list-bearing XAML files
- Registry / `SpecialFolder` review for Store compatibility
- **13-case URL-spoofing test** of the new update gate (all pass)
- XAML gate: 6 automated checks over 125 files

## 12. Tests NOT Possible

**No .NET SDK, no Windows, no NuGet in this environment.** Therefore **not performed**:

- **Phase 33** — Release build. Compiler/analyzer/nullable warnings **unverified**.
- **Phases 7 & 35** — runtime memory profiling (open module ×50, working set, Gen0/1/2).
  All memory analysis above is static reference-graph reasoning, **not a measured heap**.
- **Phase 22** — large-data performance (100 → 10,000 rows). No before/after numbers exist.
- **Phase 15** — printer/PDF failure testing.
- **Phases 13/14** — malformed CSV/Excel import fuzzing.
- **Phase 34** — the full runtime test matrix (startup/CRUD/documents/network/backup/shutdown).
- **Phase 36** — data-corruption simulation (kill during save/sync/restore).
- **Phase 31** — packaged-MSIX behaviour.
- **Phase 20** — live CVE lookup.

## 13. Files Modified

`Services/Updates/UpdateService.cs`, `App.xaml.cs`, `Services/Export/Csv/CsvExportEngine.cs`,
`Services/AI/AIService/GeminiAiService.cs`, `ViewModels/Shared/SectionActivityViewModel.cs`,
`Commands/RelayCommand.cs`,
`Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs`,
`ViewModels/DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.cs`,
`Converters/FractionToStarConverter.cs` (new),
`Tests/Agent/UpdateOriginSecurityTests.cs` (new),
`tools/layout-verify/dup_property_check.py` (new), `tools/layout-verify/using_check.py`,
`tools/layout-verify/verify_all.sh`, plus the 7 dashboard views + `Styles/DashboardStyles.xaml`.

## 14. Database Changes

**None.** No schema change, no migration, no data transformation. Deliberate: this was a
security/reliability pass, and a schema change would have broken existing user databases.

## 15. Remaining Risks

1. **A-1** — authorization is UI-only (0 service-layer checks). Highest open item.
2. **A-2** — SHA-256 password hashing should be PBKDF2.
3. **X-6** — no window-closing guard; close during save can interrupt a write.
4. **Performance** — `CanContentScroll="False"` defeats virtualization on list screens.
5. **D-4** — no cross-database transaction for the PI→JO→DN→TI chain.
6. **S-6** — 22 multi-line silent catches.
7. **Everything in §12** that could not be tested.

## 16a. Second remediation round — what changed

| ID | Was | Now |
|----|-----|-----|
| **A-1** | 18 UI checks, **0** service-layer checks, 34 reachable mutating methods | `OperationGuard.Demand` on **all 34** mutating repository methods across 7 repositories. Fails closed. `PermissionDeniedException` renders as a clean "Permission Denied" message, not a crash report. 3 extra UI checks added to destructive paths. **FIXED** |
| **A-2** | Single-round SHA-256 | **PBKDF2-HMAC-SHA256, 210,000 iterations**, constant-time compare, self-describing format. Legacy hashes verify once and silently migrate on next sign-in, so no user is locked out. No new dependency. **FIXED** |
| **X-6** | No `Closing`, no `SessionEnding` | `CriticalOperationTracker` + both handlers. Close during a cross-database save now warns, defaulting to Cancel. **FIXED** |
| **D-4** | Cross-DB failure logged only to `Debug.WriteLine` (absent in Release) | Logged **and** audited, with a message stating that re-saving repairs the link. Still non-fatal by design. **VISIBILITY FIXED**; the architectural limit remains. |
| **PERF-1** | Believed a defect, then believed intentional | Both readings were incomplete. `SmoothScrollBehavior` now resolves its mode from `VirtualizingPanel.ScrollUnit`, so the 11 real list hosts run `CanContentScroll=True` + `ScrollUnit=Pixel` — **virtualization and smooth scrolling together**. **FIXED** |
| **S-5** | 22 multi-line silent catches | 3 highest-impact converted to logged warnings (audit trail, AI-memory clear, backup pruning). 19 remain. **PARTIAL** |

**Test count added this round: 29** (`PasswordHasherTests`, `OperationGuardTests`),
on top of the 30 URL-spoofing cases from round one.

### An error I made and corrected
I first reported `CanContentScroll="False"` as a straightforward virtualization
defect. Reading `SmoothScrollBehavior` showed it was deliberate and documented, and
that flipping the XAML had already been tried and reverted. Rather than defend the
original finding or drop it, I established the actual mechanism: WPF disables
virtualization for pixel scrolling, but since .NET 4.5 `ScrollUnit="Pixel"` restores
it — and the behaviour could not see that because it read `CanContentScroll` alone.
Fixing the behaviour first made the XAML change safe. **A finding is only useful once
the mechanism is understood; the first two readings were both wrong.**

## 16. Final Recommendation

**Do not publish yet.** Three critical defects are fixed, but **R-1 means the app has never
been observable in production** — every command failure was discarded. The correct next step
is not more static analysis; it is to **run the Phase 34 matrix on Windows now that failures
finally appear in the log**, then fix A-1 and A-2, then reassess.

Order of work:
1. Release build → capture warnings (Phase 33).
2. Run Phases 7/22/34/35/36 on Windows.
3. Fix A-1 (service-layer permission checks) and A-2 (PBKDF2).
4. Add the `Closing` guard (X-6) and fix virtualization.
5. Gate `UpdateService` off for Store, confirm QuestPDF licence.
6. Re-audit, then package.
