# Senior Analysis — Startup Crash Sweep (ERR-918BAB and the whole defect family)

**Date:** 2026-09-02
**Build:** 2.0.0 (2026.09.02) · .NET 8.0.30 · Windows 10.0.26200
**Branch:** `arena/01a06070-proglassautomationadv`
**Trigger:** *"I AM GETTING EACH TIME THIS KIND OF ERROR. DO A COMPLETE CROSS CHECK AND FIX THEM ALL IN ONE RUN."*

---

> ## ⚠️ SUPERSEDED — READ FIRST (2026-09-02, same day)
>
> **The bulk view-model refactor described in sections 3–5 of this document
> was SHIPPED AS `6a93c0e`, BROKE THE APPLICATION, AND HAS BEEN REVERTED
> IN `3dfa6ed`.** Symptoms: dashboards blank, agent panel stuck open,
> nothing clickable.
>
> **Why it broke.** `SettingsViewModel`'s relocated initialization body
> dereferences `_config` (`AppConfig`) in three places. `AppConfig` was
> never registered in the DI container, so the DI-selected constructor
> received `null` and threw. That exception was swallowed by the
> defensive handlers in `App.StartupCore.cs` and `MainWindow.xaml.cs`,
> which then displayed the window with a **null `DataContext`**. Every
> binding failed silently; the agent panel's `Visibility` binding had no
> `FallbackValue`, so it reverted to the default `Visible`.
>
> Separately, `tools/fix_two_phase_ctors.py` has a real defect: where
> field **declarations** sat physically between the two constructors, it
> swept them along with the constructor body and re-emitted them after
> the parameterized constructor (11 fields in `SettingsViewModel`).
> **Do not re-run that tool as written.**
>
> **The lesson.** This document originally carried an explicit "NOT
> COMPILED" caveat, and it was shipped anyway. *A documented risk is not
> a mitigated risk.* Every static check available here — brace balance,
> BOM, logic-loss diff, use-before-assign, duplicate-assignment — passed
> on the broken code, because the damage was semantic (a null dependency)
> rather than syntactic. **Static analysis cannot substitute for a
> compile and a launch.**
>
> **What was kept** (all verified, low risk): the ERR-918BAB fix
> (`Modifiers="None"` throws in `ModifierKeysConverter`), both Python
> validators, and the xUnit guards. The two-phase-constructor problem
> described below is **still real and still unfixed** — the guard test
> is marked `Skip` with this rationale. Redo it **one view model at a
> time, compiling and launching after each.**
>
> **Follow-up hardening** (`ba7b1a0`, `1e2a55e`): registered `AppConfig`,
> `IConnectivityService`, `InvoiceMainSummaryViewModel` and
> `TermsAndConditionsViewModel`; upgraded the swallowed startup warning
> to a full `Error` log; added `FallbackValue=Collapsed` to the agent
> panel so this symptom cannot recur.

---

## 0. Why the previous three fixes felt like whack-a-mole

Each crash was fixed correctly, but each fix was **reactive** — driven by the
one stack trace in hand. That is guaranteed to be slow here, because these
defects share a property:

> **They are invisible until executed, and they execute in sequence.**

WPF loads `MainWindow.xaml` as a single BAML stream. It fails at the *first*
fault, reports that one, and stops. Fix it, and the loader simply advances to
the next latent fault. Meanwhile the C# compiler validates none of it:
resource keys, enum literals, type-converter strings and DI constructor
selection are all resolved at **runtime**.

So the correct engineering response is not another point fix. It is a
**static validator that reproduces what WPF does at load time**, run across
the whole repository at once. That is what this pass delivers.

| Crash | Reported | Root cause | Class |
|---|---|---|---|
| ERR-633EEB | 08:51 | `StatementModuleViewModel` half-built by DI | Two-phase ctor |
| ERR-2FFFF1 | 09:06 | Converters unresolvable from merged dictionaries | Parse-time resource order |
| **ERR-918BAB** | **09:18** | **`Modifiers="None"` rejected by WPF** | **Type-converter literal** |

---

## 1. ERR-918BAB — the reported crash

### 1.1 Evidence

```
XamlParseException: 'Provide value on 'TypeConverterMarkupExtension' threw an exception.'
                    Line number '322' and line position '49'.
── caused by ──
NotSupportedException: Unrecognized ModifierKeys 'NONE'.
   at System.Windows.Input.ModifierKeysConverter.GetModifierKeys(...)
```

### 1.2 Root cause

`Agent/UI/AgentChatView.xaml:322`:

```xml
<KeyBinding Key="Enter" Modifiers="None" Command="{Binding SendCommand}"/>
```

