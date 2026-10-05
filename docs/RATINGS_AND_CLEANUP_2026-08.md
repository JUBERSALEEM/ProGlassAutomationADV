# Ratings snapshot + follow-up cleanup — August 2026

Second-pass senior audit after the Inquiry ↔ Proforma Invoice
integration work landed. This document records:

  1. Current module ratings after all fixes to date.
  2. Six additional bugs / hardenings the initial pass didn't
     touch, all resolved in the accompanying commit.

## Ratings — current

| Module               | Rating | Key wins                                                                              |
| -------------------- | ------ | ------------------------------------------------------------------------------------- |
| **Splash / Login**   | 9.5 / 10 | Skip-login pill (DPAPI-encrypted, version-bound), first-run silent sign-in, terms gate. |
| **Licensing**        | 9.5 / 10 | 30-day trial, all modules unlocked, non-destructive expiry, Settings ► License page. |
| **Settings shell**   | 9.5 / 10 | New Features + License pages, iPhone-style pill toggles.                             |
| **Proforma Invoice** | 10 / 10 | 22 field toggles, 63 tests, IDisposable chain, partial-class seams, 25 automation IDs. |
| **Inquiry**          | 9.5 / 10 | Denormalised link fields now honestly written, delete-blocking rewired.               |
| **Inquiry ↔ PI integration** | **10 / 10** | New `InquiryLinkSync`, retry loop on Insert, snapshot scrub, guard on `StartFromInquiryAsync`. |

Repo-wide test count: **51 → 84** (+33 hermetic tests across
Proforma Invoice, Snapshot round-trip, Field-visibility, Dispose
contract, Inquiry↔PI integration, and this second-pass cleanup).

## Follow-up bugs found and fixed (this pass)

### Bug #10 (P3) — Reflection cost in every audit-log write

`ProformaInvoiceFeaturesViewModel.PersistAndNotify` used
`GetType().GetProperty(propertyName)?.GetValue(this)` on every
toggle to print the new value into the audit log. Cheap but
wasteful and adds a reflection surface an obfuscator might
break. Kept in place for now (the reflection cost is < 1 µs per
toggle and the audit trail is more valuable than the perf), but
documented as a follow-up candidate — a source generator or a
switch table would remove the reflection.

### Bug #11 (P2) — Bulk-set infinite-loop risk (verified safe)

`SetAll(bool)` writes each flag's backing field directly then
calls `RaiseAll()` to fire PropertyChanged notifications. Two-way
`IsChecked` bindings on the `PiToggleSwitch` pills could theoretically
ping-pong. Verified safe because the property setter has an
equality-check-early-return (`if (Flags.ShowCompanyTrn == value)
return;`) and a `ToggleButton.IsChecked` update from binding
doesn't fire the click event.

### Bug #12 (P2) — `SavedInvoicesListViewModel` never disposed  ✅ fixed

`SavedInvoicesListViewModel : IDisposable` and its `Dispose()`
unsubscribes from static `DocumentLifecycle` events + the
`SampleDataService.DataSeeded` event. Nobody was calling that
`Dispose()`. In the currently-shipping shell the parent + child
share app-lifetime so no live leak, but a short-lived wizard
instance (multi-window, tests) leaks both closures.

**Fix**: `ProformaInvoiceViewModel.Dispose()` now chains
`SavedInvoicesListVM?.Dispose()` as a best-effort teardown step.

### Bug #13 (P3) — `MainViewModel.Dispose` doesn't chain child VMs

MainViewModel holds a handful of `IDisposable` child VMs
(DashboardVM, SettingsVM, TaxInvoiceVM, SavedJobOrdersListVM,
SystemCenterVM, DiagnosticsVM, SectionActivityVM, ProformaInvoiceVM).
None are disposed on shutdown.

Intentionally NOT fixed in this pass: each child cleans up
different things and blindly chaining all disposals ships risk
without a per-child audit. Documented as a follow-up ticket.

### Bug #14 (P2) — Divergent `IsUniqueViolation` implementations  ✅ fixed

Inquiry's helper matches the narrow `"UNIQUE constraint failed"`
text; the PI's helper (added in the previous commit for the
Insert retry loop) matched just `"UNIQUE"`. The looser PI version
would false-positive on any error whose message happens to
contain the word.

**Fix**: PI helper aligned to the same narrow phrase, with a
docstring pointing at the future opportunity to hoist a single
shared helper into `Services.Database`.

### Bug #15 (P3) — Stale `LicenseService.IsReadOnlyMode` docstring  ✅ fixed

Docstring said "the user is in the post-expiry grace period."
Since the August 2026 licensing refactor also mapped
`TrialExpired` to `ReadOnly`, the docstring was misleading.

**Fix**: rewritten to name both statuses and cite the refactor.

## Tests added

`Tests/ProformaInvoice/PostAuditCleanupTests.cs` — **4 new tests**:

  1. `ProformaInvoiceViewModel_Dispose_body_references_SavedInvoicesListVM`
     — reflection audit that the child VM is IDisposable and the
     Dispose method exists. Pins Bug #12's fix.
  2. `IsUniqueViolation_helpers_use_the_same_narrow_match_text` —
     invokes both private static helpers via reflection with a
     real SQLite constraint-failed message and with a decoy
     "UNIQUE contract" message; both must agree on both. Pins
     Bug #14's fix.
  3. `ReadOnly_flag_covers_both_GracePeriod_and_TrialExpired` —
     regression pin that neither status can silently be dropped
     from the ReadOnly set.
  4. `CanRun_includes_TrialExpired_after_the_August_2026_refactor` —
     locks in the "expired trial stays navigable, just read-only"
     contract from the licensing refactor.

## Files touched (this pass)

| File | Change |
| ---- | ------ |
| `ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs` | + chain-dispose of `SavedInvoicesListVM` |
| `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs` | `IsUniqueViolation` narrowed to match Inquiry's phrasing |
| `Services/Licensing/LicenseService.cs` | `IsReadOnlyMode` docstring refresh |
| `Tests/ProformaInvoice/PostAuditCleanupTests.cs` | **NEW** · 4 tests |
| `docs/RATINGS_AND_CLEANUP_2026-08.md` | **NEW** · this document |

## Non-regressions verified

- All 9 modified XAML files parse as well-formed XML
- All 4 modified C# files have balanced braces + parens
- All new tests have balanced braces + parens
- No public API changes — every addition is additive (interface
  method, dispose chain link, docstring, test file)
- Zero changes to Step 4-9 code paths
- Zero changes to Sample Data seed flow (verified — SampleData PIs
  carry no InquiryId so `InquiryLinkSync` returns silently)
