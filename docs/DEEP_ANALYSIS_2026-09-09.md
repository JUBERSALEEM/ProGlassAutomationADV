# ProGlass Automation — Deep Code Analysis, 2026-09-09

A senior review of the actual source, not a metrics summary. Every finding
below carries the file and line it was read at. Where a claim could not be
reproduced it is marked **UNVERIFIED** rather than dropped.

**Scope read in depth:** `App.xaml.cs`, `App.StartupCore.cs`,
`ProGlassAutomation.csproj`, `Services/Licensing/LicenseVerifier.cs`,
`LicenseGate.cs`, `Services/Security/PasswordHasher.cs`,
`Services/Database/SqliteResilience.cs`, `Services/SheetStore/SheetStoreRepository.cs`,
`Services/Reporting/VatCalculator.cs`, `Services/Reporting/ReportingDataService.Vat.cs`,
`Services/ProformaInvoice/TargetPricing/SpecificationTargetCalculator.cs`,
plus structural scans across all 1,285 C# and 229 XAML files.

**Overall verdict:** this is a well-above-average WPF codebase. The commenting
discipline, the crash-handling paths, the SQLite hardening and the licensing
crypto are all genuinely senior work. The problems are not sloppy code — they
are **architectural gaps that only show up at scale or under concurrency**, plus
a small number of validations that look like they work and do not.

---

## Severity key

| | Meaning |
|---|---|
| **C** | Critical — wrong data, lost data, or money miscomputed. Fix before any multi-user deployment. |
| **M** | Major — a real defect or a structural limit that blocks "industrial ERP grade". |
| **m** | Moderate — correct today, fragile under change. |
| **L** | Low — polish, consistency, maintainability. |

---

## Critical findings

### C1 — No transaction anywhere in a business write path

`grep -rn "BeginTransaction"` across 1,285 files returns **one** hit:
`Services/Database/SchemaMigrations/SchemaMigrationRunner.cs`.

Every business write is its own `SaveChangesAsync` on its own `DbContext`.
Multi-step operations are therefore not atomic. Concrete example,
`ViewModels/SheetStore/SheetStoreViewModel.ImportCsvAsync`:

```csharp
var added = await _repo.AddAsync(row);            // write 1 — sheetstore.db
await _historyRepo.LogAsync(added.Id, ...);       // write 2 — history
```

A crash, a disk-full, or a lock between those two lines leaves a sheet with no
history record. The same shape appears in `PersistNewSheetAsync`
(sheet + history + audit, three writes, three contexts), in proforma→job-order
creation, and in the tax-invoice→delivery-note link that
`TaxInvoiceDnReconciler` exists to repair after the fact.

**The existence of a startup reconciler is the tell.** The app already knows
these writes can diverge; it heals them on next launch instead of preventing
the divergence.

→ **Phase 1**

### C2 — No optimistic concurrency, on a read-modify-write that money depends on

There is no `IsRowVersion`, no `[Timestamp]`, no `IsConcurrencyToken` anywhere
in the codebase.

`Services/SheetStore/SheetStoreRepository.cs:161-176`:

```csharp
var sheet = await db.GlassSheets.FindAsync(sheetId);   // read
if (sheet.Balance < quantity) throw ...;               // check
sheet.Used += quantity;                                // modify
await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());  // write
```

Two operators issuing from the same sheet at the same moment both read
`Used = 5`, both pass the availability check, both write `Used = 10` from their
own baseline. One issue is silently lost and **stock goes negative**.
`BuyStockAsync` at line 142 has the identical shape.

This is not theoretical: `AppDataRoot.ConfigureFrom(Config)` supports a
**shared LAN data folder**, and `UserPresenceService` exists specifically to
show colleagues on the same network. Multi-user concurrent access is a shipped,
advertised scenario.

It also invalidates an assumption recorded elsewhere in this repo's docs: that
`Balance` can never be negative because `UseStockAsync` throws first. That holds
single-threaded. Under concurrency it does not.

→ **Phase 1**

### C3 — The VAT submission report has a validation gate that cannot fail

`Services/Reporting/VatCalculator.cs:131`:

```csharp
var result = new VatValidationResult { IsValid = true };
```

`IsValid` is **never set to false** anywhere in the file, and
`VatValidationResult.Errors` is **never populated**. Negative VAT, negative
totals and arithmetic mismatches all append to `Warnings` and set nothing else.

`Services/Reporting/ReportingDataService.Vat.cs:250-254` then does:

```csharp
if (!validation.IsValid) { invalidCount++; continue; }
```