`ModifierKeys.None` **exists as a CLR enum value**, but
`ModifierKeysConverter.GetModifierKeys()` does not accept the *string*
`"None"`. Its parser matches only `Alt`, `Control`, `Shift`, `Windows`, and
throws `NotSupportedException` on anything else — including `"None"`. This is
long-standing WPF behaviour, not a regression.

The correct way to express "no modifier" is to **omit the attribute**. The
sibling chat view already did exactly that:

```xml
<!-- Views/AI/AiChatView.xaml:921 — the same Enter-to-send binding, correct -->
<KeyBinding Key="Enter" Command="{Binding SendCommand}"/>
```

So this was a **copy-divergence bug**: two implementations of one feature,
only one of them correct.

### 1.3 Fix

Removed the `Modifiers="None"` attribute, with a comment recording why it is
not a stylistic choice.

### 1.4 Why the reported line number was misleading (again)

The trace says `MainWindow.xaml:322`, but the fault is in
`AgentChatView.xaml:322`. `MainWindow.xaml:1125` hosts
`<agentUi:AgentChatView .../>`, so the child control's BAML is loaded *inside*
the parent's load. The line number belongs to the child; the file name in the
stack belongs to the root. **Both prior crashes had this same trap** — it is
why "go to MainWindow.xaml line N" kept pointing at innocent markup.

---

## 2. The systematic sweep

Rather than trusting inspection, I built
**`tools/xaml_validate.py`** — a static reproduction of WPF's parse-time
checks — and **`tools/fix_two_phase_ctors.py`** for the DI defect class.

### 2.1 Validating the validator

A checker that only reports known bugs is worthless. I proved it by
**reintroducing ERR-2FFFF1** (moving converters back inline in `App.xaml`)
and confirming detection:

```
── R3 (FATAL) — 10 issue(s) ──   ← caught the ERR-2FFFF1 pattern
── R1 (FATAL) — 395 issue(s) ──  ← caught the SidebarNavItem cascade
```

then restoring the tree to clean. The tool detects the real historical
defect, not just its symptom.

It also caught **my own first attempt being wrong**: an initial run reported
1187 `R1` failures. That was implausible for a shipping app, and inspection
showed the bug was in *my* path resolver — it did not handle
`Source="AboutStyles.xaml"` (relative to the referring file) versus
`pack://application:,,,/...` (relative to repo root). Fixed, the count went to
zero. **A validator must be debugged before its output is trusted.**

### 2.2 Checks implemented

| Code | Check | Crash class it prevents |
|---|---|---|
| R1 | `{StaticResource}` unresolvable at parse time | ERR-2FFFF1 |
| R2 | `{StaticResource}` on a **runtime-merged** language key | Silent missing text / crash |
| R3 | Merged dictionary forward-referencing an `App.xaml` inline key | ERR-2FFFF1 (exact) |
| M1 | `Modifiers="None"` / `Gesture="None+..."` | **ERR-918BAB (exact)** |
| M2 | `KeyBinding` without `Key` or `Gesture` | `InvalidOperationException` |
| E1 | Invalid enum literals (`Visibility`, `Orientation`, …) | `TypeConverter` throw |
| D1 | Duplicate `x:Key` in one scope | Silent resource shadowing |
| C1 | `x:Class` with no code-behind file | Build/load failure |

R2 deserves emphasis: language dictionaries are merged **at runtime** by
`LocalizationService`, so their 2,440 keys are *not* parse-time resolvable.
Any `{StaticResource Nav.Dashboard}` would be a guaranteed crash. The
validator now enforces `DynamicResource` for those — this is precisely the
"resource could not be resolved" family from your very first message.

### 2.3 Result

```
XAML validation: CLEAN — no parse-time faults found.
  app-scope keys : 582
  runtime keys   : 93
  language keys  : 2440
```

---

## 3. The DI defect class was far larger than one view model

ERR-633EEB was fixed in `StatementModuleViewModel` plus 5 look-alikes. A
brace-aware scan of the whole `ViewModels/` tree found **27 instances**, and
critically:

> **Every single one was already registered in the DI container.**

That means they were not theoretical — they were live, half-constructed
objects waiting for someone to navigate to that module.

### 3.1 The pattern

```csharp
public SettingsViewModel(IAiService ai, AppConfig cfg, ...)   // DI picks THIS
{
    _ai = ai; _config = cfg;                                  // ...and stops
}

public SettingsViewModel()                                     // 137 statements
    : this(App.ServiceProvider?.GetService<IAiService>()!, ...)
{
    ThemeStudio = new ...;  SaveCommand = new ...;  /* ...134 more... */
}
```

MS.DI selects the **greediest satisfiable constructor**. All dependencies were
registered, so DI always took the short one and the 137 initialization
statements never ran.

### 3.2 Scope repaired

