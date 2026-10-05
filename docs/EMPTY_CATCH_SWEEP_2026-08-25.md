# Empty-catch sweep — 2026-08-25

**Branch:** `arena/01a038f2-proglassautomationadv`
**Scope:** every production `.cs` file (857 files, 170,297 LOC)
**Result:** **0 truly-empty catch bodies remain.** 170 silent catches were
made observable; the 67 that remain silent are structural exceptions,
each documented and enforced by `Tests/Security/SilentCatchAuditTests.cs`.

---

## Why this sweep happened

The 2026-08-19 audit scored **Error handling 4.0/10**: 89 empty catches,
318 `Debug.WriteLine` calls that the Release compiler deletes. Phases 1–10
claimed this was fixed ("0 empty catches in prod"). A code-level
re-measurement on 2026-08-25 found the claim was false:

| Metric (measured, not claimed) | Before sweep | After sweep |
|---|---|---|
| Truly-empty `catch { }` bodies in prod | **87** | **0** |
| Silent catches incl. block-comment bodies (`catch { /* x */ }`) | **170** | **67** (structural only) |
| Silent catches outside the Roslyn audit's reach | 84 | **0** |
| `Debug.WriteLine` files | 18 | 13 |

The gap existed because two sweeps were needed, and because the enforcing
test only scanned `Services/`, `ViewModels/` and `App.xaml.cs` — Views,
Behaviors, Commands, Agent, Utilities, Controls, Models and Tools were
invisible to it.

## What was done

### Sweep 1 — the 87 truly-empty catches
Each site was reviewed in context and either:

- **Routed to `AppLogService`** (`Warning` for user-visible degradation,
  security denials, audit-write failures and subscriber-exception dispatch;
  `Debug` for probes, teardown and best-effort cleanup), e.g.

  ```csharp
  // before
  try { JobOrderVM?.Dispose(); } catch { }
  // after
  try { JobOrderVM?.Dispose(); }
  catch (System.Exception ex)
  { AppLogService.Debug("[MainViewModel] Dispose: JobOrderVM teardown failed: " + ex.Message); }
  ```

- **Marked policy-silent** with a comment, when silence is a structural
  requirement (the logger must not recurse into itself, the crash reporter
  must never throw, last-chance handlers must not re-throw).

### Sweep 2 — the 83 block-comment catches the first inventory missed
`catch { /* best effort */ }` style swallows — invisible to a line-based
inventory — across Agent demos, recorders, TrialLedger anchors, update
service, settings feature toggles, licensing views. Same treatment:
logged with their original rationale preserved in the message, e.g.

```csharp
catch (System.Exception ex)
{ AppLogService.Warning("[AuthSessionService] ClearSavedCredentialsCore: audit must not block security cleanup: " + ex.Message); }
```

### Enforcement — `SilentCatchAuditTests` hardened
1. **Audit scope extended** from `{Services, ViewModels, App.xaml.cs}` to
   the **entire production tree** (Agent, Behaviors, Commands, Controls,
   Converters, Models, Services, Tools, Utilities, ViewModels, Views,
   `App.xaml.cs`, `MainWindow.xaml.cs`).
2. **Three new allowlist entries**, each with a one-line justification:
   `RelayCommand.cs/Execute` (logging guard must never mask the original
   failure), `DemoJobQueue.cs` (director already records failures),
   `LogEntry.cs` (logger infrastructure must never throw / recursion risk).
3. The test now fails the build on ANY new silent catch anywhere in
   production code unless it is added to the allowlist with a reason.

## What remains silent — and why it is correct

67 catches, all on the test's allowlist:

| Class | Reason |
|---|---|
| `AppLogService.cs`, `LogEntry.cs` | The logger must never throw into a catch handler; logging from its own internals risks recursion |
| `CrashReportService.cs` | The crash reporter must never throw |
| `App.xaml.cs` last-chance handlers | Already logged above the catch; re-throwing from a last-chance handler terminates the process |
| `AsyncUtility.cs` | Explicit fire-and-forget sink; logs upstream |
| `ConnectivityService.cs`, `UserPresenceService.cs` | Network poll resilience — per-packet failures must not flood the log |
| `ProcessStartGuard.cs` | Never-throw verdict API by design |
| `InquiryListViewModel.cs`, `ReportBaseViewModel.cs` | `OperationCanceledException` on superseded loads — quiet by design |
| `RelayCommand.cs` inner guard, `DemoJobQueue.cs` | See allowlist justifications |

## Verification (no compiler in sandbox — mechanical checks)

- 0 truly-empty catch bodies in production (repo-wide scan).
- Repo-wide silent-catch simulation with the new audit scope:
  **67 found, 67 allowed, 0 disallowed.**
- Brace-delta check across all 87 changed files: balanced.
- Every log call uses the fully-qualified
  `ProGlassAutomation.Services.Logging.AppLogService` (no new `using`
  dependencies) and `catch (System.Exception ex)` (no `using System`
  dependency), so the edits compile regardless of each file's usings.

## Follow-ups (not part of this sweep)

- The 13 remaining `Debug.WriteLine` files should be swept to
  `AppLogService` the same way (next item after CI wiring).
- Run the real test suite on a Windows/CI machine to confirm
  `SilentCatchAuditTests` passes with the extended scope.
