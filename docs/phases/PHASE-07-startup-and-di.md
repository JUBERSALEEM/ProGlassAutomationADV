# Phase 7 — Startup Resilience and DI Completeness

**Severity: Major · Estimate: 2 days · Prerequisite: green build**

## Status — APPLIED 2026-09-09

| | |
|---|---|
| `App.Status` / `App.Dialogs` null after DI failure | **fixed** |
| `App.ServiceProvider` unguarded dereference | **fixed** |
| Silent loss of 10 of 11 DB init failures | **fixed** |
| `Task.Delay(2800)` splash delay | **NOT changed — see below** |

### The null services

`App.StartupCore.cs` resolved fourteen services from the container inside a
`try`. The `catch` rebuilt twelve of them by hand and **omitted `Status` and
`Dialogs`**, leaving both at the `null!` they are initialised to in
`App.xaml.cs:106-107`.

The consequence is specific and bad: the container fails, startup carries on by
design, and then the first thing the app tries to do is tell the operator
something went wrong — through `App.Status` or `App.Dialogs`, which are null.
`NullReferenceException`, during startup, in the one situation where the user
most needs a message on screen.

Both are `sealed` with implicit parameterless constructors and are registered as
singletons, so `new StatusService()` / `new DialogService()` produce what the
container would have. Verified by a scripted diff of the `try` list against the
`catch` list: 14 resolved, 14 built, 0 missing.

The log line also now carries the exception **type** and `ex.ToString()`.
"DI container build failed: Value cannot be null" names nothing; the type names
the missing registration, and the inner exception is where DI failures actually
live.

### The unguarded provider

`Views/Inquiry/InquiryListView.xaml.cs:263` called
`App.ServiceProvider.GetService(...)` with no null check, then a `??` fallback
that could never run — the dereference throws first. Changed to `?.` so the
fallback the author clearly intended actually executes. A repo-wide re-scan for
`App.ServiceProvider.` without `?.` now returns nothing.

### The swallowed DB failures

Eleven `InitializeAsync()` calls ran inside one `Task.WhenAll`. When more than
one fails — a full data folder, a locked file, a disk fault hits several at once
— `Task.WhenAll` surfaces only the first. The startup log named one database and
the operator went and fixed the wrong one.

They are now held in a named array; on failure every faulted task is logged with
its database name before the original exception is rethrown. Startup still fails
the same way, and the same exception still propagates. Reading `.Exception` also
marks each task observed, so none resurfaces as `UnobservedTaskException`.

### What was deliberately left alone

`await Task.Delay(2800)` at `App.StartupCore.cs:655` is 2.8 seconds of
unconditional startup latency on every launch, fast machine or slow. It is a
real cost and it is worth removing — but not blind.

The surrounding comments describe two races the delay is currently absorbing:
the licence-overlay subscription ("a fast machine can race the subscription
setup and the overlay stays hidden for a frame") and the splash/login z-order
fight ("splash is Topmost, login is not, Activate() fights z-order and Windows
flashes the desktop between them"). Shortening the delay without a build and a
screen to watch is a good way to ship a startup that flashes.

The correct replacement is to wait on an actual readiness signal rather than on
a wall clock, and to fix the two races so nothing depends on the wait. That is
UI work needing a running app. Recorded here rather than guessed at.

Also worth noting for Phase 14: `AppLogService` has **no Error level** — only
`Debug`, `Info` and `Warning`. A container build failure is logged as a warning
because there is nothing louder to log it as.


## Findings

### 7.1 — The DI fallback leaves two services null

`App.StartupCore.cs` builds the container in a `try` and, on failure, constructs
twelve services by hand. Two are absent from the fallback:

```csharp
public static Services.Workflow.IStatusService Status  { get; private set; } = null!;
public static Services.Dialogs.IDialogService Dialogs  { get; private set; } = null!;
```

Neither is assigned in the `catch`. If the container fails to build, both are
`null` and every consumer throws — at some arbitrary later point, far from the
cause.

**Fix:** assign both in the fallback, or make the fallback throw. A half-working
composition root is worse than a loud failure, because the shell appears to
start.

### 7.2 — A composition-root failure is logged as a Warning

```csharp
AppLogService.Warning($"DI container build failed: {ex.Message}");
```

If this fires, every binding in the shell is about to degrade. The code
elsewhere already understands this — the `MainViewModel` resolve failure is
logged as `Error` with a comment explaining that the message alone hid the real
cause during a dead-shell incident. The container build deserves the same
treatment, including the full exception with inner.

### 7.3 — A fixed 2.8-second delay on every launch

`App.StartupCore.cs:636`: `await Task.Delay(2800);` so splash animations can
play. An operator relaunching through the day pays for a fade every time.

**Fix:** gate the delay on whether the splash animation is actually still
running, or reduce it and let the fade overlap the shell handoff — the code
below already waits for real window readiness via `WaitForWindowReadyAsync` and
races the splash `Closed` event against a 1.5s fallback, so the fixed delay is
doing work the robust path already handles.

### 7.4 — Eleven databases initialise in parallel with one shared failure mode

```csharp
await Task.WhenAll(
    Specs.InitializeAsync(), Inquiries.InitializeAsync(), ...
    AuditService.InitializeAsync(), AiMemoryService.InitializeAsync());
```

Eleven concurrent SQLite `EnsureCreated` calls. `Task.WhenAll` surfaces only the
first exception; the rest are dropped. On a machine with a slow or locked data
folder, the operator sees one database's error and no indication that others
also failed.

**Fix:** await each and collect failures, then report which databases failed to
initialise. The app can often continue with a subset — but it should say which
subset.

### 7.5 — Orphaned XML documentation

`App.xaml.cs` contains a dangling `/// </summary>`. `App.StartupCore.cs` has two
consecutive `<summary>` blocks before `ShowLanguageOfferIfPendingAsync`, leaving
`BringMainWindowToForegroundAsync` with no documentation. Harmless while
`GenerateDocumentationFile` is off; it becomes CS1587 the moment documentation
generation is enabled — and this project has `TreatWarningsAsErrors=true`.

## Acceptance criteria

- [ ] The DI fallback assigns every `App.*` static, or does not exist.
- [ ] Container build failure is an `Error` with the full exception chain.
- [ ] Startup does not pay a fixed delay it does not need.
- [ ] Parallel database initialisation reports every failure, not the first.
- [ ] No orphaned XML doc comments.

## Must not change

- `VelopackApp.Build().Run()` as the first line of `Main()`. The comment
  explaining why is correct and the audit warning it suppresses is real.
- `ShutdownMode = OnExplicitShutdown` before the first window, restored after.
  Removing it reintroduces the process dying during the splash or sign-in.
- The four exception sinks and `ForceExitAfterCrash`.
- `AppDataRoot.ConfigureFrom(Config)` running **before** any DbContext opens.
  The comment says why; moving it breaks shared-folder installs.