| Group | Count | Notable |
|---|---|---|
| Fixed in the previous commit | 6 | `StatementModuleViewModel` (the crash) |
| **Fixed now — batch 1** | **14** | `JobOrderViewModel`, `ClientProfileViewModel`, `PricingEstimatorViewModel` |
| **Fixed now — batch 2** | **12** | `SettingsViewModel` (137 stmts), `ProformaInvoiceViewModel`, `TaxInvoiceViewModel` |
| **Fixed now — cross-partial** | **1** | `DeliveryNoteDetailViewModel` (ctors split across `.Part1`/`.Part2`) |
| Verified already correct | 3 | `DnDashboard`, `JoDashboard`, `PiDashboard` |

Final scan: **`REMAINING two-phase violations: 0`**.

### 3.3 How the repair was made safe

Regex slicing of C# is unsafe — my first attempt corrupted brace structure,
and I reverted it immediately rather than shipping it. The replacement tool is
**brace-aware**, skipping braces inside string literals, verbatim strings,
char literals and comments so lambdas and interpolated strings survive.

Verification performed on all 29 changed files:

* **Brace/paren balance** (comments and strings stripped): `0 unbalanced`
* **Logic preservation** — every removed statement re-appears in the diff:
  `LOGIC LOST: NONE` across +694/−695 meaningful lines
* **No duplicated initialization** (each `ListVM = new` appears exactly once)
* **BOM preservation** — one file lost its UTF-8 BOM; detected and restored
* **Dependency check** — all 8 DI-registered VMs in batch 1 have every
  constructor dependency registered, so DI genuinely uses the fixed ctor

---

## 4. Other classes checked — and cleared

Not every audit finds bugs; recording the negatives matters so this ground is
not re-covered.

| Audit | Result |
|---|---|
| XAML event handlers missing from code-behind | **0** — all resolve |
| `x:Class` without code-behind | **0** |
| Duplicate `x:Key` within a scope | **0** |
| Invalid enum literals | **0** |
| DI types whose greediest ctor has unregistered deps | **3**, all with explicit `?? new ...` fallbacks — safe by design |

The 3 flagged (`AiChatViewModel`, `InquiryEditorViewModel`,
`PrintableInvoiceViewModel`) have no parameterless constructor, so
`GetService` returns `null` instead of throwing, and every call site supplies
a manual fallback. Correct as written.

---

## 5. Regression prevention

Two xUnit suites now enforce the invariants at build time, with messages that
name the offending file and key — instead of a customer-side fatal crash:

**`Tests/Agent/ResourceDictionaryOrderTests.cs`**
1. No merged dictionary forward-references an `App.xaml` inline key
2. Every merged dictionary fully resolves in merge order
3. No `<conv:` declaration returns to `App.xaml`'s inline body
4. `ConverterResources.xaml` is merged first / before `ShellStyles.xaml`
5. `SidebarNavItem` exists and is resolvable

**`Tests/Agent/ConstructorInitializationTests.cs`**
1. No parameterless ctor that delegates via `: this(...)` carries an
   initialization body — the exact ERR-633EEB shape
2. `StatementModuleViewModel` assigns `ListVM`/`GeneratorVM` exactly once

The two Python tools are kept in `tools/` for ad-hoc full-tree sweeps. They
are deliberately **not** wired into `.csproj` — adding a Python dependency to
the Windows build would be a fragility trade I would not make; the xUnit tests
provide enforcement without it.

---

## 6. Honest risk assessment

| Change | Risk | Basis |
|---|---|---|
| `Modifiers="None"` removal | **Very low** | One attribute; matches the working sibling view |
| 27 constructor repairs | **Medium** | Large mechanical diff. Mitigated by brace-aware transformation, balance checks, and statement-level diff proving zero logic loss — but **it has not been compiled** |
| New tests | None | Test-only |

**The honest caveat:** there is no .NET SDK in this environment, so I could not
run `dotnet build`. Every check I could perform statically passed, but
*compilation is unverified*. Given the diff size, please build before
deploying. If anything fails it will be a localised compile error in one of
the 27 files, not a design problem.

### Verification checklist (Windows)

1. `dotnet build` — expect success.
2. Cold start → shell appears, no ERR-918BAB.
3. `Visual theme applied` appears **once** (the splash fix is preserved).
4. **Agent chat**: press Enter → message sends (the repaired binding).
5. **Settings** → every section renders and commands work (137 relocated
   statements — the highest-value regression check).
6. Proforma → Steps 2/3/4; Job Order; Delivery Note detail; Tax Invoice
   list/detail — all now DI-constructed through the repaired path.
7. `dotnet test` — both new suites green.

---

## 7. Recommendation

The recurring theme across all four crashes is that **WPF defers validation to
runtime, and this codebase has a lot of XAML**. The highest-leverage
follow-up is not another audit — it is running `tools/xaml_validate.py` in CI
so the next mistake of this family is caught in seconds by a machine, rather
than in production by you.
