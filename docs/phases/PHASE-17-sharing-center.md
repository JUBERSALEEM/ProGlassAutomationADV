# Phase 17 — Global Export & Document Sharing Center

**Roadmap + living record. Audit completed 2026-09-09. Implementation in stages.**

Rule honored throughout: **audit first, code second; reuse, don't duplicate.**
Nothing here regenerates documents or re-implements exporters — the Sharing
Center consumes artifacts the existing engines already produce.

---

## A. AUDIT — export inventory (verified by reading code, not guessing)

### A.1 Export engines (existing, reused as-is)

| Engine | File | Consumes |
|---|---|---|
| `PdfExportEngine` | `Services/Export/Pdf/PdfExportEngine.cs` | `IPdfLayout` |
| `ExcelExportEngine` | `Services/Export/Excel/ExcelExportEngine.cs` | `IExcelLayout` |
| `CsvExportEngine` | `Services/Export/Csv/CsvExportEngine.cs` | `ICsvLayout` |
| Naming | `Services/Export/FileSystem/ExportFileNamer.cs` | layout DocumentId + Company + Reference + Date |
| Dialogs | `Services/Export/FileSystem/SaveFileDialogService.cs` | — |
| Request/Result | `Services/Export/Core/ExportRequest.cs` / `ExportResult.cs` | `SavedPath` is the handoff point to sharing |

`ExportResult.SavedPath` is THE seam: every engine-backed flow already yields
a file path the Sharing Center can wrap as an artifact. No exporter changes.

### A.2 Module layouts (existing capability matrix)

| Module | PDF layout | Excel layout | CSV layout |
|---|---|---|---|
| Proforma Invoice | `ProformaInvoicePdfLayout` + `ProformaDossierPdfLayout` | `ProformaInvoiceExcelLayout` + `ProformaDossierExcelLayout` + `ProformaInvoiceListExcelLayout` | `ProformaInvoiceCsvLayout` |
| Job Order | `JobOrderPdfLayout` | `JobOrderExcelLayout` | `JobOrderCsvLayout` |
| Delivery Note | `DeliveryNotePdfLayout` | `DeliveryNoteExcelLayout` | `DeliveryNoteCsvLayout` |
| Tax Invoice | `TaxInvoicePdfExporter` (byte[] + `SaveToFileAsync`) | — | — |
| Statement | `StatementPdfExporter` (returns path) | — | — |
| Order Report | `OrderReportPdfExporter` | via `ExportSpecsAsync` | via `ExportSpecsAsync` |
| Reports (PI/JO/Delivery/TI/Statement/VAT) | generic `ReportingExportService<T>` | generic | generic |
| Specifications | `SpecificationsPdfLayout` | `SpecificationsExcelLayout` | `SpecificationsCsvLayout` |
| Step 5 cut list | `CutListExportLayouts` | `CutListExportLayouts` | `CutListExportLayouts` |
| Step 9 P&L | `Step9ReportExport` | `Step9ReportExport` | `Step9ReportExport` |
| Sheet Store | ✅ (VM commands) | ✅ | ✅ |
| Inquiry | — | — | `InquiryImportExportService` |
| Dashboard overview | `ExportReportCommand` (verify impl at integration) | ? | ? |
| Optimization / Production / JobProfitability | none found | none found | none found |

### A.3 Export UI surfaces (ViewModels — all follow one pattern)

Pattern: permission gate → load entity → build layout → `engine.ExportAsync`
→ status text + dialog on failure. Share integration adds ONE command per
surface that reuses the same generation call with `PromptForLocation=false`
into the share temp folder, then opens the composer. Surfaces:

1. `SavedInvoicesListViewModel` — 8 commands (PDF/Excel/CSV + customer +
   internal dossiers + filtered Excel). **P&L gate: internal dossier
   requires elevation — sharing must never offer the internal variant
   without the same gate.**
