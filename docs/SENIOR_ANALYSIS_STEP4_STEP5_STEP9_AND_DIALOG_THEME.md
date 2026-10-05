# Senior Analysis — Step 4→5→9 Data Flow and Dialog Theming

**Date:** 2026-09-02
**Branch:** `arena/01a06070-proglassautomationadv`

---

## 0. Summary

| # | Reported | Verdict |
|---|---|---|
| 1 | "Add Sheet dialog is black, should be dynamic" | **Real bug — fixed.** 10 dialogs affected, not 1 |
| 2 | "Step 5 not reading Step 4 specs / dropdown empty" | **Real bug — fixed.** Broken only on *saved* invoices |
| 3 | "Where does Step 9 import sheet size from?" | **Working as designed** — documented below |

---

## 1. The black dialog — a misused brush, not a missing one

The dialog was theme-aware the whole time. It set:

```xml
Background="{DynamicResource ModuleBackgroundBrush}"
```

and that brush is **deliberately fully transparent** in both themes:

| Theme | Value | Alpha |
|---|---|---|
| Light | `#00FFFFFF` | **00** |
| Dark | `#00000000` | **00** |

`ModuleBackgroundBrush` is a **glass** brush. It is correct for a panel
*inside* the shell, where the window's own backdrop shows through and
produces the frosted look. A standalone `Window` has **nothing behind
it**, so alpha `00` renders the black desktop void.

That is the whole bug: not a broken `DynamicResource`, not a missing
key, not a theme that failed to load. The resource resolved perfectly
and painted nothing.

**Fix:** use `WindowBackgroundBrush` — the opaque gradient already used
by the dialogs that render correctly (`UseDialogView`,
`InquiryEditorWindow`, `SplashView`).

**Scope.** I swept every `Window` in the app rather than fixing the one
you reported. **Ten** had the same defect:

```
AddEditSheetView            BuyDialogView
DuplicateResolutionView     HistoryView
ForcePasswordChangeWindow   ResetPasswordWindow
VersionsDialog              IssueDraftsDialog
ChangesDialog               UnsavedChangesDialog
```

All fixed. Zero `Window` elements now use a transparent brush as their
window background.

### Rule worth keeping

> A glass/overlay brush (alpha < FF) is only valid on an element that
> sits **on top of** something. On a top-level `Window`, always use an
> opaque brush.

---

## 2. Step 5 spec dropdown — empty on reopened invoices

### The mechanism

`SpecGroup.LoadSpecsAsync()` starts with:

```csharp
if (string.IsNullOrWhiteSpace(_selectedGlassType)
    || Specifications == null) return;
```

`Specifications` is a settable property, **not** a constructor
dependency. Two places build a `SpecGroup`:

| Path | Sets `Specifications`? | Result |
|---|---|---|
| `DataGridViewModel.AddNewSpecGroup()` — new quote | **Yes** | Dropdown works |
| `ProformaInvoiceMapper` — **loading a saved invoice** | **No** | Dropdown **empty** |

So the symptom depended entirely on how you got to Step 5. Create a
fresh quote and the dropdown listed your Step 4 / Sheet Store specs;
reopen that same quote and it was blank.

**It failed silently.** The guard is an early `return`, not an
exception, so nothing was logged and nothing surfaced in the UI.

### Wider blast radius

The same `_specSource` collection that `LoadSpecsAsync` fills also backs
`TryAutoFillRate()`, which sets `BaseRate` and `ImportedPrice` from the
spec's `PricePerSqm`. With `Specifications` null, that lookup found
nothing either — so on a reopened invoice **the spec price stopped
auto-filling too**. That matches "Step 5 not reading Step 4 for glass
specs price".

### Fix

Assign `App.Specs` in the mapper. It is the same
`ISpecificationsRepository` singleton the DI container holds, resolved
once at startup in `App.StartupCore.cs`. The mapper is `static`, so
there is no constructor to inject into, and other files under
`Services/` already reach for `App.*` this way. In unit tests it is
null, which simply restores the previous guarded behaviour.

---

## 3. Step 9 sheet size — where it actually comes from

Nothing is broken here, but the source was undiscoverable from the UI,
which is a fair complaint. `ProfitLossStepViewModel.QuoteBasis.cs` uses
a **three-tier fallback**, and it already reports which tier it used:

| Priority | Source | Label shown |
|---|---|---|
| 1 | `SpecGroup.Outer/InnerSheetWidthMm` — the sheet chosen in Step 4 | `"chosen in Step 4"` |
| 2 | `SheetStorePricingService.GetSizesAsync(color, thickness)` — first match | `"from Sheet Store"` |
| 3 | `DefaultSheetWidthMm` = **2440 × 1830** | `"default sheet — not in Sheet Store"` |

So when Step 9 looks like it "imported" a size from nowhere, it is
tier 2 or tier 3, and the row label says which.

### Why tier 1 is missed more often than you would expect

Tier 1 requires `HasOuterSheet` / `HasInnerSheet` to be true on the
`SpecGroup`. Those are populated by `AdoptSheetSelection(specName)`,
which reads **`SpecSheetRegistry`** — and that registry is:

```csharp
public static class SpecSheetRegistry
{
    private static readonly Dictionary<string, SpecSheetSelection> Map = ...;
}
```

An **in-memory static dictionary**, written by Step 4 APPLY. It is
**not persisted**. Consequences:

- Quote your spec in Step 4, go to Step 5/9 in the same session → tier 1.
- **Restart the app and reopen the invoice → the registry is empty**, so
  `AdoptSheetSelection` finds nothing, zeroes the sheet fields, and
  Step 9 silently drops to tier 2 or 3.

The saved snapshot *does* carry `OuterSheetWidthMm` etc., and the mapper
restores them — but `SelectedSpecName` is assigned during that same
rebuild, and its setter calls `AdoptSheetSelection`, which **resets
those fields to zero** on a registry miss before the snapshot values are
used.

I have **not** changed this. It is a real design weakness — session
state driving a persisted calculation — but fixing it means changing
setter-ordering semantics in `SpecGroup`, and the "reset to zero on
miss" is a deliberate, commented decision:

> *"A miss is normal (spec from an earlier session). Reset to zero so
> Step 9 / Step 6 fall back honestly instead of inheriting the previous
> spec's sheet."*

Overriding that without a compiler to verify the ordering is exactly the
kind of change that has bitten this project before. **Recommended
follow-up:** persist `SpecSheetRegistry`, or have the mapper apply the
snapshot's sheet values *after* `SelectedSpecName` is set. Happy to do
it as its own reviewable slice.

---

## 4. Verification

- `tools/xaml_validate.py` → **CLEAN**.
- Every touched `.xaml` parses as well-formed XML.
- `WindowBackgroundBrush` confirmed present in **both** themes.
- Mapper brace/paren balance 0; BOM preserved on all 11 files.
- `App.Specs` confirmed `public static ISpecificationsRepository`,
  assigned at startup; `ISpecificationsRepository` confirmed
  DI-registered as a singleton.

No compiler in this environment — the C# change is one property
assignment against a verified static, but the build remains the source
of truth.
