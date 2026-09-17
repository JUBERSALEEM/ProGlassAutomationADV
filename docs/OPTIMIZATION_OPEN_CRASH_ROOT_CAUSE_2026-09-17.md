# OPTIMIZATION MODULE — "CRASH ON OPEN, NO LOGS" ROOT-CAUSE ANALYSIS & FIX (2026-09-17)

**Status:** Fixed. Regression pinned by `Tests/Optimization/OptimizationManualCutListReentryTests.cs`.

## Symptom

Clicking **Optimization** killed the process instantly:

- no "Something went wrong" dialog,
- no `app-yyyyMMdd.log` entry for the crash,
- no `crash-*.txt` / `crash-latest.txt` report,
- Windows Event Viewer showed a fault in `System.Private.CoreLib` with no managed stack.

The previous fix attempt (commit `ee6c574`) addressed four real defects but
concluded the crash "cannot be pinned from source alone" and guessed the
trigger was VIEW RESULT. It was not — the crash happens on plain module open,
before any run exists.

## Why nothing was logged (the second complaint)

The logging system is not broken. The crash class is:

`DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`
and `TaskScheduler.UnobservedTaskException` — the three sinks wired in
`App.StartupCore.cs` — cover every **catchable** exception. The reported
signature (instant death, CoreLib frame, no managed stack) is a
**StackOverflowException**. In .NET a stack overflow terminates the process
immediately and unconditionally: the stack is exhausted, so **no managed
handler can run** — not the dispatcher sink, not the AppDomain sink, not a
`try/catch`, not a finalizer. `AppLogService.Critical` and
`CrashReportService.HandleCrash` are never reached, which is exactly why the
log folder stayed empty. This is CLR-by-design and cannot be changed
in-process; it can only be (a) prevented and (b) worked around with
breadcrumbs written *before* the risky work (see "Hardening" below).

## Root cause

`OptimizationViewModel.ManualCutList`
(`ViewModels/Optimization/OptimizationViewModel.CutList.cs`):

```csharp
private SpecGroup? _manualCutList;
public SpecGroup ManualCutList => _manualCutList ??= CreateManualCutList();

private SpecGroup CreateManualCutList()
{
    var group = new SpecGroup();
    group.PropertyChanged += (_, _) =>
        OnPropertyChanged(nameof(CutListCompositionDisplay));
    if (string.IsNullOrWhiteSpace(group.SelectedGlassType) && CutListGlassTypeOptions.Count > 0)
        group.SelectedGlassType = CutListGlassTypeOptions[0];   // ← raises PropertyChanged NOW
    return group;                                               // ← field assigned only HERE
}
```

The CUT LIST tab (`CutListTabView.xaml`) is inflated every time the module
opens, and it binds the still-being-created object from two directions at
once:

- title row: `Text="{Binding CutListCompositionDisplay}"` — and that getter
  reads `ManualCutList` (via `ResolveCutListComposition()`);
- pane controls: `SelectedItem="{Binding ManualCutList.SelectedGlassType}"`,
  `Text="{Binding ManualCutList.SpecTypedText, ...}"`,
  `DataContext="{Binding ManualCutList}"`, footer bindings, etc.

The failure chain, all synchronous on the UI thread:

1. First binding reads `ManualCutList` → backing field is null →
   `CreateManualCutList()` starts.
2. Inside it, `group.SelectedGlassType = "SGU"` fires the group's
   `PropertyChanged` — the hook subscribed one line earlier re-raises
   `CutListCompositionDisplay` on the view model.
3. The title-row binding updates: re-reads `CutListCompositionDisplay` →
   its getter reads `ManualCutList`.
4. The backing field is **still null** (`??=` assigns it only after
   `CreateManualCutList` returns — which it never gets to do), so step 1
   repeats inside step 3: a new `SpecGroup`, the same default-glass-type
   notification, the same re-read.
