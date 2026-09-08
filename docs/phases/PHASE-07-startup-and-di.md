# Phase 7 — Startup Resilience and DI Completeness

**Severity: Major · Estimate: 2 days · Prerequisite: green build**

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
