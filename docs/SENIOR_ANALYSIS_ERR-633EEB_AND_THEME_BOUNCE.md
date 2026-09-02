# Senior Analysis — ERR-633EEB startup crash + splash colour bounce

**Date:** 2026-09-02
**Build:** 2.0.0 (2026.09.02) · .NET 8.0.30 · Windows 10.0.26200
**Branch:** `arena/01a06070-proglassautomationadv`
**Severity:** P0 (application unusable — fatal on startup)

---

## 1. Executive summary

Two defects were reported. They turned out to share one root cause and one
aggravating factor.

| # | Symptom | Root cause | Class of defect |
|---|---|---|---|
| A | Fatal `TargetInvocationException` at `MainWindow.InitializeComponent()` (ERR-633EEB) | `StatementModuleViewModel` had a **partially-initializing constructor overload** that DI preferred; `GeneratorVM` stayed `null` and `MainViewModel` dereferenced it | Constructor-overload / DI contract |
| B | Splash screen colours "bounce" before the shell appears | `VisualThemeEngine.ApplyModule()` ran **3–4 redundant times** during startup, each doing a full `ClearOverrides()` → rebuild of ~60 `DynamicResource` brushes while the splash was bound to them | Idempotence / redundant work |

The aggravating factor common to both: **`<vm:MainViewModel/>` declared as the
XAML `DataContext` in `MainWindow.xaml`**, which silently constructed a second,
throw-away `MainViewModel` on every window creation. That extra construction
both (A) executed the crashing code path inside BAML load where it is not
recoverable, and (B) triggered one extra full theme rebuild.

---

## 2. Defect A — ERR-633EEB, fatal startup crash

### 2.1 Evidence

```
[0] TargetInvocationException
      at MainWindow.InitializeComponent()  ← BAML object graph
── caused by ──
[1] NullReferenceException
      at StatementModuleViewModel.set_OnOpenTaxInvoiceByNumber(Action`1) : line 56
      at MainViewModel..ctor(IServiceProvider) : line 850
      at MainViewModel..ctor()                : line 533
```

Log corroboration, in order:

```
08:51:51.816  WARN  DI MainViewModel resolve failed, using parameterless ctor: Object reference not set...
08:51:51.940  WARN  MainWindow construction failed, retrying fresh: ...target of an invocation
08:51:52.048  CRIT  (ERR-633EEB) Fatal error during application startup
```

Note the ordering: the **DI** resolve failed *first* and was handled
gracefully (App.StartupCore has a try/catch). The crash came from the
**second**, XAML-created `MainViewModel` — inside `InitializeComponent()`,
where the existing retry logic could not help, because the retry just calls
`new MainWindow()` again and reproduces the identical failure.

### 2.2 Root cause

`ViewModels/Reports/StatementModuleViewModel.cs` had two constructors:

```csharp
public StatementModuleViewModel(IDialogService? dialogs)   // ← DI picks THIS
{
    _dialogs = dialogs!;                                   // ...and nothing else
}