2. `PrintableInvoiceViewModel` (Step 7) — PDF/Excel/CSV + Print.
3. `DataGridViewModel.ImportExport` (Step 5) — PDF/Excel/CSV.
4. Step 9 P&L VMs — overview/PNL/target × 3 formats (+ glass/service/sheet).
5. `JobOrderDetailViewModel` + `SavedJobOrdersListViewModel`.
6. `DeliveryNoteDetailViewModel.Part3` (via `DeliveryNoteExportService`).
7. `TaxInvoiceDetailViewModel.PdfExport`.
8. `StatementGeneratorViewModel` + `StatementListViewModel` + `StDashboardViewModel`.
9. `OrderReportGeneratorViewModel` + `OrderReportListViewModel`.
10. `ReportBaseViewModel` subclasses (PI/JO/Delivery/TI/Statement/VAT) —
    **one integration point covers all six reports.**
11. `SpecificationsCenterViewModel`, `SheetStoreViewModel`, `InquiryListViewModel`.

### A.4 Existing sharing functionality

**None.** Only related code: a licensing help string mentioning WhatsApp,
a `mailto:` launch in `DeveloperSection.xaml.cs`, and the safe launcher
`ProcessStartGuard` (`TryOpenFile/Folder/ExplorerSelect/StartTool`).
No URL opener exists — one will be added (scheme-allowlisted) for
`https://wa.me/…` and `mailto:`.

### A.5 Contact sources (real fields only)

| Source | Identity | Phone | Email | Notes |
|---|---|---|---|---|
| `AppUser` (FROM) | Username, DisplayName, Role | ❌ | ❌ | **Minimal extension needed:** `Mobile`, `Email` + migration + UserCenter fields |
| `PreparedBySnapshot` (FROM) | Name, Dept, Status | — | — | Snapshot on every saved doc |
| `ClientSnapshot` / `SavedClient` (TO) | CustomerName, AttentionExecutiveName | `ClientContactPhone` (one) | ❌ | `FindClientByNameAsync` exists |
| `SavedSalesman` (TO) | Name, Department | ❌ | ❌ | Name only by design ("future" comment) |
| `SavedCompany` (TO, rep) | CompanyName, Representative | `RepresentativeMobile` | `CompanyEmail` | `FindCompanyByNameAsync` exists |
| Boss / Supplier entities | — | — | — | **Do not exist.** Recipient types limited to what exists + manual fallback |

Default TO chain (per prompt §43, adapted): Customer Attention w/ phone →
Customer phone → Company Representative → Salesman (name only, phone must be
supplied) → Manual entry. Never auto-send; composer always confirms.

### A.6 Architecture hooks (all exist — adapt, don't invent)

| Need | Existing hook |
|---|---|
| Settings storage | `AppConfig` JSON + `ConfigService.SaveDebounced` → add `SharingSettings` node |
| Settings UI | `SettingsViewModel.Navigation` rows + `Views/Settings/Sections/*` pattern → add Sharing section |
| Dialogs | `IDialogService.Create/ShowDialog<TWindow>` → composer is a `Window` |
| Audit | `AuditService.Log` + `AuditSections` → add `Sharing` constant |
| Localization | `LocalizationService.T(key)` + 15 locale files |
| Theme | `DynamicResource` glass system; modern `WindowStyle=None` windows |
| Safe launch | `ProcessStartGuard` → add `TryOpenUrl` (https/mailto only) |
| Permissions | `PermissionService.Require` — **decision: no new permission.** Sharing consumes files the user's existing export permission already let them generate/open; adding a gate would break flows without adding protection. The internal-dossier P&L gate stays at generation time. |
| DI | `ServiceRegistration` exists but modules use `App.*` statics — sharing follows the `App.*` static pattern for consistency |

---

## B. ARCHITECTURE (final target — matches prompt §45)

