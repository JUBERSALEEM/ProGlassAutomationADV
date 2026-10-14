# Sticker Printer Studio — UX and Engineering Phased Plan

## Goal

Provide a printer-ready glass-sticker workflow from both Proforma Invoice Step 5 and Optimization, while also making the reusable template studio a first-class main-shell module. Operators should be able to design and save layouts without source rows, then print real pane data through the existing workflow entry points.

A printed label preserves order/PI/job identity and shows the actual pane, dimensions, quantity, reference and customer with the source company's branding. Template files store layout and field keys only; printer preferences remain workstation-local.

## Source-of-truth decisions

| Sticker value | Authoritative source | Handling |
|---|---|---|
| Company name, address, logo, email | Step 1's company snapshot / saved company profile | Use the invoice's snapshot for PI and Job Order-origin labels; never substitute a different saved company. |
| Company telephone and website | Saved company profile and immutable invoice snapshot | Company telephone is a separate field from representative mobile. Never substitute mobile for the company telephone. |
| Representative mobile | Saved company profile / invoice snapshot | Optional, separately addressable contact field. |
| Order / PI / job | PI Step 1 identity and/or immutable Job Order source snapshot | Show the Job Order number when present and retain the PI number; Step 5 before JO creation uses its PI ID. |
| Customer company | Step 2 client snapshot / Job Order's embedded PI | Per-job, not a global customer value. |
| Glass, color and pane position | Step 5 spec group and `ProductionDistributionEngine` pane occurrence | Use the same SGU/DGU/LAM/DGU+LAM routing the production floor uses; do not infer a second pane from dimensions alone. |
| Width, height and quantity | Step 5 rows normalized by `ProductionDistributionEngine`; Optimization's imported pane rows | Keep millimetres and source quantity. Default to one sticker per physical glass piece; alternate one-per-row mode prints aggregate quantity. |
| Reference | Project reference, contract/LPO and customer reference from the invoice snapshot | Keep supplied identifiers visible; do not invent a reference. |

The sample's `0.41` is not mapped: its meaning is not established by the current data model or image. No label field implies that it is area, thickness, weight or another metric.

## Architecture

1. **Profile/data layer** — additive telephone and website fields flow through company master data and the invoice snapshot. Existing databases deserialize missing values as blank; schema additions use defaults.
2. **Canonical label mapping** — Step 5 and Optimization pane rows map to the same sticker model without changing production or cutting data.
3. **Reusable editor** — `StickerPrinterView` is hosted directly by the main shell with a design-only `StickerPrinterViewModel`; `StickerPrintWindow` is a thin modal host around the same view for source-row printing. This keeps template behavior and preview rendering consistent.
4. **Local persistence** — named `.pgsticker` templates and printer preferences live under the current Windows user's local application data. Exported templates contain layout metadata and field keys, not invoice or customer values.
5. **Print path** — `StickerPrintEngine` uses WPF `PrintDialog` with two distinct output contracts, not the A4/A3 business-document PDF layout: one custom-media page per sticker for roll/thermal printing, or a true-size A4 multi-up sheet for standard printers. The sheet paginator uses the selected queue's reported imageable area, configured inset/gap, row-major source order, and a print-time layout confirmation. The engine repeats the batch preflight independently of dialog validation.

## Phases and status

### Phase 1 — printer-ready core — implemented; Windows acceptance remains pending

- Store company telephone and website in the company profile and immutable invoice snapshot.
- Map Step 5 and Optimization panes to sticker records without altering cutting/optimization data.
- Provide live preview, source-row review, editable label width/length, margin/font controls, field visibility, one-label-per-piece mode, local preferences and custom-media printing.
- Add entry points beside Step 5's optimization controls and on Optimization's report/export surface.
- Preserve the ZT411 profile and its media-width, print-width, resolution and media-handling limits.

### Phase 2 — reusable Sticker Printer Studio — implemented in source; Windows acceptance remains pending

- Split the studio into Design, Templates, Printer Setup and Source Rows tabs using the shared Bento visual language.
- Add a first-class `Sticker Printer` main-shell/sidebar route; keep Step 5 and Optimization print entry points modal through `StickerPrintWindow`.
- Keep design-only preview data clearly marked and printing unavailable from the standalone studio.
- Support SGU, DGU, Proforma/Job and custom layouts; add, select, drag, resize, nudge, duplicate, reorder, lock and delete fields. Delete works from the preview or layer list without hijacking Delete inside inspector text fields.
- Add quick-add controls for common fields, including company telephone and outer/inner pane side.
- Support editing, local library save, `.pgsticker` open/import and export. Same-name, different-ID templates receive a numbered filename instead of overwriting a library entry.
- Add print-engine preflight for the 50,000-label ceiling and ignore invalid/non-finite source dimensions before a dialog or paginator is created.
- Confirm unsaved studio edits on modal close and on application shutdown; saving also attempts to persist the last-used setup.