public StatementModuleViewModel() : this(App.ServiceProvider?...)
{
    ListVM      = new StatementListViewModel(...);         // ← all the real work
    GeneratorVM = new StatementGeneratorViewModel(...);    //   lives here
    NewStatementCommand = ...; BackToListCommand = ...;
}
```

`Microsoft.Extensions.DependencyInjection` selects the **greediest constructor
whose parameters it can all satisfy**. `IDialogService` *is* registered, so the
container always chose the one-argument overload. The resulting instance had:

* `ListVM` = `null`
* `GeneratorVM` = `null`
* `NewStatementCommand` / `BackToListCommand` = `null`

`MainViewModel.Part1.cs:850` then executes:

```csharp
StatementModuleVM.OnOpenTaxInvoiceByNumber = async invoiceNumber => { ... };
```

whose setter is a **pass-through to a null field**:

```csharp
public Action<string>? OnOpenTaxInvoiceByNumber
{
    set => GeneratorVM.OnOpenTaxInvoiceByNumber = value;  // GeneratorVM == null → NRE
}
```

This is a classic *two-phase construction* trap: the "convenience" DI overload
looked harmless because the parameterless one delegated to it, but delegation
runs the **base** ctor first and the initialization **after** — so the DI entry
point skips initialization entirely.

### 2.3 Why it appeared now, not earlier

`ResolveOrNew<T>()` in `MainViewModel.Part2.cs` prefers DI and falls back to
`Activator.CreateInstance<T>()` (the parameterless ctor). Before
`StatementModuleViewModel` was registered in `ServiceRegistration.cs:204`, the
DI lookup returned `null` and the Activator path produced a *correct* object.
Registering it for DI is what flipped the code onto the broken overload — a
latent bug armed by an unrelated, well-intentioned change.

### 2.4 Fix

1. **One initialization path.** `StatementModuleViewModel` now has a single
   real constructor taking all four dependencies; the parameterless ctor
   chains into it with `null`s, and each dependency falls back
   `argument → DI → App singleton → new`. Every construction route (DI,
   Activator, XAML) now yields a fully-built object.
   Parameters are deliberately **required, not optional** — optional
   parameters plus a parameterless ctor create an ambiguous overload set.

2. **Explicit DI factory.** `ServiceRegistration` registers it via
   `AddTransient(sp => new StatementModuleViewModel(...))` so the container's
   greediest-ctor heuristic is removed from the equation permanently.

3. **Same pattern eliminated repo-wide.** An audit found five more view models
   with the identical `(IDialogService? dialogs) { _dialogs = dialogs!; }`
   shape whose real initialization lived in the parameterless ctor. All were
   restructured onto a private `(IDialogService?, bool initialize)` core ctor:

   * `AboutCrashReportsViewModel`
   * `TermsAndConditionsViewModel`
   * `ScreenRecordPanelViewModel`
   * `HistoryViewModel` (Sheet Store)
   * `SheetStoreViewModel`

   None of these are DI-registered *today*, which is exactly why they were
   dormant. Registering any of them would have reproduced ERR-633EEB in a
   different module.

4. **XAML `DataContext` removed** (see §4) so a VM fault can never again
   surface as an unrecoverable BAML `TargetInvocationException`.

---

## 3. Defect B — splash screen colour bounce

### 3.1 Observed behaviour

Between the splash appearing and the shell showing, background/surface colours
visibly flash — typically a pale/neutral frame, then the real family palette.
The log shows the smoking gun:

```
08:51:46.305  Visual theme applied: Normal      ← startup step 9d (legitimate)
08:51:51.686  Visual theme applied: Normal      ← extra
08:51:51.875  Visual theme applied: Normal      ← extra
08:51:51.987  Visual theme applied: Normal      ← extra
```

Four full theme applications; only the first is necessary. Each subsequent one
is byte-for-byte identical in output.

### 3.2 Mechanism

`VisualThemeEngine.ApplyModule()` is not a diff — it is a **teardown and
rebuild**:

```csharp
ClearOverrides(resources);   // Application.Resources.Remove(...) × ~60 keys
Current = Normalize(...);
switch (Current) { case LiquidGlass: ApplyLiquidGlass(resources); ... }
```

Every one of those keys (`WindowBackgroundBrush`, `ModuleBackgroundBrush`,
`GlassSurfaceBrush`, `TextPrimaryBrush`, …) is consumed through
**`DynamicResource`**. WPF's `DynamicResource` re-evaluates on *every*
dictionary mutation. So a single `ApplyModule()` makes each bound surface
repaint **twice**:

1. On `Remove` → falls back to the base theme dictionary value (the pale frame).
2. On the family's re-write → the correct value.

That is one flash per call. The splash (`Views/Splash/SplashView.xaml`) is
`Topmost`, `Maximized`, and binds `WindowBackgroundBrush`, `AccentBrush`,
`AccentColor`, `AccentLightColor`, `PrimaryForegroundBrush`, `OutlineBrush`
and more via `DynamicResource` — so it repaints in lock-step with every one of
those rebuilds, in full view of the user.

### 3.3 Where the redundant calls came from

| Call | Trigger | Necessary? |
|---|---|---|
| 1 | `App.StartupCore` step 9d — restore persisted family/mode/accent/wallpaper | **Yes** |
| 2 | `SettingsViewModel` ctor → `SettingsViewModel.Helpers.cs:208 ApplyVisualTheme()` — runs on *every* `SettingsViewModel` construction, and `SettingsViewModel` is built eagerly inside `MainViewModel`'s ctor | No |
| 3 | `MainViewModel` ctor → `ApplyModuleWallpaper("Dashboard")` | Debatable — but see below |
| 4 | Repeat of 2+3 for the **second** `MainViewModel` created by `<vm:MainViewModel/>` in `MainWindow.xaml` | No |

Call 3 is semantically correct (the shell must layer the Dashboard wallpaper),
but with the persisted config it produces a scene *identical* to call 1, so it
is a pure flash with no visual delta.

### 3.4 Fix — three layers

**Layer 1: idempotence guard (the real fix).**
`ApplyModule()` now fingerprints every input its output depends on and skips
identical re-renders:

```csharp
string signature = BuildSceneSignature(moduleKey, appearance);
if (signature == _lastAppliedSignature && _hasAppliedOnce) return;
```

The signature covers: colour mode (`ThemeManager.CurrentTheme`), accent
(`ThemeManager.AccentColor`), family, module key, system wallpaper path +
opacity, that module's per-module wallpaper override (enabled/path/opacity),
and the global font-colour preference. Any *real* change alters the signature
and applies normally; a duplicate is a no-op instead of a flash.

The signature is recorded in `finally`, deliberately — the module-wallpaper
branch has an early `return`, and a pass that threw has already had
`GuaranteeCriticalResources()` restore a valid scene.

**Layer 2: explicit invalidation** wherever theme state changes *outside*
`ApplyModule`, so the cache can never go stale:

* `VisualThemeEngine.Apply()` (family swap) — writes the baseline directly
* `VisualThemeEngine.OnAccentChanged()` — Monet/Liquid Glass re-derive
* `ThemeManager.ApplyTheme()` — Light/Dark swap
* `ThemeManager.ApplyAccent()` — accent change
* `ThemeStudioService.Apply()` — writes glass brushes over the family

All invalidation calls are individually try/caught and logged at Debug, so a
theme-cache concern can never break a theme apply.

**Layer 3: remove the redundant call sites.**

* `SettingsViewModel.Helpers.cs` no longer unconditionally calls
  `ApplyVisualTheme()` on construction. It now only does so if the engine
  genuinely has not run (`LastRequestedModule` empty **and** `Current ==
  Normal` **and** the persisted family is not Normal) — a real cold-start
  safety net rather than a per-construction rebuild.
* `MainWindow.xaml`'s `<vm:MainViewModel/>` is gone, eliminating an entire
  duplicate VM tree and its theme work.

Expected log after the fix: **one** `Visual theme applied` line at startup.

---

## 4. The shared aggravator — `<vm:MainViewModel/>` in XAML

```xml
<Window.DataContext>
    <vm:MainViewModel/>   <!-- removed -->
