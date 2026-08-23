# Senior Developer Analysis — Inquiry Module Rebuild (2026 SaaS Redesign)

**Scope:** full root-cause analysis of the Inquiry module (saving failures, attachment
defects, dead UI), the fixes applied, and the 2026 SaaS redesign that replaces the
previous one-off hardcoded screen.

Analysis method: static trace of every write path (`InquiryEditorViewModel →
InquiryWorkflowService → InquiryRepository → InquiryDbContext`), every attachment
path (`InquiryAttachmentService → InquiryAttachmentPreviewWindow → disk`), and every
read path back into `InquiryListViewModel` / `InquiryListView`.

---

## Part 1 — Inquiry saving issues (root causes)

### S1. CRITICAL — Saved inquiries "disappear" the next day (default date filter)
**File:** `ViewModels/Inquiry/InquiryListViewModel.cs`
**Root cause:** `SelectedDateRange` defaulted to `"Today"`. The register therefore only
ever showed rows with `ReceivedAt >= today 00:00`. Three visible symptoms:
1. An inquiry saved with a backdated *Received* date (DatePicker) vanished immediately
   after save → user concludes "save does not work".
2. Every morning the register appeared empty → user concludes "records were lost".
3. After CSV import of historical inquiries, none appeared.

**Fix:** default is now `"All time"`. Date ranges (`Today / Yesterday / Last 7 / Last 30 /
All time / Custom`) are an explicit, opt-in narrowing filter. `Custom` was a fake entry
that behaved exactly like "no filter" — it is now honest (`All time`).

### S2. CRITICAL — Edits to the received date were silently discarded
**File:** `Services/Inquiry/InquiryRepository.UpdateAsync`
**Root cause:** the update statement copied 13 fields but **not** `ReceivedAt`. The
editor let the user change the received date, `Save` reported "Saved.", and the value
was never persisted. Classic silent-write-drop: no error, no exception, data loss.

**Fix:** `UpdateAsync` now persists `ReceivedAt` (guarded: never persists `default`).
Status remains intentionally excluded from the free-form update path — status changes
must go through the controlled workflow transitions (audit trail + timestamps).

### S3. HIGH — Import crashed on duplicate inquiry numbers
**File:** `ViewModels/Inquiry/InquiryListViewModel.ImportAsync`
**Root cause:** the import loop had no per-row error handling. `InquiryNumber` has a
UNIQUE index; the second row with the same number threw `DbUpdateException`, aborted
the whole import mid-way, and left a half-imported register with no user feedback.

**Fix:** `InquiryImportExportService.ParseCsvAsync` now returns a parse result
(rows + skipped count) and **de-duplicates numbers inside one file** (duplicates are
left blank so the repository generates fresh sequential numbers). The import loop
catches per-row failures and reports `created / skipped / failed` at the end.

### S4. HIGH — "Attach before save" dead end
**File:** `ViewModels/Inquiry/InquiryEditorViewModel.AddAttachmentsAsync`
**Root cause:** adding an attachment to an unsaved inquiry showed "Save the Inquiry
before adding attachments." Clicking *Save* then closed the whole editor — the user's
attachment intent was lost and the flow had to be repeated.

**Fix:** the editor auto-saves a draft record (create path) before the first
attachment, then continues attaching. Attach → Save → close is now one gesture.

### S5. MEDIUM — Inquiry-number generation race
**File:** `Services/Inquiry/InquiryRepository.CreateAsync`
**Root cause:** `MAX(existing sequence) + 1` is read-then-write without a transaction;
two concurrent saves (parallel init, double-click) hit the unique index and one save
failed with a raw `DbUpdateException`.

**Fix:** retry loop — on unique-index collision the number is regenerated and the
insert retried (bounded, 5 attempts) before surfacing an error.

### S6. MEDIUM — Cancel silently discarded work
**Root cause:** *Cancel* closed the editor with no dirty check; a fully typed inquiry
could be lost with one mis-click. **Fix:** dirty-state tracking; cancel/discard of a
dirty record requires confirmation (callback, so the VM stays UI-free).

### S7. LOW — Cancellation exceptions logged as errors
**Root cause:** `LoadAsync` rethrew `OperationCanceledException`, which
`AsyncUtility.SafeFireAndForget` then logged as a fault — one error log per debounced
keystroke, hiding real errors in noise. **Fix:** cancellations return quietly.

### S8. LOW — Decimal value binding
**Root cause:** `TextBox → decimal` binding fails silently on culture/typing mistakes
(e.g. `"1,000"` mid-typing), so the old value gets saved without any feedback.
**Fix:** the editor binds a string `ValueText` parsed explicitly with
`decimal.TryParse(NumberStyles.Number)`; parse failures are shown inline and block
save.

---

## Part 2 — Attachment issues (root causes)

### A1. CRITICAL — Image preview locked the file forever
**File:** `Views/Inquiry/InquiryAttachmentPreviewWindow.xaml.cs`
**Root cause:** `new BitmapImage(new Uri(path))` uses the default
`CacheOption.OnDemand` — WPF keeps the source file open until GC. Deleting that
attachment afterwards always failed (`File.Delete` threw, the exception was swallowed)
and orphan files accumulated on disk forever.

**Fix:** `BitmapCacheOption.OnLoad` + `CreateOptions.IgnoreImageCache` + `Freeze()` —
the bytes are decoded into memory and the handle is released immediately. The preview
window can no longer block attachment deletion.

### A2. HIGH — WebBrowser PDF control kept the PDF locked
**Fix:** on window close the `WebBrowser` is navigated to `about:blank` to release the
document before disposal.

