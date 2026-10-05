# ProGlass Automation — Delivery Note Module Reference

**Purpose:** Complete technical reference for the Delivery Note (DN)
module. When building Tax Invoice or any downstream module that
consumes DN data, THIS FILE is the source of truth for DN structure,
workflow, sibling chain, storage, and integration contracts.

**Version:** 1.0 (Chat 10 — reflects Phase 10.1 → 10.9a state)

---

## 1. Module Purpose

A Delivery Note (DN) tracks the physical delivery of glass from the
factory to a customer's site. It:

- References a parent Job Order (which references a parent PI)
- Records what was actually loaded, driven, and received
- Handles partial deliveries via a sibling DN chain
- Stores photo + PDF proof of delivery
- Links to a Tax Invoice once billed (Phase 19)

**One JO can have many DNs** — the sibling chain enables partial
deliveries. Each DN documents ONE physical trip.

---

## 2. Business Rules (LOCKED)

### 2.1 DN ID Format

    DN-{Year}-{JoSeq}-{DnSeq}
    Example: DN-2026-001-01

Derived from parent JO ID:
    JOB-2026-001  →  extract "2026-001"  →  DN-2026-001-{DnSeq:D2}

### 2.2 Statuses (5)

| Status      | Meaning                              | Editable? |
|-------------|--------------------------------------|-----------|
| Draft       | Being prepared, not dispatched yet   | Yes       |
| Dispatched  | Loaded on truck, on the road         | Yes       |
| Delivered   | Reached customer site                | Yes       |
| Signed      | Customer signed delivery receipt     | Yes       |
| Received    | Customer confirmed receipt in system | Yes       |

Status transitions are free-form (no state machine). User chooses
from ComboBox in detail view.

### 2.3 Delivery Types (4)

| Type      | Meaning                                        |
|-----------|------------------------------------------------|
| Original  | First delivery for a JO item                   |
| Dummy     | Auto-created for balance qty after dispatch    |
| Rework    | Replacement for defective delivered items      |
| Return    | Customer returned items (negative delivery)    |

### 2.4 FOC (Free of Charge) Flag