```
Existing modules → Existing exporters → SavedPath
                                              │
                                     ShareableArtifact (format-agnostic)
                                              │
                                         ShareContext
                                     (doc + customer + amounts)
                                              │
                          ┌───────────────────┴───────────────────┐
                         FROM                                    TO
                  CurrentUser/PreparedBy              ContactResolver candidates
                          │                                       │
                          └───────────────┬───────────────────────┘
                                          │
                                   ShareTemplateService
                                   (doc+provider fallback)
                                          │
                                  ShareComposer (Window)
                                          │
                                DocumentSharingService
                                          │
        ┌──────────────┬──────────────────┼──────────────┬───────────────┐
     WhatsApp        Email            ShowFile        CopyPath         Future
  (wa.me+reveal)  (mailto+reveal)   (Completed)     (Completed)
        │               │
   ExternalAppOpened (honest — never "Sent")
                                          │
                                   Audit (Sharing section)
```

### Honest-state rule (prompt §10 — non-negotiable)

`wa.me` and `mailto:` are **handoff** mechanisms: they open an external app
with prefilled text. They cannot attach files and report no delivery. So:

- WhatsApp/Email outcomes are `ExternalAppOpened`, never "Sent".
- The composer says "Opened in WhatsApp — attach the file shown in
  Explorer" and reveals the artifact beside the chat.
- Only providers that complete work inside our process (`ShowFile`,
  `CopyPath`, future real APIs) report `Completed`.

### Files to create (Stage 1 — core, no UI)

| File | Responsibility |
|---|---|
| `Services/Sharing/ShareableArtifact.cs` | Path + name + MIME + size + format + doc identity; `FromFile()` validates existence/lock/size |
| `Services/Sharing/ShareMimeTypes.cs` | Extension → MIME (pdf/xlsx/xls/csv/png/txt) |
| `Services/Sharing/ShareContext.cs` | Doc/customer/amount/date/company/prepared-by snapshot for templates + recipients |
| `Services/Sharing/ShareRecipient.cs` | Kind + name + phone + email + display lines; manual-fallback flag |
| `Services/Sharing/Contacts/PhoneNumbers.cs` | Normalize/validate; UAE trunk-0 → 971 only when explicitly regional; never mangles real international numbers |
| `Services/Sharing/Contacts/ContactResolver.cs` | `IContactResolver`: FROM (user/prepared-by) + TO candidates + default chain |
| `Services/Sharing/Templates/ShareTemplateService.cs` | Built-ins + `AppConfig` overrides; PI+WA → PI → WA → global fallback; real variables only |
| `Services/Sharing/Providers/ISharingProvider.cs` | `Id`, `Capabilities`, `ShareAsync(ShareRequest, CancellationToken)` → `ShareOutcome` |
| `Services/Sharing/Providers/WhatsAppShareProvider.cs` | wa.me chat + reveal file + honest outcome |
| `Services/Sharing/Providers/EmailShareProvider.cs` | mailto subject/body + reveal file + honest outcome |
| `Services/Sharing/Providers/SystemFileShareProvider.cs` | Reveal-in-Explorer / copy-path — true `Completed` |
| `Services/Sharing/DocumentSharingService.cs` | Validate → provider → audit → temp cleanup; async, cancellable, never blocks UI |
| `Services/Sharing/ShareTempFiles.cs` | App-controlled temp dir, safe names, traversal-proof, crash-tolerant sweep |
| `Services/Sharing/SharingSettings.cs` | `AppConfig` node: enabled providers, defaults, doc rules, temp behavior |

### Files to modify (Stage 1)

| File | Change (additive) |
|---|---|
| `Services/Audit/AuditSections.cs` | `+ Sharing` constant |
| `Services/Security/ProcessStartGuard.cs` | `+ TryOpenUrl` (https/mailto allowlist) |
| `Services/AI/AIConfig/AppConfig.cs` | `+ SharingSettings Sharing` node |

### Stage 2 — Composer + identity + settings UI

- `Views/Sharing/ShareComposerWindow.xaml(.cs)` + `ViewModels/Sharing/ShareComposerViewModel.cs`
  (theme glass cards, localized, FROM/TO/attachments/message/provider row).
- `AppUser` += `Mobile`, `Email` (migration + UserCenter editor fields).
- Settings "Sharing" nav row + section (providers, defaults, templates,
  document rules, temp behavior) — only controls with real function.
- Locale keys `Sharing.*` in all 15 files.

