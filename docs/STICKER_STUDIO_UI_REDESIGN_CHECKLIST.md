# Sticker Printer Studio UI Redesign — Baseline and Acceptance Checklist

This checklist applies to the shared `StickerPrinterView` hosted in the main shell and in `StickerPrintWindow`. It is paired with the existing [Sticker Printing Engine Phased Plan](STICKER_PRINTING_ENGINE_PHASED_PLAN.md), which remains the source of truth for label data, print-engine, and printer-capability behavior.

## Phase 0 — behavior baseline

> **Baseline timing note:** This checklist was reconstructed from the pre-redesign revision (`9e3755c`) and the existing view-model, service, print-engine, and test code after UI implementation had already begun. It is a backfilled behavior lock, not a pre-styling sign-off. No production behavior was intentionally changed by the visual redesign.

### Entry paths and modes

- [ ] The main-shell `StickerPrinterViewModel` opens the reusable studio in design-only mode, with an empty production `Items` collection and a synthetic sample created for the selected template kind.
- [ ] Production sticker printing from Proforma Invoice Step 5 and Optimization continues through `StickerPrintDialogService` and the modal `StickerPrintWindow`; invalid/non-finite dimensions and non-positive quantities are filtered before the window opens.
- [ ] Both paths host the same `StickerPrinterView`; mode-specific visibility and copy must not change which data reaches printing.
- [ ] Closing the modal with dirty template edits continues to prompt Save / Discard / Cancel.

### Behavior that must remain unchanged

- [ ] Design-only preview values are synthetic and clearly marked. `PrintSample_Click` refuses to run outside design-only mode or if production source rows exist; the test job contains one copied sample row with printed quantity one.
- [ ] Production rows remain in source order. The source grid is read-only; selecting a row updates the shared selected label and preview. Previous/Next follow that same order.
- [ ] Built-in SGU, DGU, Proforma/Job, custom, last-used, and saved-library templates remain selectable. Unsaved edits are confirmed before switching.
- [ ] **Save layout** writes the active layout to the local template library. **Save setup** persists workstation printer preferences and the last-used layout. Neither action is a substitute for the other.
- [ ] `.pgsticker` open/import and export continue to use template validation. Files contain layout and field keys, not invoice/customer records. Same-name templates with different IDs do not overwrite one another.
- [ ] Printer controls retain Custom/ZT411 profile, output layout, resolution, media handling, label width/length, inset, text size, and one-label-per-piece/one-label-per-row behavior. The 4 × 6 in preset remains manually editable.
- [ ] ZT411/A4 defaults, printer limits, dimension validation, A4 inset/gap, imageable-area planning, source-order pagination, batch preflight, and driver confirmation remain owned by the existing view-model and print engine.
- [ ] Print and sample-print action availability stays mode-specific. No preview-only interaction sends a print job.

## Phases 1–5 — UI implementation coverage

- [x] **1 — Visual system:** sticker-scoped styles use existing ProGlass semantic resources and typography; no global theme, target framework, signing, publishing, or CI settings are changed.
- [x] **2 — Shared frame:** concise studio/layout/saved-state header; four scannable tabs; persistent preview; anchored status/actions; existing default/minimum window dimensions retained and modal sizing still clamps to the desktop work area.
- [x] **3 — Design and Preview:** add-fields/objects, layers, and selected-object controls are grouped; units and editor guidance are explicit; sample and A4 preview states remain available; only Design exposes editor interactions.
- [x] **4 — Templates, Printer Setup, and Source Rows:** template library actions, printer options/limits, read-only source grid, row order, and selected-row preview are retained and presented in focused sections.
- [x] **5 — Modes, actions, and feedback:** sample versus production is explicit; Print Sample versus Print Stickers is mode-specific; Save Layout and Save Setup are distinct; sample, label-size, template-name/background, inset, text-size, and A4-layout feedback is surfaced near the relevant controls.

## Phase 6 — regression and release checks

Source checks are not substitutes for interactive Windows acceptance. Mark runtime checks only after they are performed on Windows.

| Check | Source-level status | Windows runtime status |
|---|---|---|
| Default 1240 × 720 and minimum 800 × 600 modal dimensions; work-area clamp | Verified in XAML/code | Pending |
| High-DPI scaling and layout rounding | `UseLayoutRounding`/device-pixel settings inspected | Pending |
| Keyboard/focus order, tab switching, editor nudge/delete, drag/resize/cancel | Handlers, bindings, and scopes inspected | Pending |
| Long layout names and long source values | 100-character template limit, trimming, and cell tooltips inspected | Pending |
| Empty and populated source rows; selected-row preview | Mode/visibility/binding paths inspected | Pending |
| A4 preview and page navigation | Planner/paginator code and existing tests inspected | Pending |
| Built-in/saved templates and printer-profile combinations | View-model code and existing tests inspected | Pending |
| WPF Windows build and test suite | Not run: .NET SDK is not installed in this Linux sandbox | Pending |
| Print-dialog preview and representative hardware/media smoke test | Not available in this sandbox | Pending |

## Phase 6 — checks completed in this sandbox

- XAML parses as XML; 37 current `x:Name` values are unique, all 34 names from the pre-redesign view remain, and all 25 original event handlers remain.
- All 84 pre-redesign binding expressions remain; all 39 `StaticResource` and 26 `DynamicResource` keys resolve against the local/application dictionaries.
- All XAML event-handler names exist in the code-behind. Edited C# files pass a lightweight delimiter scan, and `git show --check` reports no whitespace errors.
- Source-level behavior checks confirmed mode-specific print buttons, rejection of sample printing when production rows exist, single synthetic sample-row printing, production printing from the source collection, read-only source rows, the A4 preview route, and the existing modal size clamp.
- Existing sticker tests cover source-row selection/navigation, sample isolation, A4 layout/order, template presets and printer options. A new test covers inline template/printer validation. These tests are present but were **not executed**.
- The local environment has no `dotnet`, MSBuild, or Mono; the project targets WPF `net8.0-windows`.

**Release boundary:** Phase 6 is still pending Windows build/tests, interactive checks at minimum/default sizes and multiple DPI scales, keyboard/focus and populated/empty-row review, Windows print-dialog preview, and representative hardware/media smoke testing. The repository's existing printing-engine plan also leaves Windows and physical-printer acceptance pending.
