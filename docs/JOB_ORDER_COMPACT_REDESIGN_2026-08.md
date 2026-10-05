# Job Order — Compact Unified Detail Redesign (Aug 2026)

**Commit:** `b3d56be` — `feat(joborder): compact unified production layout + header action bar`
**Branch:** `arena/01a03473-proglassautomationadv`

---

## 1. What you asked for

> "Job order module production page more tighter still lots of big width.
> Make it compact tighten like we did in settings. Make compact tighten
> job order module production page. Create dn pdf excel csv save etc move
> to overview top section do a senior analysis and fix this like industrial
> application. Production section move to overview section before datagrid
> section with proper spacing do not too big or too small. Do a senior
> analysis to redesign job module."

Three concrete problems to fix:

1. **Production page too wide / wasted space** — the production form
   stacked full-width fields in one column.
2. **Action buttons (Create DN / PDF / Excel / CSV / Save) were buried at
   the bottom** — they should be at the top.
3. **Production form lived on a separate tab, below the data grid** — it
   should sit on the overview page **above** the glass-panels grid.

---

## 2. What changed

### A. One continuous page (no more Overview / Production tabs)
The Job Order detail used to swap between two tabs. Now it is a single
scrolling sheet that reads top-to-bottom:

```
┌──────────────────────────────────────────────────────────┐
│ HEADER ACTION BAR                                          │
│ BACK │ title │ CREATE DN · MASTER DN · PDF · EXCEL · CSV  │
│          · SAVE │ INVOICE VALUE                            │
├──────────────────────────────────────────────────────────┤
│ PRODUCTION DETAILS  (compact tile grid — editable)         │
│   Status | Priority | Delivery Date | Assigned To ...      │
├──────────────────────────────────────────────────────────┤
│ SOURCE INVOICE                                             │
│   COMPANY  │  CLIENT  │  PROJECT  │  INVOICE VALUE         │
├──────────────────────────────────────────────────────────┤
│ GLASS PANELS (production ticket grid)                      │
└──────────────────────────────────────────────────────────┘
```

### B. Compact tile-grid production form (like Settings)
Each of the 9 production fields now lives in its own small card
(`JoFieldTileStyle`, MinWidth 220 / MaxWidth 360) which reflows in a
`WrapPanel` — so on a wide window two or three fields sit side-by-side
instead of one field stretching the full width. The whole panel is capped
to `MaxWidth=1180` so it never balloons on a huge monitor.

Taken from one long column → a tidy, evenly-spaced grid:
- Status (combobox)
- Priority (combobox)
- Delivery Date (date picker)
- Assigned To (text)
- Site Delivery · Contact (text)
- Site Delivery · Phone (text)
- Drawing Reference (text)
- Site Delivery Address (wide text)
- Cutting Instructions (wide multi-line)
- Internal Notes (wide multi-line)

### C. All action buttons moved to the top
`BACK`, `CREATE DN`, `MASTER DN`, `PDF`, `EXCEL`, `CSV`, `SAVE` now live
in the header action bar (wrapped, so they reflow nicely on a narrow
window). The production panel no longer owns its own toolbar — this is the
"small buttons taking too much space" fix.

### D. Production form appears **before** the data grid
The production section is hosted inside the overview page **above** the
source-invoice strip and the GLASS PANELS grid, so the factory manager
sees the editable planning fields first, then the parts list.

---

## 3. What was preserved (verified, not guessed)

- **All 13 production bindings** intact (StatusOptions, SelectedStatus,
  PriorityOptions, SelectedPriority, DeliveryDate, SiteDeliveryContact,
  SiteDeliveryPhone, SiteDeliveryAddress, AssignedTo, CuttingInstructions,
  DrawingReference, InternalNotes, LastEditedDisplay).
- **All 7 header actions** bind in the detail view (BackToListCommand,
  CreateDeliveryNoteCommand, OpenMasterDnCommand, ExportPdfCommand,
  ExportExcelCommand, ExportCsvCommand, SaveCommand).
- **All overview bindings** intact (InvoiceSnapshot.Company/Client/Project,
  GrandTotalDisplay, SpecGroups and every nested row field).
- **No `Style`-set-twice / XLS0501 / MC3024 regression** anywhere in the
  Job Order XAML.
- `IsOverviewTab` / `IsProductionTab` / `SwitchDetailTabCommand` stay on
  the ViewModel so the Demo Hub workflow that calls them still runs; the
  tab strip is simply no longer rendered.
- Code-behind files unchanged; `x:Class` matches for all three views.

---

## 4. Tests added / updated

- `Tests/JobOrder/JobOrderProductionRedesignTests.cs` rewritten to lock
  the unified layout:
  - production bindings survive,
  - action buttons live in the header **and** are gone from the panel,
  - production renders **before** the glass-panels grid,
  - no XAML sets a property twice.
- Demo narration updated (production → "section", not "tab"), beat still
  ~670 chars (> 200 minimum) so `WorkflowNarrationDepthTests` passes.

---

## 5. Notes for you (non-coder summary)

- Open a **Job Order** and you now see one clean sheet: **all actions at
  the top**, the **editable production plan underneath** (as tidy side-by-side
  cards), and then the **glass parts list** below.
- No more hunting for Save / PDF / Create DN at the bottom — they're all
  in the top bar now.
- The production cards are compact and re-flow to fit the window width,
  exactly like the Settings pages you liked.