### A3. HIGH — Misleading error when the file was missing
**Root cause:** a moved/deleted file surfaced as *"Preview is not available for this
file type"* — same message as an unsupported type; `Open` bubbled an OS error.

**Fix:** explicit existence check with a dedicated "file not found on disk" state, and
`Open` fails with a precise, actionable message instead of an exception.

### A4. HIGH — Absolute StoragePath broke when the data folder changed
**File:** `Services/Inquiry/InquiryAttachmentService`
**Root cause:** rows store the absolute path captured at upload time. Switching
Demo ↔ Production data root (or restoring a backup to another machine) invalidated
every stored path → every attachment "missing" although the files are present.

**Fix:** `ResolvePath()` — absolute path first; if missing, fall back to
`{AppDataRoot.CurrentFolder}/Inquiries/{InquiryId}/Attachments/{guid}_{FileName}`.
Used by open, preview and delete.

### A5. MEDIUM — Unobserved fire-and-forget in the editor
**Root cause:** `_ = LoadMastersAsync(); _ = LoadAttachmentsAsync();` — exceptions
 disappeared (unobserved task exceptions), and at design time `App.Inquiries == null`
 meant a silent `NullReferenceException`. **Fix:** routed through
 `AsyncUtility.SafeFireAndForget` with null guards.

### A6. MEDIUM — Preview resolved the service through the service locator
**File:** `Views/Inquiry/InquiryEditorWindow.Preview`
**Root cause:** `App.ServiceProvider.GetService(...)` on every click (crash when DI
fallback was active — `ServiceProvider` is null), while the window *already had* the
service injected in its constructor and threw it away. **Fix:** the injected instance
is stored and used.

### A7. LOW — Stale drawer after edits
The details drawer showed `ContactSummary` (never populated → always blank),
`NotesPreview` (never populated → always blank) and a hardcoded
`"Inquiry → PI not created"` chain even after a PI existed. **Fix:** drawer loads the
real detail entity; chain text is computed from the actual PI/JO links; selection is
restored by id after a reload.

---

## Part 3 — Dead / fake UI removed (truthfulness audit)

| Element | Former behaviour | Disposition |
|---|---|---|
| "In progress" / "Converted" filter values | never matched any DB status → permanently empty result | replaced by the real status model (segmented tabs) |
| Row checkbox column | bound `IsSelected`, never read by anything | removed |
| BULK CHANGE OWNER / STATUS / EXPORT SELECTED | status-bar placeholders ("No Inquiry records selected.") | removed (no fake affordances) |
| "View History" button | status-bar placeholder | replaced by the real activity timeline in the drawer |
| `ProcessingStatus` on attachments | stored the file extension under a "processing" label | removed |

---

## Part 4 — 2026 SaaS redesign

The previous screen was a light-theme-only, hardcoded copy of a static mockup
(24 hardcoded hex colors, `#FFFFFF` surfaces) that ignored the application's
Light/Dark theme engine and shared design system. The rebuild composes from the
canonical app primitives instead:

**Register (`InquiryListView`)**
- Theme-aware everywhere (`DynamicResource` brushes) — follows Light/Dark appearance.
- Page header with live result count; primary/secondary actions (New, Import, Export,
  Refresh) using `ModuleToolbar*` styles.
- Segmented status tabs with live counts (All / Received / Prepared / Sent /
  Confirmed / Hold / Cancelled) replacing the fake filter dropdown values.
- Compact KPI strip: Pipeline value, Open (Received+Prepared), Sent awaiting customer,
  Confirmed.
- Command bar: search, date range, salesman, prepared-by, priority, has-attachments
  toggle, sort, reset.
- Virtualized table (`ModuleReportListBoxStyle`, recycling, pixel scrolling) with
  sortable headers, status + priority pills, attachment chips, right-aligned value,
  received/updated columns, PI/JO chain pill, accent selection rail.
- Real loading / empty / error states (EmptyStateControl, retry).
- Pagination footer.
- Slide-in details drawer: workflow quick actions (guarded by the state machine),
  fields, attachments (preview / open / delete), activity timeline, document chain,
  Convert-to-PI (only for confirmed, unconverted inquiries).

**Editor (`InquiryEditorWindow`)**
- Two-pane workspace: validated form (customer & project / commercial / notes) +
  right rail with workflow stepper (Received → Prepared → Sent → Confirmed with
  timestamps) and attachments.
- Drag-and-drop attachments onto the drop zone (in addition to the file dialog);
  auto-saves the draft before the first attachment.
- Inline validation, dirty tracking, discard confirmation, Ctrl+S / Esc shortcuts,
  keyboard-accessible status transitions.

**Preview (`InquiryAttachmentPreviewWindow`)**
- Theme-aware; no file locks; missing-file state; releases the PDF control on close.

---

## Part 5 — What was deliberately NOT changed

- `inquiries.db` schema — zero migrations; the EF model is untouched. All fixes are
  behavioral. The unique index, cascade deletes and snapshots remain.
- Repository / workflow / attachment-service interfaces and DI registrations — same
  signatures, so every existing consumer keeps compiling.
- Audit trail, version history and permission guards — unchanged semantics
  (Inquiry still rides the Proforma permission set as before).
- Existing unit tests — still valid; new tests cover the fixed regressions.

## Part 6 — Regression tests added

- `InquiryWorkflowTests` — status normalization + transition guard (existing, kept).
- `InquiryImportTests` — CSV parsing incl. quoted cells, duplicate-number
  de-duplication, invalid-row skipping, status normalization.
- `InquirySequenceTests` — inquiry-number suffix parsing used by the collision-retry
  generator.