</Window.DataContext>
```

`App.StartupCore` already does:

```csharp
mainVm = ServiceProvider.GetRequiredService<MainViewModel>();
mainWindow = new MainWindow();
if (mainVm != null) mainWindow.DataContext = mainVm;
```

So the XAML instance was constructed, ran its entire ctor (DB-touching child
VMs, theme applies, timers, event subscriptions), and was then **immediately
discarded** by the assignment on the next line. Costs:

* **Correctness** — VM ctor faults become `TargetInvocationException` inside
  BAML load, bypassing the graceful DI fallback (this crash).
* **Performance** — two full VM trees per window; twice the repository
  construction, twice the `VatSubmission` load attempts (visible as the
  duplicated `VAT Submission: Data load failed gracefully` warning **pairs**
  in the log).
* **Correctness, subtle** — event subscriptions from the discarded VM
  (`ConnectivityChanged`, `CurrentUserChanged`, clock timer) are never
  detached: a static-event leak keeping a dead VM alive.

`MainWindow`'s ctor now has a defensive `DataContext ??=` fallback (DI first,
`new` second, fully wrapped in try/catch) so the design surface and any
forgetful call site still work — but startup uses the single DI instance.

---

## 5. Collateral finding (not fixed here)

```
WARN  VAT Submission: Data load failed gracefully — An exception was thrown while
      attempting to evaluate a LINQ query parameter expression...
