# 🔬 Deep Audit — Full Project Analysis

**Date:** 2026-08-14 (night pass, pre-build) · branch `arena/019fff44-proglassautomationadv`
**Scope:** bugs · memory leaks · style mismatches · wrong references · wrong bindings · formatting/UX defects
**Method:** static analysis scripts (no compiler available in the workspace — sandbox reaches GitHub only, no .NET SDK / NuGet route). All fixes were structural, additive and parse-verified.

---

## 1. Resource reference audit (crash class MC3000)

All `{StaticResource}` / `{DynamicResource}` references in **every XAML file** were
checked against defined keys (x:Key declarations + `ThemeManager` registrations).

**Result: ✅ ZERO unresolved references.** Nothing can crash at load for a missing brush/style.

## 2. Duplicate resource key audit (shadowing class)

- Dark/Light theme pairs defining identical keys — **by design** (theme mechanism).
- File-scoped local styles duplicated across modules (e.g. `FieldLabelStyle` ×11,
  `RateInputStyle` ×2, `MetricCardStyle` ×2, `StatCardStyle` ×2) — **anti-pattern debt**,
  not a runtime bug; consolidation is a future cleanup (each copy is local to its
  resource dictionary and cannot cross-shadow).
- **🐛 FOUND & FIXED:** `SettingsView.xaml` defined 4 local frozen brushes — including a
  **redefined `DangerBrush`** — that shadowed the theme dictionaries, so Settings stayed
  in light-mode colors regardless of active theme. Removed the local definitions; all 16
  references now use theme brushes via `DynamicResource` (re-theme live). → commit `9214db0`

## 3. Memory leak audit

| Pattern | Files | Verdict |
|---|---|---|
| `CompositionTarget.Rendering` | `SmoothScrollBehavior` | ✅ leak-resistant: WeakReference scroller registry, unload-detach, auto-unhook when idle |
| `DispatcherTimer` | 6 files | ✅ all Start/Stop balanced; `MainViewModel` clock is legitimately app-lifetime |
| Event pairing (`Loaded/Closed +=`) | all code-behind | ✅ only one `Loaded` self-subscription (short-lived dialog — dies with the window, not a leak) |
| EF `DbContext` | all repositories + AuditService | ✅ uniformly `using var db = new …` — no context leaks |
| `async void` | 9 non-handler methods | ✅ every one wrapped in try/catch (prior BUG-77 hardening) — no unobserved-exception crash path |

**No memory-leak evidence found in static analysis.** On-device confirmation (dotMemory/VS
Diagnostic Tools) recommended post-build for long sessions with module switching.

## 4. Binding audit (wrong-binding class)

- 8 views expose direct `DataContext is XViewModel` casts; their XAML binding paths were
  checked against VM members **and** collection-item/entity types.
- 74 item-level paths flagged by the first pass → **all 74 verified to exist** on entity
  model types (DataTemplate row bindings — false positives of the VM-only pass).

**Result: ✅ zero broken bindings in scanned scope.** Dotted sub-paths across all 88 views
remain subject to on-device Output-window verification (the definitive check).

## 5. User-reported defects — fixed this pass

| # | Defect | Root cause | Fix | Commit |
|---|---|---|---|---|
| 1 | Sheet price shows `25.00` | editable price boxes used `StringFormat=N2` | all 9 Sheet Store price bindings → `0.##` (25 stays 25; 25.5 shows 25.5). SQM/AED totals correctly keep N2 | `8f60e0d` |
| 2 | Select-all on click missing in some inputs | `SelectAllOnFocus` behavior is ON globally via App.xaml implicit style, but **17 local TextBox styles bypassed the implicit style** | attached setter added to every local input style (12 files; 5 gained the behaviors xmlns); multi-line notes style gets explicit `False` per convention; behavior code untouched (detach-first prevents double-hooking) | `8f60e0d` |
| 3 | Dashboard card backgrounds not dynamic | 7 dashboard views + container used hard-coded pastel/blue fills | **81 plain setters** → theme brushes (canvas `#F5F9FF` → `ModuleBackgroundBrush`, state fills → Tint*, borders → AccentLight/Warning/Danger, text → semantic). Frozen ambient gradients/blobs/shadows remain documented exceptions | `1f048ab` |

## 6. Style consistency status (solid colors remaining: 1022, all categorized)

| Area | Hex | Category |
|---|---|---|
| Sheet Store · SystemCenter · Diagnostics | **0** | ✅ fully themed |
| Dashboard (172) | frozen gradients, blobs, effect colors | documented exceptions — visual-redesign phase |
| Reports (201) | 4 Statement-Generator step files (59-color palette, 47 frozen gradients) + WizardStyles exceptions | careful per-file pass, post-build |
| ProformaInvoice (323) | incl. **paper-white print-preview surfaces that must NOT be themed** | judgment pass, post-build |
| DeliveryNote 94 · TaxInvoice 77 · JobOrder 23 · Splash 34 · About 31 · AI 13 · Settings 8 · misc 7 | module interiors | upcoming Phase 2 batches |

## 7. Deliberate exceptions (documented, not defects)

1. **Frozen Freezables** (`po:Freeze`) cannot hold `DynamicResource` — gradients, shadow
   colors and the dark tooltip in `WizardStyles.xaml` stay hex. Theme-adaptive gradients
   need Color-keyed resources + unfrozen brushes = perf tradeoff = redesign phase.
2. **StDashboardView** carries a documented historical workaround (a past build failed to
   resolve DynamicResource in that view). Left untouched until on-device verification.
3. **Paper-white invoice preview** and **brand gradients** (PI preview, GradientHeroCard)
   are intentionally theme-independent.

## 8. Recommended post-build actions

1. Rebuild → report any Error IDs (first compile of the whole V2 stack).
2. Activate CI: copy `Docs/CI/build.yml` → `.github/workflows/build.yml`.
3. VS Output window: confirm zero binding errors (definitive binding check).
4. Visual check: dashboards in Normal/Monet/Professional + dark; Settings in dark theme.
5. Verify select-all UX inside DataGrid cells (cell templates host their own TextBoxes).
6. Long-session memory check (Diagnostic Tools) with heavy module switching.