### Stage 3 — Integration + regression (DELIVERED 2026-09-09)

- `Services/Sharing/ShareLauncher.cs` — the ONLY glue modules add:
  `GenerateAndShareAsync` (existing exporter → share temp → composer),
  `AttachFile` + `OpenComposer` (already-existing files), layout
  staging (`ExportLayoutToFolderAsync` → `ExportResult`), central
  `RuleFormat()` mapping. `ShareContextFactory` — one reviewed
  entity→context mapping per type (nested TI snapshot, PI validity
  date, books AED, title-led list/report contexts).
- Share on every real §A.3 export surface, reusing each module's
  EXISTING generator (staged once, no dialog, no auto-open — never
  regenerated): PI saved list + Step 5 cut list + Step 7 preview +
  Step 9 P&L/reports (overview, cost ledger, price decision, glass,
  service, sheet) + JO detail/list + DN detail + TI detail (with
  attachments choice) + 6 `ReportBaseViewModel` reports (one
  base-class hook) + statement/order-report generators/lists +
  dashboard statement items + inquiry CSV + sheet store + specs
  catalogue. Wizard analyses share under the Report rule/template
  (honest wording — never invoice text on a margin report).
- Internal-dossier rule: Stage 3 shares single-format invoice/report
  artifacts only, so the P&L elevation gate is untouched and unneeded;
  if a future dossier share is added it must honor the gate.
- Dashboard statement overview export is a disabled stub (no
  generator exists) — deliberately NOT share-wired; sharing it
  would fabricate an export surface.
- `ContactResolver` merged to canonical form: optional
  `IMasterDataRepository` injection (fake in tests), username
  fallback, `CustomerEmail` recipients, company-rep gap-fill,
  attention/customer dedupe. Duplicate root resolver removed.
- Tests (`Tests/Sharing/`, xUnit): template precedence/variables/
  unknown-token passthrough; phone trunk/`+`/`00`/7–15-digit rules;
  artifact temp-file + unsupported/missing/empty rejections; rule
  doc/default fallback; resolver order/gap-fill/dedupe/sender via a
  fake repository; factory mappings from hand-built entities;
  provider `Validate()`/`Capabilities` only — `ShareAsync()` never
  (launches external processes).
- Full binding cross-check: every `Share*Command` bound in XAML,
  every share binding backed by a command.
- Build validation per §40 still needs the Windows SDK env (this
  stage was verified by API-level review + the new test suite).

---

## C. STAGE LOG

- [x] Stage 0 — Audit (this document §A) + roadmap (§B). 2026-09-09
- [x] Stage 1 — Core engine (no UI). 2026-09-09 —
  `Services/Sharing/*` (artifact, MIME, context, recipient, phones,
  resolver, templates, 3 providers, temp, settings, service) +
  `AuditSections.Sharing` + `ProcessStartGuard.TryOpenUrl` +
  `AppConfig.Sharing` (+ load hydration) + `AppUser.Mobile/Email`
  (+ audit.db migration). Composer/settings/integration not yet wired.
- [x] Stage 2 — Composer + identity + settings UI. 2026-09-09 —
  `ShareComposerViewModel` + `ShareComposerWindow` (glass card,
  localized, FROM/TO/manual/attachments/message/providers/outcome);
  `AuditService.CreateUserAsync` contact params +
  `UpdateUserContactAsync` + UserCenter create fields + per-user contact
  editor + card contact lines; Settings "Sharing" rail page (providers,
  defaults, document rules, template editor with signature-guarded save);
  51 `Sharing.*`/`UserCenter.*`/`Settings.SecSharing` keys × 15 locales.
- [x] Stage 3 — Integration + tests + regression. 2026-09-09 —
  launcher + factory + canonical resolver merge; share on every real
  export surface (PI list/wizard Steps 5+7+9, JO, DN, TI, 6
  base-hook reports, statement/order-report flows, dashboard,
  inquiry, sheet store, specs); 7 xUnit suites in `Tests/Sharing/`;
  binding cross-check clean; build validation still needs SDK env.