### Phase 3 — production hardening and optional outputs — partially implemented; Windows acceptance remains pending

- Implement A4 multi-up output for standard printers with automatic portrait/landscape capacity selection, adjustable sheet inset and inter-label gap, exact-size labels, imageable-area clipping, an A4 sheet preview, and an exact print-time sheet/page confirmation. `PrinterDefault` resolves to A4 sheets for the Custom/other profile and preserves one custom-media label per page for the Zebra ZT411; either mode can be explicitly selected where supported.
- Build and run the WPF test suite on Windows; complete acceptance with the real printer driver and representative label stock.
- Validate custom media, A4 media selection, orientation, scaling, printer DPI, peel/rewind/cutter behavior, long labels and large batches on target hardware.
- Add die-cut/Avery-style sheet-stock profiles with configurable pitch and cut guides only when the target stock and registration tolerances are defined.
- Consider per-company branding presets, validated barcode/QR fields, configurable reprints, custom-page PDF output and print-job audit/reprint history if operators require them.
- Localize studio strings after the workflow and copy stabilize.

## UX / technical review notes

- The main-shell workspace and modal printing path use the same view, avoiding a separate editor/preview implementation that could drift.
- The studio keeps the live preview visible while its left-side cards change with the selected tab. A selected-element inspector is hidden until an object is selected; keyboard focus is restored to the preview after canvas refreshes. The modal wrapper clamps its size to the desktop work area so bottom actions stay reachable on smaller displays.
- The Optimization Sizes toolbar now wraps instead of clipping its fixed-width controls at narrow content widths. The 2D sheet view's Left/Right Arrow tooltip is backed by a real single-sheet key handler; its schedule retains horizontal scrolling for the full-width matrix.
- Template name collisions, invalid/non-finite batch dimensions and missing print-engine caps are handled at the persistence/engine boundary, not only in the UI.
- Root cause of the reported 12-page output: the original paginator treated one sticker as one document page and the print engine requested the sticker's own dimensions as page media. A normal A4 printer therefore received twelve individual custom-size pages instead of a sheet layout.
- The standard-printer sheet path never shrinks a label to fill paper: it packs labels at their designed millimetre dimensions, centres a row-major grid inside the overlap of the requested inset and the driver-reported imageable region, and paginates overflow onto further A4 sheets. If the driver cannot report imageable bounds, it uses a conservative 10 mm fallback and discloses that in the confirmation. Thermal/roll output remains one physical label per custom page.

## Acceptance checks

1. The main-shell sidebar opens the Sticker Printer Studio as an in-shell module, not a popup.
2. Step 5 and Optimization still invoke the modal print wrapper with their real source rows.
3. SGU, DGU and DGU+LAM rows produce only the panes defined by `ProductionDistributionEngine`, with outer/inner/laminated positions traceable to the source.
4. A quantity `N` row produces `N` labels by default, each displaying quantity `1`; one-per-source-row mode produces one label displaying quantity `N`.
5. Preview and source rows agree on glass, W×H, order/PI, customer, reference and repeat count.
6. Company telephone resolves only from company telephone; representative mobile remains separate.
7. Editing, dragging, resizing and Delete-key removal operate on the selected object; no preview-only action sends a print job.
8. Same-name template saves do not overwrite a different template ID; exported/imported `.pgsticker` files round-trip layout without customer data.
9. A batch above 50,000 labels is rejected before `PrintDialog`; invalid or non-finite source dimensions are excluded.
10. Changing label width/height changes the preview aspect and requested media size; saved preferences reload on the next launch.
11. Twelve small stickers whose dimensions fit the A4 grid occupy one sheet; larger batches paginate in source order. Print confirmation reports the driver-specific rows, columns, orientation and page count; sticker dimensions remain unscaled, and the ZT411 path remains one label per custom-media page.

## Verification boundary

This repository targets WPF `net8.0-windows`. The current environment is Linux and has no .NET SDK, so no WPF build/test run, Windows UI interaction, printer-driver acceptance or physical-print verification is claimed here. Source-level XML, reference, C# structure and diff checks can be run in this environment; the remaining Windows and printer checks belong to the Phase 3 handoff.