```

This is an EF Core translation failure in `VatSubmissionViewModel`'s query
(a captured expression the SQLite provider cannot evaluate), not a startup
blocker — it is caught and returns empty results. It appeared **four** times
because of the duplicate VM trees; after this change it will appear twice, and
should be investigated separately with
`DbContextOptionsBuilder.EnableSensitiveDataLogging()` to surface the offending
parameter.

---

## 6. Regression risk & verification

| Change | Risk | Mitigation |
|---|---|---|
| `StatementModuleViewModel` ctor merge | Low | Single path; every dependency has a fallback chain; public surface unchanged |
| 5 view models restructured | Low | Behaviour identical — the initialization body was moved verbatim into a private core ctor both public ctors chain to |
| `ApplyModule` de-duplication | **Medium** | The signature must cover every input. Reviewed against the full body of `ApplyModule`; explicit invalidation added at all five external mutation points; `GuaranteeCriticalResources()` still runs on every non-skipped pass |
| `SettingsViewModel` conditional apply | Low | Startup step 9d already applies the persisted theme before the splash; the condition is a true cold-start guard |
| XAML `DataContext` removal | Low | Code fallback added in the `MainWindow` ctor |

### Manual verification checklist (Windows)

1. Cold start → app reaches the shell, **no** ERR-633EEB.
2. `Visual theme applied` appears **once** in the startup log.
3. Splash shows a single, steady palette — no flash before the shell.
4. Settings → Appearance: toggle Light/Dark → colours change **immediately**
   (proves invalidation works, i.e. the cache is not over-eager).
5. Change accent → whole UI re-tints.
6. Set a per-module wallpaper → navigate to that module and away → wallpaper
   layers and un-layers correctly.
7. Theme Studio: drag frostness/transparency → live update still applies.
8. Navigate to Reports → Statement: list loads, "New Statement" works
   (proves `StatementModuleViewModel` is fully built under DI).

---

## 7. Recommendations

1. **Ban the two-phase ctor pattern.** Add a `SilentCatchAuditTests`-style
   analyzer test asserting that no `ViewModel` declares a constructor which
   assigns only `_dialogs` while a sibling ctor performs initialization.
2. **Ban `<vm:XxxViewModel/>` in production XAML.** `DataContext` should be
   assigned in code (DI) or via `d:DataContext` for design-time only. A grep
   test over `Views/**/*.xaml` + `MainWindow.xaml` would enforce this cheaply.
3. **Make theme applies diff-based long-term.** The signature guard fixes the
   symptom; the structural fix is for `ApplyModule` to compute the target
   brush set and write only the keys whose values actually changed, removing
   the `Remove` → fallback → rewrite double-repaint entirely.
4. **Wrap `MainWindow` construction in a real recovery path.** The current
   retry (`new MainWindow()` again) cannot succeed for a deterministic fault;
   it should fall back to a minimal diagnostic shell that shows the crash ID.
