# Step 8 Approve — Unifying the Split JPG/PDF Flow (Senior Analysis)

**Date:** 2026-09-10 · **Scope:** Proforma Invoice approve flow — Saved-Invoices list ↔ Step 8 Attachments
**Directive:** merge the two-option (Image / PDF) attachment flow into a **single "APPROVE INVOICE" action in Step 8**, following the list's approve action.

---

## 1. How it works today (the fragmentation)

```
LIST row "Approve"
   └► ApproveAsync(entity)                       SavedInvoicesListViewModel.Part2
        └► ApprovalUploadChoiceDialog            ← WINDOW 1: "JPG image or PDF?"
             └► OnInvoiceUploadRequested(id, uploadPdf: bool)   ← the TYPE is smuggled as a bool
                  └► ProformaInvoiceViewModel
                       └► OpenInvoiceAtStepAndUploadAsync(id, bool)
                            └► Load invoice → GoToStep(8)
                                 └► Dispatcher.BeginInvoke( AddPdfCommand | AddImageCommand )   ← the SAME question again, pre-answered
                                      └► file picker (filter enforces the earlier choice)
                                           └► rename → copy → manifest
                                                └► AttachmentUploaded event
                                                     └► CompleteApprovalAsync(id)   (gated on _pendingApprovalInvoiceId)
                                                          └► InvoiceApprovalDialog  ← WINDOW 2: Signed PI vs LPO + LPO no.
                                                               └► validate transition → Status = Approved → audit
                                                                    └► auto-convert to Job Order → success prompt
```

Step 8 itself carries **two ghost chips — IMAGE and PDF** — disconnected from approve
semantics; they exist only as the landing targets of the smuggled bool.

## 2. Defects of the split (why this is a defect, not a style choice)

| # | Defect | Impact |
|---|---|---|
| D1 | **The JPG-vs-PDF question is asked twice** (choice dialog, then the picker filter re-enforces it) | Choosing "JPG" then holding a PDF = cancel, restart the whole approve flow |
| D2 | **The type travels as a `bool`** through `OnInvoiceUploadRequested(int, bool)` → `OpenInvoiceAtStepAndUploadAsync(id, bool)` | API encodes a UI preference; any third entry point must re-invent the dance |
| D3 | **Fragile timing**: the Add command is executed via `Dispatcher.BeginInvoke` *after* navigation purely to fire the pre-chosen picker | Sequence coupling; a slow navigation silently skips the dialog |
| D4 | **Step 8 cannot complete an approval on its own** — `CompleteApprovalAsync` only runs when the invoice was opened *from the list* (`_pendingApprovalInvoiceId`) | A clerk working in the wizard at Step 8 has no approve affordance at all |
| D5 | **Three surfaces, one intent**: list approve → choice window → Step 8 chips → picker → approval window | The user asked for ONE action; the intent ("approve this invoice") is spread across 3 windows and 2 navigation hops |

## 3. Target architecture (single merged action)

```
LIST row "Approve" ──────────────┐
                                 ▼
        OnInvoiceApproveRequested(id)            (the bool dies with the choice dialog)
                                 ▼
        Load invoice → GoToStep(8) → Step8VM.ApproveInvoiceCommand   (same button the user sees)
                                 ▼
        ONE file picker — images AND PDFs        (AttachmentLimits.CombinedDialogFilter)
        type auto-detected from the extension     (IsValidImageExtension, else PDF path)
                                 ▼
        same rename → copy → manifest pipeline → AttachmentUploaded
                                 ▼
        InvoiceApprovalDialog (Signed PI vs LPO) → validate → Approved → audit → JO
```

**Hardening (2026-09-10, second pass):** pressing APPROVE INVOICE when the PI
**already has attachments** now raises a confirmation popup first ("X already
has N attachment(s) on file… attach another confirmation document?") —
declining keeps the existing documents and leaves the approval untouched,
so a duplicate confirmation document can never be attached by reflex.

**Rules encoded in the design**
- **R1 — one intent, one action.** Step 8's toolbar exposes a single
  `APPROVE INVOICE` primary pill (`SuccessPillButton`, ✔ glyph). The two ghost
  chips are removed; the refresh button stays.
- **R2 — the type is a property of the file, not a question.** One combined
  picker; `isImage` becomes per-file auto-detection (`bool?` where `null` =
  detect). The `uploadPdf` bool and `ApprovalUploadChoiceDialog` are deleted.
- **R3 — the list action and the Step 8 button are the same command.** The list
  approve navigates to Step 8 and executes `ApproveInvoiceCommand` — there is
  exactly one approve pipeline in the product.
- **R4 — cancel-safe at every hop.** Cancelling the picker, the rename, or the
  approval dialog leaves the invoice status untouched (unchanged guarantee).
- **R5 — invalid transitions stay impossible.** `CompleteApprovalAsync` still
  validates against the workflow table before flipping status.
- **R6 — standalone Step 8 works.** The wizard host tracks the loaded invoice
  id (`_loadedInvoiceId`), so pressing APPROVE INVOICE inside Step 8 — without
  ever visiting the list — completes the same flow.

## 4. Change map

| File | Change |
|---|---|
| `Services/DeliveryNote/Attachments/AttachmentType.cs` | + `CombinedDialogFilter` (images + PDF in one picker) |
| `ViewModels/ProformaInvoice/Step8Attachments/AttachmentsStepViewModel.cs` | `AddImageCommand`/`AddPdfCommand` → single `ApproveInvoiceCommand`; `AddFileAsync` gains per-file type auto-detection |
| `Views/ProformaInvoice/Step8Attachments/AttachmentsStepView.xaml` | two chips → one `APPROVE INVOICE` pill (reuses `ProformaInvoice.ApproveInvoice` / `ApproveConvertJobOrder` keys) |
| `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part1.cs` | `Action<int,bool> OnInvoiceUploadRequested` → `Action<int> OnInvoiceApproveRequested` |
| `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part2.cs` | `ApproveAsync` drops the choice dialog → direct forward |
| `Views/ProformaInvoice/SavedInvoicesList/ApprovalUploadChoiceDialog.*` | **deleted** (merged) |
| `ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs` | new wiring (`OnInvoiceApproveRequested`), `OpenInvoiceAtStepAndApproveAsync` (executes the same Step 8 command), `_loadedInvoiceId` tracking + fallback in `OnStep8AttachmentUploaded` |

Deliberately unchanged: `InvoiceApprovalDialog` (Signed-PI/LPO + LPO number),
status transition validation, audit wording, Job-Order auto-conversion, the
attachment persistence service, and the localization keys (reused).

## 5. Verification checklist
1. Only `ApproveInvoiceCommand` references remain (no `AddImage/AddPdf` orphans).
2. No reference to `ApprovalUploadChoiceDialog` anywhere.
3. All changed C# brace/paren balanced; changed XAML XML-parse + single-content checked.
4. Cancel at picker / rename / approval dialog → status unchanged.
5. Already-approved invoice re-run → transition validation blocks (silent, as before).