`invalidCount` is therefore permanently `0`, and line 307 prints
`"(filtered from N, 0 invalid)"` to the operator on every VAT submission
report. The gate is decorative. For a UAE FTA filing document, a validation
step that silently validates nothing is the wrong failure mode — it reads as
checked.

→ **Phase 3**

---

## Major findings

### M1 — Two rounding policies inside one money pipeline

- `Math.Round(x, 2)` (default `MidpointRounding.ToEven`, i.e. banker's) —
  **122 call sites** across `Services/ProformaInvoice`, `Services/TaxInvoice`,
  `Services/Reporting`, `Models/ProformaInvoice`.
- `MidpointRounding.AwayFromZero` — **8 call sites**, in
  `Services/ProformaInvoice/InvoiceRoundTo.cs` and
  `Services/Reporting/VatCalculator.cs:41`.

So the pricing engine rounds half-to-even and the invoice/VAT layer rounds
half-away-from-zero. A line that lands exactly on a half-fil is rounded one way
on the way in and the other way on the way out. Individually each is defensible;
together they mean `subtotal + vat ≠ grand` on a predictable subset of invoices.

Compounding it, `VatCalculator.cs:38-41`:

```csharp
/// Rounding strategy (banker's rounding for financial accuracy)
private const MidpointRounding ROUNDING = MidpointRounding.AwayFromZero;
```

The comment says banker's rounding. `AwayFromZero` **is not** banker's rounding
— banker's is `ToEven`. A maintainer trusting the comment will reason wrongly
about every figure downstream.

→ **Phase 2**

### M2 — VAT is hardwired to UAE 5%, in an app that already does multi-country

`VatCalculator` exposes `VAT_RATE_PERCENT = 5.0m`, `VAT_RATE_DECIMAL = 0.05m`,
`VAT_MULTIPLIER = 1.05m` as `const`. Meanwhile
`Services/Localization/GeoService` and `CurrencyService` already resolve
country and currency at runtime, and `LocalizationService` supports a UI
language independent of currency.

The architecture is multi-region everywhere except the one number that appears
on every invoice. Selling outside the UAE means touching every money path.

→ **Phase 5**

### M3 — License gating guards navigation, and fails open on an unknown key

`Services/Licensing/LicenseGate.cs` maps module keys to required features.
Two properties combine badly:

```csharp
if (!Map.TryGetValue(moduleKey, out var required)) return LicenseGateResult.Allow();
```

An unrecognised key is **allowed**. The map keys are plain strings duplicated
from the navigation rail. Rename `"Optimization"` in the rail and the gate for
the paid cutting-optimisation engine silently stops applying — no compile error,
no test failure, feature unlocked.

The file's own header states the second half of the problem:

> The Gate does not block the user from OPENING a view. It blocks the
> NAVIGATION. If the module is already loaded in the ViewModel, the user can
> still scroll to it.

So gating is a UI-layer advisory, not an enforcement boundary.

Also worth a deliberate commercial decision: an active `Trial` returns
`Allow()` for everything, including `Cutting.*` and `Optimization`.

→ **Phase 4**

### M4 — The DI fallback path leaves two services null

`App.StartupCore.cs` builds the container in a `try`, and on failure constructs
twelve services by hand. Two are missing from the fallback:

```csharp
public static Services.Workflow.IStatusService Status { get; private set; } = null!;
public static Services.Dialogs.IDialogService Dialogs { get; private set; } = null!;
```

Both are `= null!` and neither appears in the `catch` block. If the container
ever fails to build, `App.Status` and `App.Dialogs` are `null` and every
consumer throws — and the failure was logged only as a `Warning`:

```csharp
AppLogService.Warning($"DI container build failed: {ex.Message}");
```

A composition-root failure means every binding in the shell is about to degrade.
That is an `Error`, and the fallback must be complete.

→ **Phase 7**

### M5 — Stored timestamps mix local and UTC

**245** uses of `DateTime.Now` against **151** of `DateTime.UtcNow` in
production code. Both appear inside the same subsystems — `LicenseVerifier`
compares in UTC while `SheetStoreRepository` stamps `LastUpdated = DateTime.Now`,
and `VatCalculator.ValidateInvoice` uses `DateTime.Now.AddDays(1)` against a
stored `IssueDate`.

Consequences: a machine whose timezone or clock changes rewrites the meaning of
historical rows; aging and overdue calculations shift; and any jurisdiction with
DST gains or loses an hour twice a year in every date-range filter. The app
already supports multiple countries via `GeoService`, so "UAE has no DST" is not
a permanent answer.

→ **Phase 6**

---

## Moderate findings

### m1 — Password policy is thin for a system that holds customer money
`PasswordHasher.MinLength = 8`. No complexity rule, no breached-password check.
The hashing itself is correct and current (PBKDF2-HMAC-SHA256, 210,000
iterations, self-describing format, transparent legacy migration) — the *policy*
is the weak part. OWASP's current guidance is 12+ for user-chosen secrets. → **Phase 9**

### m2 — `LicenseVerifier`'s header contradicts itself
Lines 12-16 say the verifier "is no longer the active runtime authority after
the secure offline activation redesign". Lines 20-22 say it "is the ONLY piece
of licensing code the app's runtime calls on every cold start". Both cannot be
true, and the next person to change licensing will read one of them. → **Phase 4**

### m3 — A license with no machine fingerprint is valid on any machine
`LicenseVerifier.cs` only checks the fingerprint
`if (!string.IsNullOrEmpty(payload.MachineFingerprint))`. That may be an
intended floating-license feature, but nothing documents it as a policy, and the
generator can therefore issue unbound licenses by omission. → **Phase 4**

### m4 — Non-constant-time fallback in legacy password comparison
`PasswordHasher.VerifyLegacySha256` does a `FixedTimeEquals` on the decoded
bytes **or** a plain `string.Equals(OrdinalIgnoreCase)`. The second branch
reintroduces the timing signal the comment above it says was removed.
Negligible on a local desktop, but the comment over-claims. → **Phase 9**

### m5 — `SqliteCacheMode.Shared` on local databases
`SqliteResilience.BuildConnectionString` opts into shared cache for non-shared
roots. Shared cache brings table-level locking inside a single process, which is
a known source of `SQLITE_LOCKED` (code 6) — the same code the retry helper
then loops on. The network-share branch correctly avoids it. → **Phase 1**

### m6 — Thirteen files exceed 990 lines
`JobProfitabilityViewModel.cs` 1,933 · `OrderReportGeneratorViewModel.cs` 1,417 ·
`SheetProfitabilityViewModel.cs` 1,300 · `PdfChrome.Part1.cs` 1,272 ·
`SpecGroup.Part1.cs` 1,246 · `VisualThemeEngine.Part1.cs` 1,157 ·
`PrintableInvoiceViewModel.Part1.cs` 1,120 · `Step9ReportExport.cs` 1,099 ·
`ProfitLossStepViewModel.TargetPricing.cs` 1,059 · `App.StartupCore.cs` 1,043 ·
`MainViewModel.Part1.cs` 1,031 · `ProformaInvoiceViewModel.cs` 1,009 ·
`ProformaInvoiceMapper.Part1.cs` 993.

Partial classes contain file length, not responsibility count. → **Phase 10**

### m7 — A fixed 2.8-second delay on every launch
`App.StartupCore.cs:636`: `await Task.Delay(2800);` to let splash animations
play. An operator launching this forty times a day pays 112 seconds a day for a
fade. → **Phase 7**

### m8 — The pricing engine rounds a rate, then multiplies by area
`SpecificationTargetCalculator` rounds the base rate to 2dp
(`Math.Round(preUplift, 2)`), then rounds the target rate to 2dp, then rounds
`rate × area`. Three successive roundings. The first is deliberate and
well-commented — it reconciles with the shop's written arithmetic — but there is
no single document stating the rounding policy for the pipeline, which is what
lets M1 exist. → **Phase 2**

---

## Low findings

- **L1 — Orphaned XML doc comments.** `App.xaml.cs` has a dangling
  `/// </summary>`; `App.StartupCore.cs` has two consecutive `<summary>` blocks
  before `ShowLanguageOfferIfPendingAsync`, leaving
  `BringMainWindowToForegroundAsync` undocumented. Harmless while
  `GenerateDocumentationFile` is off; it will become CS1587 the moment someone
  turns documentation on. → **Phase 15**
- **L2 — Two pre-existing searches use SQL Server LIKE escaping.**
  `ProformaInvoiceRepository.SearchAsync` and `JobOrderRepository.SearchAsync`
  escape as `[%]`/`[_]`, which SQLite does not implement. Recorded in
  `Services/Database/SearchPattern.cs`. Still open. → **Phase 8**
- **L3 — `DeliveryTypeValues` had zero call sites** until 2026-09-08, while
  `"Rework"`/`"Return"` were re-declared as private constants in
  `DnDashboardViewModel` and as dictionary keys in `DeliveryTypeMeta`. The
  canonical home existed and nobody used it. → **Phase 12**

---

## UNVERIFIED — recorded, not asserted

- **~51 self-referencing resource keys in `DashboardStyles.xaml`.** This figure
  appears in earlier notes for this repo. Two independent regex passes over all
  229 XAML files on 2026-09-09 returned **zero**. Either the original count used
  a different definition of "self-referencing" or it was wrong. Re-measure with
  a named definition before acting on it. → **Phase 11**
- **A fourth design system (`Crystal*` / `Liquid*` / `Glass*`) alongside three.**
  `Themes/CrystalFluid/` exists and `design-system/` holds
  `proglass-industrial` and `proglass-pi`. Whether these are parallel systems or
  layered families was not established by reading them. → **Phase 11**

---

## What is already good, and should not be "fixed"

Recording this matters as much as the defects, because a blunt upgrade pass
would damage it:

- **`SqliteResilience`** — WAL, `busy_timeout`, `foreign_keys=ON` re-applied per
  physical connection, a separate pragma script for network shares, and an error
  classifier that distinguishes retryable `SQLITE_BUSY` from terminal
  `SQLITE_CORRUPT`. Better than most production code.
- **`PasswordHasher`** — PBKDF2 at the OWASP count, self-describing stored
  format so the count can be raised without resetting passwords, transparent
  migration from the legacy scheme, constant-time comparison.
- **The four exception sinks** — `DispatcherUnhandledException`,
  `AppDomain.UnhandledException`, `SessionEnding`, `UnobservedTaskException` —
  each with a stated reason for existing, plus `ForceExitAfterCrash` to stop the
  process lingering.
- **Licensing crypto** — RSA-PSS only, with the PKCS#1 v1.5 fallback explicitly
  removed and the reason recorded; a production key *ring* so re-issued licenses
  coexist with field licenses; placeholder keys detected and explained rather
  than silently rejecting everything.
- **`SpecificationTargetCalculator`** — a pure, deterministic, WPF-free pricing
  core with explicit `TargetStatus` for every incomplete-input path, so the UI
  can never paint a fake 0% margin.
- **`AsyncUtility.SafeFireAndForget`** — one sanctioned `async void` in the app,
  with correlation IDs, replacing seven per-ViewModel copies.

---

## Phase map

Phases are ordered by **risk of harm**, not by effort. Each file in
[`phases/`](phases/) is self-contained: evidence, exact files, ordered steps,
acceptance criteria, and what must not change.

| # | Phase | Severity | Est. |
|---|---|---|---|
| 1 | [Transactions and optimistic concurrency](phases/PHASE-01-transactions-and-concurrency.md) | C | 4-6 d |
| 2 | [One rounding policy for all money](phases/PHASE-02-rounding-policy.md) | C | 2-3 d |
| 3 | [Make VAT submission validation real](phases/PHASE-03-vat-validation.md) | C | 1 d |
| 4 | [Licensing hardening](phases/PHASE-04-licensing-hardening.md) | M | 2-3 d |
| 5 | [Tax and currency as configuration](phases/PHASE-05-tax-and-currency-config.md) | M | 5-8 d |
| 6 | [UTC everywhere](phases/PHASE-06-utc-everywhere.md) | M | 3-4 d |
| 7 | [Startup resilience and DI completeness](phases/PHASE-07-startup-and-di.md) | M | 2 d |
| 8 | [Finish pagination](phases/PHASE-08-pagination.md) | M | 4-5 d |
| 9 | [Security hardening](phases/PHASE-09-security-hardening.md) | m | 2 d |
| 10 | [Decompose the god files](phases/PHASE-10-decomposition.md) | m | 5-7 d |
| 11 | [Theme and design-system consolidation](phases/PHASE-11-theme-consolidation.md) | m | 3-4 d |
| 12 | [One home for every rule](phases/PHASE-12-rules-single-home.md) | m | 3 d |
| 13 | [Test depth where money is computed](phases/PHASE-13-test-depth.md) | M | 4-6 d |
| 14 | [Observability and audit completeness](phases/PHASE-14-observability.md) | m | 3 d |
| 15 | [Release hardening](phases/PHASE-15-release-hardening.md) | L | 2-3 d |

**Do not start any phase without a green build.** Nothing committed since
`b86a3bc` has been compiled; see
[docs/BUILD_VERIFICATION_GUIDE.md](BUILD_VERIFICATION_GUIDE.md).
