# Glass Sticker Printing Engine — Analysis and Phased Plan

## Goal

Add a printer-ready glass-sticker workflow to the Optimization module and Proforma Invoice Step 5. A printed label should preserve the source order/PI/job identity and show the actual glass pane, dimensions, quantity, reference and customer, with the selected company's branding. Operators need a live preview and control over label stock width/height before sending the batch to a Windows printer.

## Findings and source-of-truth decisions

| Sticker value | Authoritative source | Handling |
|---|---|---|
| Company name, address, logo, email | Step 1's company snapshot / saved company profile | Use the invoice's snapshot for PI and Job Order-origin labels; never substitute a different saved company. |
| Company telephone and website | Not represented in the existing company profile at task start | Add additive profile fields to Company Center, Step 1, the saved-company table and PI snapshot. Representative Mobile remains a distinct optional contact and is never substituted for company telephone. |
| Order / PI / job | PI Step 1 identity and/or the immutable Job Order source snapshot | Show the Job Order number when present and retain the PI number; Step 5 before JO creation uses its PI ID. |
| Customer company | Step 2 client snapshot / Job Order's embedded PI | Per-job, not a global customer value. |
| Glass, color and pane position | Step 5 spec group and `ProductionDistributionEngine` pane occurrence | Use the same SGU/DGU/LAM/DGU+LAM routing the production floor uses; do not infer a second pane from dimensions alone. |
| Width, height and quantity | Step 5 rows as normalized by `ProductionDistributionEngine`; Optimization's imported pane rows | Keep millimetres and source quantity. Default to one sticker per physical glass piece; an alternate one-per-row mode prints the aggregate quantity. |
| Reference | Project reference, contract/LPO and customer reference from the invoice snapshot | Keep supplied identifiers visible in the reference field; do not invent a reference. |

The sample's `0.41` is not mapped: its meaning is not established by the current data model or the image. No label field will imply that it is area, thickness, weight or another metric.

## Architecture

1. **Profile/data layer** — add additive telephone/website fields to company master data and the invoice snapshot. Existing records deserialize with empty strings and existing company databases receive `TEXT NOT NULL DEFAULT ''` columns.
2. **Pure label mapping** — map Step 5 through the production distribution engine and Optimization pane rows through the same canonical fields. Keep the mapping independent of WPF so it can be tested without a printer.
3. **Sticker configuration** — store workstation-specific stock/layout choices under the user's local application data, not the shared company database. A printer's stock and calibration are local to that workstation/device.
4. **Preview and printing** — a focused WPF setup window shows a preview and source-row cross-check. A dedicated paginator requests one custom-size page per sticker from WPF `PrintDialog`; it does not route through A4/A3 PDF layouts because those page contracts cannot express roll-label dimensions.
5. **Integration** — Step 5 receives a late-bound sticker-print action from the invoice composer (matching its existing optimization-runner pattern). Optimization's report/export surface receives a print-stickers action backed by its current pane list.

## Phases

### Phase 1 — printer-ready core (this change)

- Store company telephone and website in the existing company profile and immutable invoice snapshot.
- Map Step 5 and Optimization panes to sticker records without changing cutting/optimization data.
- Provide live preview, source rows, editable label width/height, margin/font controls, field visibility, one-label-per-piece mode, saved local preferences, and Windows printer selection/custom media printing.
- Add entry points beside the Step 5 optimization controls and on the Optimization report/export surface.

### Phase 2 — reusable label studio

- Named templates per label stock/printer, per-company branding presets, drag/drop field placement, typography/alignment controls, and barcode/QR options where the source has a validated identifier.
- Sheet/multi-up label layouts and configurable duplicate/reprint selection.
- Localized UI strings for every supported language and a template import/export format.

### Phase 3 — production calibration and file outputs

- Validate custom media and scaling on the target thermal/industrial printers; persist printer-specific offsets only after operator calibration.
- Add a faithful custom-page PDF output path and a print-job audit/reprint workflow if required.
- Add a Windows acceptance matrix for printer drivers, DPI, portrait/landscape stock, long labels and large batches.

## Acceptance checks for Phase 1

1. From Step 5, a sample SGU row `730 × 555`, quantity `1`, prints exactly one preview/page with the PI ID, customer and project reference from the open invoice.
2. DGU and DGU+LAM test rows produce only the panes defined by `ProductionDistributionEngine`, with outer/inner/laminated positions and colors traceable to Step 5.
3. A row with quantity `N` previews/prints `N` labels by default, each displaying quantity `1`; the alternate mode prints one label showing quantity `N`.
4. Preview values and the source list agree on glass, W×H, order/PI, customer, reference and repeat count.
5. Changing label width/height changes both preview aspect and the requested printer media size; saved preferences reload on the next launch.
6. Existing invoices and company databases without telephone/website continue to load with blank values.
7. No meaning is assigned to the sample's `0.41`.

## Environment / verification boundary

This repository targets WPF `net8.0-windows`. The current execution environment is Linux and does not have the .NET SDK, so Windows compilation, `PrintDialog`, printer-driver custom-media acceptance, scaling and physical print accuracy cannot be claimed here. Those checks remain part of the Phase 1 handoff to a Windows workstation with the target printer. Source-level tests and XML/C# structure checks will be run in this environment where possible.
