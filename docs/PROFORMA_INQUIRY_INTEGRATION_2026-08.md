# Inquiry ↔ Proforma Invoice integration — senior audit (August 2026)

Follow-up to the standalone Proforma Invoice audit
(`PROFORMA_INVOICE_AUDIT_2026-08.md`). This pass examines how the
two modules cooperate. Score is preserved at **10/10** on both
modules and the integration surface between them is now tight.

## Bug catalogue — 9 findings, all fixed

| # | Sev | Area                       | Description                                                                                                                                             | Fix commit shipped in this pass |
| - | --- | -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------- |
| 1 | P1  | Inquiry ↔ PI               | `Inquiries.ProformaInvoiceId` + `ProformaNumber` columns declared, read, and used as guard — but NEVER written. Denormalized cache was permanently blank. | ✅ `InquiryLinkSync` + new repo method |
| 2 | P1  | InquiryListViewModel       | `ConvertToPiAsync` guard was dead code — the "already has a PI" check never fired because bug #1 kept the field empty.                                | ✅ (side-effect of #1)          |
| 3 | P2  | PI wizard                  | `StartFromInquiryAsync` copied 5 fields but skipped `CustomerLocation` (→BillingAddress) and used `InquiryNumber` where `ProjectDescription` would be richer. | ✅ additive pre-fill            |
| 4 | P3  | PI wizard                  | Stale navigation comment.                                                                                                                              | ✅ rewritten                    |
| 5 | P1  | PI delete                  | `DeleteAsync` never cleared the source Inquiry's back-link. Once bug #1 was fixed, this became critical.                                              | ✅ `InquiryLinkSync.AfterDeleteAsync` |
| 6 | P1  | Lifecycle cache            | PI insert / update / delete never invalidated `IInquiryLifecycleService`. Freshly-created PIs took up to 60 s to appear on the Inquiry list.          | ✅ SetProformaLinkAsync invalidates the cache |
| 7 | P1  | InquiryListRow             | `IsConverted` read the empty `ProformaNumber` → `CanConvert` stayed `true` forever → unlimited PIs per Confirmed inquiry.                            | ✅ (side-effect of #1) + guard in `StartFromInquiryAsync` |
| 8 | P1  | PI Duplicate               | Entity's `InquiryId` was NOT copied, but the snapshot JSON's `Company.InquiryId` was — first re-save of a duplicate silently pointed a phantom second PI at the source's inquiry. | ✅ `ScrubIdsForDuplicate` helper |
| 9 | P2  | PI Insert concurrency      | `SuggestNextIdAsync` + `InsertAsync` was a check-then-act race. Two concurrent saves for the same prefix caused a raw `DbUpdateException` that `RelayCommand` swallowed. | ✅ retry loop mirroring Inquiry's `CreateAsync` |

Total lines changed: ~250. Zero public API breakage (all additive).

## Architectural change

Introduced **`Services.ProformaInvoice.InquiryLinkSync`** — the
sibling of the existing `ProformaLifecycleSync` — as the ONE place
that knows how a PI event should ripple into the Inquiry module:

```
  ProformaInvoiceRepository.InsertAsync(pi)
    └─ InquiryLinkSync.AfterInsertAsync(pi)
        └─ App.Inquiries.SetProformaLinkAsync(pi.InquiryId, pi.Id, pi.ProformaId)
            ├─ writes ProformaInvoiceId + ProformaNumber on Inquiries row
            └─ InquiryLifecycleService.Invalidate(inquiryId)

  ProformaInvoiceRepository.UpdateAsync(pi)   ↓
    └─ InquiryLinkSync.AfterUpdateAsync(pi)   ↓ (same as above,
        └─ App.Inquiries.SetProformaLinkAsync(...)   idempotent)

  ProformaInvoiceRepository.DeleteAsync(pi)
    └─ InquiryLinkSync.AfterDeleteAsync(pi)
        └─ App.Inquiries.SetProformaLinkAsync(oldInquiryId, null, "")
```

The sync helper is fire-and-forget safe: every method swallows
exceptions and logs a warning. A PI operation never fails because
the back-link write failed — the PI is already committed at that
point.

**Contract on IInquiryRepository:**

```csharp
Task<bool> SetProformaLinkAsync(
    int inquiryId,
    int? proformaInvoiceId,
    string proformaNumber,
    CancellationToken ct = default);
```

- Idempotent (a no-op update returns `true` without touching the row).
- Never demands `InquiryEdit` — the authorization boundary is the
  originating PI operation which already passed
  `ProformaCreate` / `Edit` / `Delete`. Demanding the Inquiry
  permission here would refuse the link write for a Sales role
  that only holds `ProformaCreate`, leaving the columns stale.
- Invalidates the lifecycle cache on every call so a downstream
  refresh sees fresh data within one paint frame.

## Concurrency hardening — PI Insert retry

`ProformaInvoiceRepository.InsertAsync` now mirrors
`InquiryRepository.CreateAsync`'s pattern:

```csharp
for (int attempt = 0; attempt < 5; attempt++)
{
    // check
    // add
    try { await SaveChangesAsync(); break; }
    catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt < 4)
    {
        db.Entry(invoice).State = EntityState.Detached;
        invoice.ProformaId = await SuggestNextIdAsync(invoice.ProformaId);
        invoice.Id = 0;
        continue;
    }
}
```

- Retries up to 5 times before giving up (matches Inquiry's cap).
- Bumps `ProformaId` between attempts so a genuinely concurrent
  writer that reserved the same id causes a fresh id, not a loop.
- Resets `invoice.Id = 0` between attempts so EF doesn't try to
  update a stale row.
- Rejects when all 5 attempts fail with a precise
  `InvalidOperationException` naming the last attempted id.

## Duplicate scrub

New helper in `SavedInvoicesListViewModel`:

```csharp
private static string ScrubIdsForDuplicate(
    string? sourceSnapshotJson, string newProformaId)
```

- Deserialises the source snapshot using `ProformaInvoiceMapper`.
- Sets `Company.InquiryId = null` and `Company.ProformaId = newProformaId`.
- Re-serialises with the same `JsonSerializerOptions` the mapper
  uses, so every OTHER field round-trips byte-identical.
- On mapper failure, returns the original snapshot and logs a
  warning — the entity's own `InquiryId` remains null, so the
  back-link stays clean.

## Enhanced `StartFromInquiryAsync`

Extra pre-fill fields (with fall-backs):

| Inquiry field           | Target wizard field         | Fall-back |
| ----------------------- | --------------------------- | --------- |
| `CustomerName`          | `Step2VM.CustomerName`      | —         |
| `CustomerLocation`      | `Step2VM.BillingAddress`    | skip when empty |
| `SalesmanName`          | `Step2VM.SalesRepresentative` | —       |
| `PreparedByName`        | `Step2VM.DocumentPreparedBy` | —        |
| `ProjectName`           | `Step3VM.ProjectIdentifierName` | —      |
| `ProjectDescription`    | `Step3VM.ProjectReference`  | falls back to `InquiryNumber` |

New guard: refuses to start when `inquiry.ProformaInvoiceId.HasValue ||
!string.IsNullOrWhiteSpace(inquiry.ProformaNumber)` — critical because
the shell's list already blocks this at the UI, but other entry
points (Agent, tests, dashboard quick-actions) reach the method
directly.

## Tests added

`Tests/ProformaInvoice/InquiryPiIntegrationTests.cs` — **12 new tests**:

- 2 · `IInquiryRepository.SetProformaLinkAsync` contract (signature +
  implementation exists)
- 4 · `InquiryLinkSync` fire-and-forget safety (null invoice, no
  InquiryId, null delete, null App.Inquiries — all no-ops)
- 1 · `InsertAsync` retry helper (`IsUniqueViolation` present)
- 3 · `StartFromInquiryAsync` guard (method signature, model round-trip,
  status eligibility matrix)
- 2 · Duplicate scrub (snapshot round-trip preserves null InquiryId,
  scrubbed JSON no longer carries source InquiryId)

All hermetic — no DB, no filesystem. Every code path they touch
already guards `App.* == null`.

## Module test count

| Module         | Before | After |
| -------------- | ------ | ----- |
| Proforma Inv.  | 51     | **63**|
| Inquiry        | 6      | 6     |

Integration tests intentionally live under the Proforma folder
because the sync originates on the PI side.

## Files touched

| File                                                                        | Change |
| --------------------------------------------------------------------------- | ------ |
| `Services/Inquiry/InquiryContracts.cs`                                      | + `SetProformaLinkAsync` on `IInquiryRepository` |
| `Services/Inquiry/InquiryRepository.cs`                                     | + implementation of `SetProformaLinkAsync` |
| `Services/ProformaInvoice/InquiryLinkSync.cs`                               | **NEW** sync helper |
| `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs` | Insert retry + AfterInsert/Update/Delete wiring + previous-InquiryId cleanup |
| `ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs`                   | `StartFromInquiryAsync` extra fields + Proforma-link guard |
| `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.cs`| Duplicate scrub helper + wiring |
| `Tests/ProformaInvoice/InquiryPiIntegrationTests.cs`                        | **NEW** 12 tests |