5. Unbounded re-entry → stack exhausted → process dies. No exception is
   ever "thrown" in a catchable sense.

Why earlier scans missed it: it is not syntactic recursion. No method calls
itself, no property pair sets each other, no XAML style/template cycles.
The cycle runs *through* the WPF binding engine into a lazy property whose
initialisation raises notifications before publishing its result. Static
recursion scanners cannot see it; it is visible only by tracing what the
view actually binds during first inflation.

Side damage of the same defect (also fixed by the change): each re-entrant
pass created another `SpecGroup`, and every `SpecGroup` subscribes the
static `SpecCatalogNotifier.Changed` event — discarded duplicates were
rooted forever (memory + duplicate reloads).

## The fix

Publish before wiring — assign the backing field **before** anything that
raises a notification:

```csharp
var group = new SpecGroup();
_manualCutList = group;                 // publish first
group.PropertyChanged += ...;           // wiring may now notify safely
group.SelectedGlassType = ...;
return group;
```

Any re-entrant read of `ManualCutList` during wiring now returns the
already-published instance (self-consistent at that point — the glass-type
field is set before its notification fires), so `CreateManualCutList` can
never nest. Exactly one `SpecGroup` is ever created.

## Hardening — "never again a silent death"

1. **Module-open breadcrumbs** (written synchronously to disk by
   `AppLogService`): `MainViewModel.Navigate` logs `Navigation → <module>`
   before constructing the module, and `OptimizationViewModel`'s constructor
   logs open/constructed markers. If anything ever kills the process during
   module open again, the **last line in `app-*.log` names the module** and
   whether the view model finished constructing.
2. **Unclean-shutdown detection** (`AppLogService.Initialize`):
   a `session-open.marker` file is written at startup and removed only by
   `AppLogService.MarkCleanShutdown()` at the very end of `App.OnExit`.
   If it still exists at the next launch, the app logs a loud Warning:
   *"Previous session ended WITHOUT a clean shutdown — signature of an
   uncatchable crash (StackOverflowException etc.)"*, with guidance to check
   the surrounding log lines and Windows Event Viewer. Catchable crashes are
   unaffected (they already produce a crash report and still exit cleanly).
3. **Regression test** without WPF:
   `Tests/Optimization/OptimizationManualCutListReentryTests.cs` attaches a
   `PropertyChanged` listener that re-reads `ManualCutList` every time
   `CutListCompositionDisplay` is raised — precisely the role of the
   title-row binding in production. Against the pre-fix code the first read
   stack-overflows the test host; against the fix it returns a single shared
   instance.

## For future diagnosis of uncatchable crashes

In-process code cannot catch a StackOverflowException — prevention and
breadcrumbs are the tools. To capture an actual memory dump of such a crash
on a user machine, enable Windows Error Reporting local dumps once
(registry, survives reboots):

```
HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\ProGlassAutomation.exe
  DumpFolder = C:\CrashDumps   (REG_EXPAND_SZ)
  DumpType   = 2               (REG_DWORD, full dump)
```

The dump opens in Visual Studio / WinDbg and shows the exact recursing
frames even when the managed stack is gone.

## Files changed

| File | Change |
|------|--------|
| `ViewModels/Optimization/OptimizationViewModel.CutList.cs` | Publish `_manualCutList` before notification-raising wiring (the crash fix) |
| `ViewModels/Optimization/OptimizationViewModel.cs` | Open/constructed breadcrumbs in the constructor |
| `ViewModels/MainViewModel.Part2.cs` | `Navigation → <module>` breadcrumb before module construction |
| `Services/Logging/AppLogService.cs` | `session-open.marker` + unclean-shutdown Warning at next startup, `MarkCleanShutdown()` |
| `App.xaml.cs` | `MarkCleanShutdown()` as the last act of `OnExit` |
| `Tests/Optimization/OptimizationManualCutListReentryTests.cs` | Regression pin for the re-entry |