- Set at **DN level** (not item level — Gotcha #9)
- When true, entire DN is FOC (no invoicing)
- Amber badge shown in UI + PDF letterhead

### 2.5 Balance Formula (CRITICAL — Gotcha #40)

    Balance = Ordered − Delivered
    (Never sum snapshot BalanceQty across siblings — double-counts)

Applied per item AND per DN AND per JO. Always compute at query
time, never trust stored snapshot.

### 2.6 Sibling DN Chain

    JO-2026-001 ordered 100 pcs
        DN-2026-001-01  Original, delivered 40 pcs → 60 balance
        DN-2026-001-02  Dummy (auto-created), delivered 30 pcs → 30 balance
        DN-2026-001-03  Dummy (auto-created), delivered 30 pcs → 0 balance
        Chain complete

Rules:
- When any DN is dispatched with balance > 0, a **Dummy DN**
  is auto-created for the remaining qty
- Only ONE Draft DN allowed per JO at a time (Draft-exists check
  before creating new one)
- User can delete Dummy DNs; system recreates on next dispatch
  if balance still exists

### 2.7 PreviouslyDeliveredQty (Self-Healing)

Each item on a DN stores `PreviouslyDeliveredQty` — sum of
DeliveredQty from all confirmed (non-Draft) sibling DNs.

**Problem:** Stored value goes stale if a sibling DN's DeliveredQty
is edited after this DN was created.

**Solution:** Self-heal on **load AND after save** by calling
`GetPreviouslyDeliveredMapAsync(jobOrderId, excludeDnId)` which
queries fresh from DB, keyed by `"{specGroupNumber}|{serialNumber}"`.

Never trust the stored value in memory — always re-heal.

---

## 3. Database Schema

### 3.1 Database File

    %AppData%\ProGlassAutomation\deliverynotes.db  (5th SQLite file)

### 3.2 Tables

    DeliveryNotes
        Id (PK)
        DeliveryNoteId          UNIQUE indexed (DN-YYYY-NNN-NN)
        JobOrderId              indexed (JOB-YYYY-NNN)
        ProformaId              indexed (PI-YYYY-NNN)  ← for Tax Invoice link
        SequenceNumber          int (1, 2, 3...)
        CustomerName            indexed
        ProjectName             indexed
        Status                  indexed ("Draft"|"Dispatched"|...)
        DeliveryType            indexed ("Original"|"Dummy"|"Rework"|"Return")
        IsFoc                   bool
        DriverName
        VehicleNumber
        ReceiverName
        DeliveryDate            indexed (DateTime)
        DeliveryTime            string "HH:mm"
        Notes                   MaxLength 500
        TotalOrderedQty         int
        TotalDeliveredQty       int
        TotalBalanceQty         int
        TotalPreviouslyDeliveredQty  int  (Phase 10.7)
        TaxInvoiceId            MaxLength 50 (Phase 10.7 placeholder)
        HasTaxInvoice           bool (Phase 10.7 placeholder)
        FullSnapshotJson        NVARCHAR blob (POCO tree)
        CreatedAt               DateTime
        UpdatedAt               DateTime

    DeliveryNoteItems (child of DeliveryNotes)
        Id (PK)
        DeliveryNoteId (FK)
        SpecGroupNumber         int
        SpecGroupName           string (used for grouping in UI)
        SerialNumber            int (row # within spec group)
        GlassRef                string
        W1, H1, W2, H2          int (mm)
        OrderedQty              int
        PreviouslyDeliveredQty  int  (self-healed on load)
        DeliveredQty            int  (user-editable)
        BalanceQty              int  (computed on save)
        ReworkQty               int
        ReturnedQty             int
        Notes                   string

    DeliveryNoteAttachments (child of DeliveryNotes)
        Id (PK)
        DeliveryNoteId (FK, string)
        AttachmentType          "Image" | "PDF"
        FileName                string (user-chosen after Rename dialog)
        FilePath                string (full disk path)
        FileSize                long (bytes)
        Description             string (optional)
        UploadedAt              DateTime

### 3.3 Attachment File Storage

    %AppData%\ProGlassAutomation\DeliveryNotes\Attachments\
        Images\{DeliveryNoteId}\
            {timestamp}_{userFileName}.jpg
        PDFs\{DeliveryNoteId}\
            {timestamp}_{userFileName}.pdf

- Max file size: 10 MB (enforced in `AttachmentLimits`)
- Allowed image types: JPG, PNG, BMP, GIF, WEBP
- Allowed PDF: PDF only
- DB stores paths only; files live on disk
- Delete DN → delete all attachment files first (best-effort)

---

## 4. Entity + Snapshot Structure

### 4.1 SavedDeliveryNote (EF Entity)

Located at:
    Models/DeliveryNote/SavedDeliveryNote/SavedDeliveryNote.cs

Same JSON-blob + indexed-column pattern as PI and JO.

    class SavedDeliveryNote
    {
        int Id;
        string DeliveryNoteId, JobOrderId, ProformaId;
        int SequenceNumber;
        string CustomerName, ProjectName;
        string Status, DeliveryType;
        bool IsFoc;
        string DriverName, VehicleNumber, ReceiverName;
        DateTime DeliveryDate;
        string DeliveryTime, Notes;
        int TotalOrderedQty, TotalDeliveredQty, TotalBalanceQty;
        int TotalPreviouslyDeliveredQty;
        string TaxInvoiceId;
        bool HasTaxInvoice;
        string FullSnapshotJson;
        DateTime CreatedAt, UpdatedAt;
        List<DeliveryNoteItem> Items;
        List<DeliveryNoteAttachment> Attachments;
    }

### 4.2 DeliveryNoteSnapshot (Pure POCO — JSON serialization)

Located at:
    Models/DeliveryNote/SavedDeliveryNote/DeliveryNoteSnapshot.cs

    class DeliveryNoteSnapshot
    {
        int SchemaVersion = 1;
        DnHeaderSnapshot Header;
        DnJobOrderRefSnapshot JobOrderRef;      ← embedded JO context
        List<DnItemSnapshot> Items;
        List<DnAttachmentSnapshot> Attachments;
    }

    class DnJobOrderRefSnapshot   ← key for downstream consumers
    {
        string JobOrderId;
        string ProformaId;
        string CustomerName;
        string ProjectName;
        string CompanyName;         ← from PI Company step
        string CompanyTrn;
        string CompanyLocation;
        List<DnJoSpecGroupSnapshot> SpecGroups;  ← for JO context display
    }

### 4.3 DeliveryNoteListItem (Lightweight for lists — no JSON parse)

Located at:
    Models/DeliveryNote/SavedDeliveryNote/DeliveryNoteListItem.cs

Returned by repository list methods. Only indexed columns.

---

## 5. Repository Contract

Located at:
    Services/DeliveryNote/Repository/IDeliveryNoteRepository.cs

**PROTECTED METHODS — DO NOT REMOVE OR RENAME** (Gotcha #46):

    Task InitializeAsync();

    // Single DN retrieval
    Task<SavedDeliveryNote?> GetByIdAsync(int id);
    Task<SavedDeliveryNote?> GetByDeliveryNoteIdAsync(string deliveryNoteId);

    // List retrieval
    Task<List<DeliveryNoteListItem>> GetAllAsync();
    Task<List<DeliveryNoteListItem>> GetByJobOrderIdAsync(string jobOrderId);
    Task<List<DeliveryNoteListItem>> GetPreparedAsync();
    Task<List<DeliveryNoteListItem>> GetDeliveredAsync();
    Task<List<PendingQtySummary>> GetPendingQtyJobsAsync();
    Task<List<DeliveryNoteListItem>> GetReworkAsync();
    Task<List<DeliveryNoteListItem>> GetReturnedAsync();

    // Delivery history
    Task<DeliveryHistorySummary> GetDeliveryHistoryAsync(string jobOrderId);
    Task<int> GetPreviouslyDeliveredQtyAsync(
        string jobOrderId, int specGroupNumber, int serialNumber);
    Task<Dictionary<string, int>> GetPreviouslyDeliveredMapAsync(
        string jobOrderId, string excludeDnId);
    // ↑ Key format: $"{specGroupNumber}|{serialNumber}"

    // Numbering
    Task<int> GetNextSequenceAsync(string jobOrderId);
    Task<string> BuildDeliveryNoteIdAsync(string jobOrderId, int sequence);

    // CRUD
    Task<SavedDeliveryNote> InsertAsync(SavedDeliveryNote entity);
    Task UpdateAsync(SavedDeliveryNote entity);
    Task DeleteAsync(int id);

    // Existence checks
    Task<bool> ExistsByDeliveryNoteIdAsync(string deliveryNoteId);
    Task<bool> HasAnyDeliveryAsync(string jobOrderId);

    // Attachments
    Task<List<DeliveryNoteAttachment>> GetAttachmentsAsync(string deliveryNoteId);
    Task<DeliveryNoteAttachment> AddAttachmentAsync(DeliveryNoteAttachment entity);
    Task DeleteAttachmentAsync(int attachmentId);

    // Attachment counts
    Task<int> GetAttachmentCountAsync(string deliveryNoteId);
    Task<Dictionary<string, int>> GetAttachmentCountsAsync(
        IEnumerable<string> deliveryNoteIds);

**Supporting DTOs:**

    class PendingQtySummary
    {
        string JobOrderId, CustomerName, ProjectName, LastDnId;
        int TotalOrdered, TotalDelivered, TotalBalance;
    }

    class DeliveryHistorySummary
    {
        string JobOrderId;
        int TotalOrdered, TotalDelivered, TotalBalance;
        int TotalRework, TotalReturned;
        List<DeliveryHistoryEntry> Entries;
    }

    class DeliveryHistoryEntry
    {
        string DeliveryNoteId, DeliveryType, Status, DriverName;
        int DeliveredQty, BalanceQty;
        DateTime DeliveryDate;
    }

---

## 6. Mapper

Located at:
    Services/DeliveryNote/Mapper/DeliveryNoteMapper.cs

Two creation paths:

### 6.1 FromJobOrder (fresh DN from parent JO)

    DeliveryNoteMapper.FromJobOrder(
        SavedJobOrder joEntity,
        JobOrderSnapshot joSnapshot,
        string newDnId,
        int sequenceNumber)
    → SavedDeliveryNote

Copies all glass items from JO spec groups as DN items with:
- OrderedQty = JO qty
- DeliveredQty = 0 (user edits)
- PreviouslyDeliveredQty = 0 (self-healed on load)
- BalanceQty = OrderedQty

### 6.2 FromBalance (Dummy DN for remaining qty)

    DeliveryNoteMapper.FromBalance(
        SavedDeliveryNote parentDn,
        string newDnId,
        int sequenceNumber)
    → SavedDeliveryNote

Called when a DN is dispatched with balance > 0. Creates a Dummy DN
where OrderedQty = the balance qty from parent DN.

### 6.3 Serialization Helpers

    DeliveryNoteMapper.RefreshSnapshot(entity, joRef)
        Rebuilds FullSnapshotJson from current entity state

    DeliveryNoteMapper.DeserializeSnapshot(entity)
        → DeliveryNoteSnapshot (POCO)

---

## 7. Attachment Service

Located at:
    Services/DeliveryNote/Attachments/AttachmentService.cs

Handles disk I/O for attachment files.

    class AttachmentService
    {
        Task<DeliveryNoteAttachment> CopyImageAsync(
            string sourcePath, string dnId, string finalFileName);

        Task<DeliveryNoteAttachment> CopyPdfAsync(
            string sourcePath, string dnId, string finalFileName);

        void DeleteFile(string filePath);
        void OpenInDefaultApp(string filePath);

        bool IsValidImageExtension(string path);
        bool IsValidPdfExtension(string path);
        bool IsUnderSizeLimit(string path);
    }

Constants in `AttachmentLimits`:
- `MaxFileSizeBytes = 10 * 1024 * 1024` (10 MB)
- `MaxFileSizeDisplay = "10 MB"`
- `ImageDialogFilter` = supported image extensions
- `PdfDialogFilter` = "PDF Files|*.pdf"

---

## 8. UI Structure

### 8.1 Shell View

    Views/DeliveryNote/DeliveryNoteView.xaml
        ContentControl + DataTemplate binding on IsListView/IsDetailView

### 8.2 List View

    Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml

- **Header:** "Delivery Notes" title + "🚚 Create DN" button
- **5 hero cards:** dynamic based on selected DN or global stats
- **6 tab card headers:** All · Prepared · Delivered · Pending Qty · Rework · Returned
- **Selected DN meta line:** shows customer/project/JO for selected row
- **Filter bar (All tab):** search + status + type + clear + refresh
- **DN Row card:** shared DataTemplate (`DnRowTemplate`)
    - Line 1: DN ID + Status pill + Type pill + FOC/DUMMY badges
    - Line 2: 📋 JO ID · Customer · 📅 Date
    - Line 3: 🚚 Driver chip + Qty chip
    - Right actions: 📂 Open button + 🧾 Invoice button
      (Invoice always visible, `IsEnabled=CanGenerateInvoice`)

### 8.3 Detail View (Phase 10.8b Premium Redesign)

    Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml

**Action bar (card style):**
    ← Back | DN ID + Type pill + FOC pill + Status pill
    [💾 Save] [🚚 Make DN] | [📄 PDF] [📊 Excel] [📋 CSV] | divider | [🗑️ Delete]

**Hero summary card (gradient):**
- Dark green header with Customer + Project + JO/PI/Seq info + Date/Time
- 4 stat tiles: ORDERED · PREV DELIVERED · THIS DN · BALANCE

**Two-column layout (2* : 3*):**
- Left column:
    - Delivery History card (indigo gradient) — click row to navigate to sibling
    - Attachments card (indigo gradient) — + Image / + PDF buttons
- Right column:
    - Delivery Details card (green gradient) — Status/FOC/Driver/Vehicle/Receiver/Notes
    - Delivery Items grid — grouped by SpecGroupName with indigo spec group headers
      Columns: # · Glass Ref/Dim · Spec Group · Ordered · Prev · This DN · Balance
    - Items grid header includes qty chips (Ordered · Prev · This DN · Balance)

### 8.4 JO Picker Dialog

    Views/DeliveryNote/DeliveryNoteList/JobOrderPicker/
        JobOrderPickerDialog.xaml + .xaml.cs

Frameless glassy modal shown when user clicks "🚚 Create DN".
User picks a JO → checks Draft-exists → creates new DN or opens existing.

### 8.5 Rename Attachment Dialog

    Views/DeliveryNote/DeliveryNoteDetail/Attachments/Rename/
        RenameAttachmentDialog.xaml + .xaml.cs

Frameless glassy modal shown on upload. Extension is locked (pill).
User edits filename only.

---

## 9. ViewModel Structure

### 9.1 Shell VM

    ViewModels/DeliveryNote/DeliveryNoteViewModel.cs

- Owns `ListVM` and `DetailVM`
- Toggles `IsListView` / `IsDetailView` state
- Public methods:
    - `OpenDeliveryNoteAsync(int deliveryNoteId)` — used by cross-module nav
    - `CreateDnFromJobOrderAsync(...)` — Draft-exists check + creation

### 9.2 List VM

    ViewModels/DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.cs

- 6 tab collections + 5 hero card values
- Selected row → hero cards swap to that DN's stats
- `CreateDnCommand` → opens JO picker
- `GenerateInvoiceCommand` → placeholder (shows "Phase 19" message)

### 9.3 Detail VM

    ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.cs

Commands:
- `SaveCommand` — validates, updates entity, self-heals, reloads
- `MakeDnCommand` (was ConfirmDispatchCommand) — dispatches + auto-Dummy
- `DeleteDnCommand` — deletes DN + all attachment files
- `ExportPdfCommand` / `ExportExcelCommand` / `ExportCsvCommand`
- `UploadImageCommand` / `UploadPdfCommand`
- `OpenAttachmentCommand` / `DeleteAttachmentCommand`
- `SelectHistoryEntryCommand` — navigates to sibling DN

Key public properties for XAML:
- `TotalOrderedQty`, `TotalPreviouslyDeliveredQty`,
  `TotalDeliveredQty`, `TotalBalanceQty`
- `GroupedItemsView` (ICollectionView, grouped by SpecGroupName)
- `HistoryEntries`, `Attachments`
- `IsSaving`, `IsLoading` (drive CanExecute)

---

## 10. Export Layouts

Located at:
    Services/DeliveryNote/Export/

Three layouts implement shared engine contracts:

### 10.1 DeliveryNotePdfLayout : IPdfLayout

- Letterhead: company from JO ref + DN ID + "Delivery Note"
- Delivery Details section (2-column: customer left, logistics right)
- Items table grouped by SpecGroupName
  Columns: # · Glass Ref · Ordered · Prev · This DN · Balance
- Group subtotals per spec group
- **5-column totals strip** (Phase 10.8a):
    ORDERED · PREV DELIVERED · THIS DN · BALANCE · DN SEQUENCE
- Signature block: Dispatched By · Received By · Date
- Subtitle: `"JO: xxx  ·  dd MMM yyyy  ·  STATUS"`

### 10.2 DeliveryNoteExcelLayout : IExcelLayout

2-sheet workbook:
- **Sheet 1 "Summary"** — DN identity + customer + logistics + qty
  totals + Tax Invoice placeholder + audit timestamps
- **Sheet 2 "Items"** — 15-column flat table, alternating rows,
  balance column red when > 0, totals row, auto-filter, freeze header

### 10.3 DeliveryNoteCsvLayout : ICsvLayout

Flat item table, **24 columns**:
    DN ID · JO ID · Customer · Project · Status · Type · FOC ·
    Date · Time · Driver · Vehicle · Receiver ·
    Spec# · SpecGroup · Row# · GlassRef · W1 · H1 · W2 · H2 ·
    Ordered · PrevDelivered · ThisDN · Balance

One row per item, ordered by SpecGroupNumber → SerialNumber.

---

## 11. Workflow Diagrams

### 11.1 Create DN from JO (from JO detail view)

    User opens JO detail
        ↓
    Clicks "Create Delivery Note"
        ↓
    JobOrderDetailViewModel.CreateDeliveryNoteAsync()
        ↓
    App.DeliveryNotes.GetDeliveryHistoryAsync(joId)
        Check TotalBalance
        ↓ If TotalBalance == 0 → "Already Delivered" message + return
        ↓ Otherwise:
    Confirm creation dialog
        ↓
    GetNextSequenceAsync + BuildDeliveryNoteIdAsync
        ↓
    DeliveryNoteMapper.FromJobOrder(...)
        ↓
    If sequence > 1: adjust PreviouslyDeliveredQty per item
        (calls GetPreviouslyDeliveredQtyAsync)
        Remove items with zero balance
        ↓
    App.DeliveryNotes.InsertAsync(newDn)
        ↓
    OnRequestOpenDeliveryNote(newDn.Id) callback
        MainViewModel navigates to DeliveryNote module
        Shell VM calls OpenDeliveryNoteAsync

### 11.2 Create DN from DN List (JO Picker)

    User clicks "🚚 Create DN" on DN List header
        ↓
    JobOrderPickerDialog opens
        User picks JO → CreateDnFromJobOrderAsync(jobOrderId)
        ↓
    Check for existing Draft DN for this JO
        ↓ If found: "Draft exists — Open existing / Cancel"
        ↓ If none:  Confirm + create new DN (same flow as 11.1)
        ↓
    Open new DN in detail view

### 11.3 Dispatch (Make DN) Flow

    User edits DeliveredQty per item in detail view
        ↓
    Clicks "🚚 Make DN"
        ↓
    Check: at least 1 item has DeliveredQty > 0?
        ↓ No: "Cannot dispatch, enter qty" + return
        ↓ Yes: Confirm dialog
        ↓
    Status = "Dispatched"
    SaveAsync() (writes entity + self-heals + reloads)
        ↓
    totalBalance = _entity.Items.Sum(i => i.BalanceQty)
        ↓ If totalBalance > 0:
            Get next sequence + build ID
            Create Dummy DN via FromBalance()
            Insert to DB
            Success message: "Dispatched + Dummy DN created"
        ↓ Else:
            Success message: "All items fully delivered"
        ↓
    OnRequestBackToList → returns to list

### 11.4 Self-Heal PREV on Load

    User opens DN in detail view
        ↓
    LoadAsync(dnId)
        entity = App.DeliveryNotes.GetByIdAsync(dnId)
        ↓
    HealItemPrevQtyAsync(entity)
        prevMap = App.DeliveryNotes.GetPreviouslyDeliveredMapAsync(
                      entity.JobOrderId, entity.DeliveryNoteId)
        for each item:
            key = "{specGroupNumber}|{serialNumber}"
            item.PreviouslyDeliveredQty = prevMap[key] or 0
        ↓
    Build ItemRows from healed items
    RecalculateTotals (includes TotalPreviouslyDeliveredQty)

### 11.5 History Navigation

    User clicks any row in Delivery History card
        ↓
    SelectHistoryEntryCommand fires
        ↓
    NavigateToDnAsync(targetDnId)
        ↓ If already on this DN: return
        ↓ Otherwise:
    App.DeliveryNotes.GetByDeliveryNoteIdAsync(targetDnId)
        ↓
    LoadAsync(target.Id)  ← full reload, all fields refresh

---

## 12. Cross-Module Integration Points

### 12.1 Called BY DN Module

- `App.Jobs.GetByIdAsync(jobOrderId)` — fetch parent JO
- `App.Jobs.GetAllAsync()` — for JO picker dialog

### 12.2 Called ON DN Module (external consumers)

- `App.DeliveryNotes.GetAllAsync()` — Dashboard stats
- `App.DeliveryNotes.GetPendingQtyJobsAsync()` — Dashboard pending count
- `App.DeliveryNotes.GetDeliveryHistoryAsync(joId)` — JO detail
  view checks balance before allowing Create DN

### 12.3 Cross-Module Navigation

- MainViewModel exposes `OpenDeliveryNoteAsync(id)` on
  `DeliveryNoteViewModel` (shell)
- Job Order detail VM fires `OnRequestOpenDeliveryNote(newId)`
  after creating DN → shell navigates + opens

---

## 13. Tax Invoice Integration (Phase 19 — When Ready)

### 13.1 Placeholder Fields Already on DN Entity

    string TaxInvoiceId    (default "")
    bool HasTaxInvoice     (default false)

### 13.2 Invoice Button on Row

    Placement: DN row card, right side, next to "📂 Open"
    Style: Green tinted, always visible
    IsEnabled bound to CanGenerateInvoice property on row VM
    CanGenerateInvoice = Status ∈ {Delivered, Signed, Received}
    Currently: shows "Coming in Phase 19" message
    Phase 19: fires actual conversion command

### 13.3 Suggested Tax Invoice Flow (for Phase 19 planning)

    1. User clicks "🧾 Invoice" on a Delivered/Signed/Received DN
    2. Check DN.HasTaxInvoice — if true, open existing Tax Invoice
    3. Otherwise: open Tax Invoice wizard pre-filled with:
       - Customer from DN.CustomerName
       - Project from DN.ProjectName
       - JO ref from DN.JobOrderId
       - PI ref from DN.ProformaId
       - Items from DN Items collection (only DeliveredQty > 0)
       - Rate/pricing pulled from embedded PI snapshot (JobOrderRef
         doesn't store rates — will need to fetch PI or JO snapshot)
    4. On Tax Invoice save:
       - Update DN.TaxInvoiceId = newTaxInvoiceId
       - Update DN.HasTaxInvoice = true
       - Repository method needed: LinkTaxInvoiceAsync(dnId, taxInvId)
    5. Consider: multiple DNs can roll up into ONE Tax Invoice?
       - If yes: TaxInvoice entity needs List<DeliveryNoteRef>
       - If no: 1 DN = 1 Tax Invoice (simpler, matches current model)

### 13.4 Data Available from DN for Tax Invoice

Everything you need to build a Tax Invoice from a DN:

**From DN entity directly:**
- DN ID, Date, Time
- Customer, Project (indexed columns)
- All items with DeliveredQty (billable amounts)
- FOC flag (skip invoicing if true)
- Driver, Vehicle, Receiver (for audit)

**From embedded snapshot's JobOrderRef:**
- Company details (Name, TRN, Location)
- Full customer context

**MISSING — must fetch from PI or JO:**
- Item rates / pricing
- VAT settings
- Other charges (silicon, argon, etc.)
- Discount %
- Terms & Conditions

**Recommendation for Phase 19:**
Extend DnJobOrderRefSnapshot to include pricing snapshot at
DN-creation time, OR fetch fresh from ProformaId when generating
Tax Invoice.

---

## 14. Complete File Locations Map

### 14.1 Models

    Models/DeliveryNote/
        Common/DeliveryNoteStatus/
            DeliveryNoteStatus.cs
            DeliveryNoteStatusMeta.cs
        Common/DeliveryType/
            DeliveryType.cs
            DeliveryTypeMeta.cs
        SavedDeliveryNote/
            SavedDeliveryNote.cs           ← EF entity
            DeliveryNoteSnapshot.cs        ← POCO tree for JSON
            DeliveryNoteListItem.cs        ← lightweight list DTO
            DeliveryNoteItem.cs            ← EF child entity
            DeliveryNoteAttachment.cs      ← EF child entity

### 14.2 Services

    Services/DeliveryNote/
        Database/
            DeliveryNotesDbContext.cs
        Repository/
            IDeliveryNoteRepository.cs
            DeliveryNoteRepository.cs
        Mapper/
            DeliveryNoteMapper.cs
        Attachments/
            AttachmentService.cs
            AttachmentLimits.cs
            AttachmentType.cs
        Export/                             ← Phase 10.8
            DeliveryNotePdfLayout.cs
            DeliveryNoteExcelLayout.cs
            DeliveryNoteCsvLayout.cs

### 14.3 ViewModels

    ViewModels/DeliveryNote/
        DeliveryNoteViewModel.cs           ← shell
        DeliveryNoteList/
            DeliveryNoteListViewModel.cs
            DeliveryNoteRowViewModel.cs
        DeliveryNoteDetail/
            DeliveryNoteDetailViewModel.cs
            DnAttachmentRowViewModel.cs
            (nested: DnItemRowViewModel, DnHistoryEntryViewModel)

### 14.4 Views

    Views/DeliveryNote/
        DeliveryNoteView.xaml              ← shell
        DeliveryNoteList/
            DeliveryNoteListView.xaml
            DeliveryNoteListView.xaml.cs   ← OnRowLeftClick, OnRowRightClick
            JobOrderPicker/
                JobOrderPickerDialog.xaml
                JobOrderPickerDialog.xaml.cs
        DeliveryNoteDetail/
            DeliveryNoteDetailView.xaml
            DeliveryNoteDetailView.xaml.cs
            Attachments/Rename/
                RenameAttachmentDialog.xaml
                RenameAttachmentDialog.xaml.cs

---

## 15. Phase Timeline

- **Phase 10.1** — Foundation (models + 5th SQLite)
- **Phase 10.2** — Repository + mapper
- **Phase 10.3** — List view (6 tabs + KPI + filters)
- **Phase 10.3b** — Premium card tab headers
- **Phase 10.4** — Detail view (two-column)
- **Phase 10.5** — Create DN from JO + cross-module nav
- **Phase 10.5a** — Removed debug MessageBoxes
- **Phase 10.5b** — Cleanup of load flow
- **Phase 10.5c** — 6 tabs + 5 hero cards + click-to-preview
- **Phase 10.6** — Attachments (disk-based file storage)
- **Phase 10.6a** — Rename dialog on upload
- **Phase 10.6b** — Clickable history rows
- **Phase 10.6c** — CommandManager.InvalidateRequerySuggested
- **Phase 10.7** — 7 improvements (balance fix, PREV heal, grouping,
  wider columns, Make DN rename, Tax Invoice fields, TotalPrevQty)
- **Phase 10.7a** — Create DN button + JO picker + Draft check +
  Delete + zero-qty warning + navigation fix + Invoice placeholder +
  row card redesign + Invoice button per row
- **Phase 10.8** — DN Export (PDF + Excel + CSV via shared engine)
- **Phase 10.8a** — Prev Delivered in PDF totals + detail chips
- **Phase 10.8b** — Premium detail page redesign
- **Phase 10.9** — Dashboard integration (Delivery Snapshot +
  activity feed + Deliveries quick action)
- **Phase 10.9a** — Overview dashboard full redesign (parent-card
  grid + chat activity + smooth scrolling)

---

## 16. Key Gotchas (DN-specific)

| # | Rule |
|---|------|
| #40 | Balance = Ordered − Delivered (NEVER sum snapshots) |
| #41 | PREV values stale — self-heal on load + save |
| #42 | Save refreshes BOTH panels (history + items grid) |
| #46 | Never remove/rename repository methods (other modules depend) |
| #48 | Reused DetailVM resets ALL editable fields on load |
| #49 | Clicking history row navigates (one DN on screen) |
| #52 | Call CommandManager.InvalidateRequerySuggested after load |

---

## 17. Testing Checklist (Manual)

Before considering the DN module "done" in any regression test:

- [ ] Create DN from JO — Draft appears in Prepared tab
- [ ] Edit DeliveredQty — Balance updates live
- [ ] Save with 0 qty — warning appears, can proceed
- [ ] Make DN — status changes, Dummy created if balance > 0
- [ ] Delete DN — DB row + all attachment files removed
- [ ] Upload image — rename dialog appears, file lands in Images/
- [ ] Upload PDF — file lands in PDFs/
- [ ] Click history row — navigates to that DN, loads fresh data
- [ ] Export PDF — 5-column totals strip visible with Prev
- [ ] Export Excel — 2 sheets, alternating rows, red balance
- [ ] Export CSV — 24 columns, one row per item
- [ ] Dashboard — Delivery Snapshot tiles show live counts
- [ ] Dashboard — DN events appear in Recent Activity
- [ ] Theme swap — DN detail hero card gradient updates
- [ ] Draft-exists check — second "Create DN" opens existing Draft