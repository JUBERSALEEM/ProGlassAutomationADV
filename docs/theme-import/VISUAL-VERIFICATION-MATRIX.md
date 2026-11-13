# Nine-family × Light/Dark visual verification matrix

**Purpose:** record observable WPF verification for all 18 runtime selections. This is a test/acceptance matrix, not source-level evidence. A cell becomes `PASS` only after the named real ERP surface has been opened in the running Windows application, inspected, and its screenshot/evidence path recorded. `NOT_RUN` is not a pass.

**Current status:** matrix defined; **0/18 combinations visually verified**. No WPF runtime or screenshot-capable Windows environment was available during authoring. Do not infer visual success from source checks or the STA source harness.

## Fixed surfaces and conditions

Use the same test database and authorized test account for all rows; reset the selected family/mode between captures. Do not replace business screens with theme-demo controls.

| Matrix column | Real WPF surface to inspect |
|---|---|
| Shell | `MainWindow.xaml` shell frame, header and content background. |
| Sidebar | `MainWindow.xaml` navigation groups, selected item, hover and disabled states. |
| Toolbar | Production toolbar in the Proforma Invoice wizard (`Views/ProformaInvoice/ProformaInvoiceView.xaml`) and its action buttons. |
| Primary / secondary buttons | Real wizard save/next actions and a secondary/cancel action; verify enabled/disabled, hover, pressed and focus feedback. |
| TextBox | Proforma Invoice client/profile and pricing inputs; verify caret, selection, placeholder/label, validation and disabled state. |
| Editable ComboBox | `Views/SheetStore/AddEditSheetView.xaml` has editable selectors with the ERP typing behavior; verify text entry, open list, selected value, keyboard navigation and validation. Do not substitute a non-editable ComboBox. |
| CheckBox | Existing Proforma Invoice options in `Views/ProformaInvoice/Step6OtherCharges/OtherChargesView.xaml`; verify checked, unchecked, disabled, focus and keyboard operation. |
| RadioButton | Existing mutually-exclusive approval choice in `Views/ProformaInvoice/SavedInvoicesList/InvoiceApprovalDialog.xaml`; verify selection, focus and keyboard operation. |
| DataGrid | `Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml`; inspect header, row, selected cell, editable cell, validation, alternating/disabled states and totals. Preserve real data/calculation behavior. |
| Dialog | Existing `Views/ProformaInvoice/SavedInvoicesList/InvoiceApprovalDialog.xaml`; inspect its real title/content/actions and close behavior. |
| Popup / dropdown | Open the editable selector dropdown in `Views/SheetStore/AddEditSheetView.xaml` and a production toolbar/context popup; inspect clipping, z-order, contrast and dismissal. |
| Representative business screen | Existing Proforma Invoice workflow from client/profile through pricing, Step 5 line-item grid and Step 6 charges, using the test database; confirm layout is theme-readable and calculations/workflow remain unchanged. |

### Per-cell acceptance

- All 18 selections must be activated through the real Settings selector/pipeline, not by changing only a test dictionary.
- Record: effective family and mode, screenshot(s), viewport/DPI, observed defects, and pass/fail. Capture at 1440×900, 100% scaling for the primary comparison; repeat any clipping/focus issues at the target deployment DPI.
- On every surface check readable foreground/background, distinct surfaces/borders/radii, text clipping, control hit targets, focus visibility, selected/hover/pressed/disabled states and popup/dialog layering.
- Validate family identity from the live view, not palette alone: Bento modular hierarchy; Clay tactile depth; Fluent restrained acrylic/system chrome; Neumorphism relief; Liquid Glass specular/frost; Glassmorphism translucent/fallback treatment; Spatial depth planes; Swiss grid/type hierarchy; Holographic restrained iridescent rim. A row cannot pass if it is merely a recolor or a surface appears broken/illegible.
- Compare Light and Dark for the same family. Confirm a family/mode switch does not lose input, change business data, alter calculations, disrupt DataGrid editing, or change access/workflow behavior.

## 18-combination results

Each cell in the status column represents **all** surfaces listed above for that selection. Use `PASS`, `FAIL`, or `BLOCKED`; leave `NOT_RUN` until inspected. Store evidence under `artifacts/theme-visual/<family-slug>/<mode>/` (outside Git unless the repository's artifact policy explicitly requires checked-in evidence) and place the exact file paths in the Evidence column.

| ID | Family | Mode | Shell | Sidebar | Toolbar | Primary button | Secondary button | TextBox | Editable ComboBox | CheckBox | RadioButton | DataGrid | Dialog | Popup/dropdown | Business screen | Evidence / defect notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| TV-01 | Bento Grid / Modular | Light | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-02 | Bento Grid / Modular | Dark | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-03 | Claymorphism | Light | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-04 | Claymorphism | Dark | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-05 | Fluent / Acrylic | Light | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-06 | Fluent / Acrylic | Dark | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-07 | Neumorphism | Light | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-08 | Neumorphism | Dark | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-09 | Liquid Glass | Light | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-10 | Liquid Glass | Dark | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-11 | Glassmorphism | Light | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-12 | Glassmorphism | Dark | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-13 | Spatial / Immersive | Light | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-14 | Spatial / Immersive | Dark | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-15 | Swiss International | Light | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-16 | Swiss International | Dark | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-17 | Holographic / Iridescent | Light | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |
| TV-18 | Holographic / Iridescent | Dark | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | — |

## Completion rule

This requirement is complete as a **defined coverage matrix**. It is not complete as visual acceptance until all 18 rows have evidence for every listed surface and all failures have been fixed and rechecked. A successful build or source/runtime unit test alone does not convert a visual cell to `PASS`.
