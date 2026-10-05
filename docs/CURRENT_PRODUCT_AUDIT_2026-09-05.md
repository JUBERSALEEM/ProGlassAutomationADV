# PROGLASSAUTOMATION — CURRENT PRODUCT AUDIT
**Audit date:** 2026-09-05 · **Branch:** `arena/01a06d1e-proglassautomationadv` · **Commit:** `400cfa8`
**Method:** static code audit (read-only). No code was changed. Runtime verification (Windows build, click-through) was not possible in this environment — every such item is marked 🔵 / MANUAL TEST.

---

## 1. Executive Summary

- **What it is:** A glass-industry desktop ERP (WPF, .NET 8, SQLite) covering the full sales-to-cash chain: master data → inquiry → proforma invoice (9-step wizard) → job order → glass specifications → sheet-store inventory → delivery notes → tax invoices → statements → profitability, plus dashboards, PDF/Excel/CSV export, attachments, backup/restore, licensing, and a local AI assistant with voice.
- **Scale:** ~1,210 C# files, 216 XAML files, 11 EF Core DbContexts / 10+ SQLite files, 1,056 unit-test facts, Velopack installer + GitHub-based auto-update wired.
- **Stage:** Late-stage development. The architecture, hardening, and documentation trail indicate the product has been through multiple senior audits. The build is expected to compile (latest commit fixed the last XAML build break on this branch).
- **Core workflow:** Fully wired end-to-end on paper — PI → LPO approval → Job Order (with `SourceProformaId`) → Delivery Notes (with `JobOrderId` + `ProformaId`) → Tax Invoice (with `ProformaId`/`JobOrderId`/`DeliveryNoteReference`) → Statement (aggregates TI). Status transitions are centrally enforced (`Services/Workflow/StatusTransitions.cs`). Unverifiable at runtime from static analysis — manual pass required.
- **Invoice/TI numbering:** proper family + sequence system (`SequenceLetter` + `SequenceNumber`, e.g. partial/incremental invoices).
- **Safety nets:** WAL + busy_timeout on every SQLite connection (`SqliteResilience`), startup schema migrations with backup-before-migrate, full-dataset backup sets with pre-restore safety snapshot, restore drill service.
- **Biggest strengths:** (1) the glass-specific PI wizard with pricing estimator (SGU/DGU/LAM/DGULAM), Step-5 cutting grid and Step-9 profit analysis; (2) document chain integrity (snapshots + part-DN/TI flows + reconciler); (3) mature licensing platform (RSA-2048-PSS, offline activation, trial ledger, feature gating); (4) 1,000+ automated tests incl. negative-path pins (silent-catch audit, read-only gate coverage, demo isolation).
- **Biggest weaknesses:** (1) **No payment recording** anywhere — TI can be mark-paid, statements show outstanding, but there is no payment/receipt entity, no partial-payment amounts, no customer ledger entry on receipt. (2) **LPO** exists only as a free-text number field — no LPO module, no LPO validation. (3) Optimization module is a "coming soon" stub. (4) CI is not active — the build/workflow files live in `docs/ci/` because the GitHub App lacks workflow permission. (5) Production signing key handling: license public key present but trial keys are placeholders removed; generator's **private key is committed in the private repo** (acceptable short term; must move before any real distribution, see §9).
- **Attachments are split-brained:** PI/DN attachments live as files under `%AppData%\ProGlassAutomation\ProformaInvoices\…` while TI attachments are stored as blobs in `taxinvoices.db`. BackupService only copies `*.db` files in the AppData folder — **on-disk PI/DN attachments are NOT covered by any backup.**
- **Is it customer-ready?** Not yet for a real paying customer. It is close for a supervised pilot/demo. The three must-fix gaps for a first customer: payment capture (or an explicitly documented "TI is the end of chain" scope), attachment coverage in backups, and verifying the full runtime on a clean Windows box (clean install, first-run, license, update).
- **Confidence:** static evidence is strong for structure; runtime confidence is MEDIUM pending the Windows manual-test list (§14 of this audit); several individual phases have dedicated prior-audit docs under `docs/` that corroborate.

---

## 2. Current Architecture

```
ProGlassAutomation (WPF, net8.0-windows, x64)
├── Views (216 XAML)  ── ViewModels (MVVM, RelayCommand, partial-class split)
├── Services/              ~80 service folders (repositories+, exporters, migrators…)
├── Agent/                 in-app local AI assistant (tools, permissions, voice, memory)
├── Licensing/             ProGlass.Licensing (contract lib) + generator test suite
├── Tools/LicenseGenerator WPF vendor tool (signs .proglasslicense keys)
├── Themes/ + Styles/      design system (Light/Dark + Crystal Fluid family, Theme Studio)
└── 11 SQLite databases under %AppData%\ProGlassAutomation\:
    proglass.db        (Sheet Store: GlassSheets + SheetHistory)         AppDbContext
    masterdata.db      (SavedCompany/Client/Project/Salesman)            MasterDataDbContext
    inquiries.db       (Inquiry + attachments + comms + line items)      InquiryDbContext
    proforma.db*       (SavedProformaInvoice)                            ProformaInvoicesDbContext
    specifications.db  (SavedSpecification + usage)                      SpecificationsDbContext
    joborders.db       (SavedJobOrder)                                   JobOrdersDbContext
    deliverynotes.db   (SavedDN + items + attachments)                   DeliveryNotesDbContext
    taxinvoices.db     (SavedTI + items + attachment blobs)              TaxInvoiceDbContext
    statements.db      (SavedStatement + line items)                     StatementDbContext
    audit.db           (users + activity log)                            AuditDbContext
    aimemory.db        (AI memory entries)                               AiMemoryDbContext
    (* exact .db filename per model path constant)
+ Cross-DB integrity layer: DB coordinator + startup reconciler (Workflow + Result<T>)
+ Backup/restore: full-dataset sets, WAL releases, pre-restore snapshot, restore drill
+ Update: Velopack + GitHubSource (channel "stable"), version 2.0.0 single source in csproj
```

Design notes: documents are snapshot-based ("FullSnapshotJson" everywhere), so old documents never change when master data is edited; print/export always re-composes from the snapshot. Indexes are managed via `Services/Database/IndexingService.cs` + schema migration runner (`user_version` tracking, transaction-wrapped, backup-before-migrate).

---

## 3. Complete Module Status

| Module | Exists | Core function | Integration | Reliability | Release status | Priority |
|---|---|---|---|---|---|---|
| Company / Master Data | Yes | Companies, clients, projects, salesmen CRUD | Feeds all documents; card copy | Good (3 CRITICAL bugs fixed in a prior commit per `FULL_WORKFLOW_AUDIT.md`) | 🟢/🟡 (needs runtime pass) — 🟡 Medium confidence | P1 |
| Inquiry | Yes | List + editor, comms, attachments, line items, convert-to-PI | → PI (dup-guarded) | Good | 🟡 — needs manual test | P1 |
| Proforma Invoice (9 steps) | Yes | Full wizard: company/client/project → pricing estimator → data grid → charges → preview → attachments → P&L | → LPO approval, → JO; Specification catalog + usage; MasterData feed-back | Highest-quality module (most dedicated audits) | 🟡 — nearly ready | P1 |
| LPO | Partial | `LpoNumber` text fields only; approval dialog with LPO upload | PI approval → JO allowed | No module, no validation beyond required flag | 🟠/⚪ — NOT a real module | P2 |
| Job Order | Yes | Saved JOs, priorities, status transitions, dashboard | from PI (`SourceProformaId`), → DN | Good (prior audit fixes on record) | 🟡 | P1 |
| Specifications | Yes | Saved spec catalog + usage tracking | feeds PI Step 4/5/9, price history | Good | 🟡 | P2 |
| Sheet Store (inventory) | Yes | Stock CRUD, buy/use dialogs, CSV/Excel import, history, PDF | feeds PI/pricing; consumption via deliveries | Good | 🟡 — needs manual test | P1 |
| Production stages | Present | 8 stage views (cutting, tempering, polish,…) as input views | From JO production section | UNKNOWN — appears to be views + per-stage ViewModels; whether statuses roll up to JO is 🔵 | 🔵 MANUAL TEST | P1 |
| Delivery Note | Yes | Master DN + part-DNs, balance tracking, attachments, exports | from JO (`JobOrderId`), → TI (`TaxInvoiceId`), Prevents over-delivery | Good (balance enforced in `DeliveryNoteValidationService`) | 🟡 (🟢-leaning) | P1 |
| Tax Invoice | Yes | Full/partial/incremental family sequencing, VAT/TRN, PDF, attachment blobs, mark-paid | from PI or DN family, statement feed, reconciler "marks DN invoiced" | Good | 🟡 (missing payments, see §6/§12) | P0/P1 |
| Statements | Yes | 4-step generator wizard, aggregation, reconciliation view, PDF | reads TI, "outstanding" math | Good | 🟡 | P1 |
| Job Profitability | Yes | Portfolio P&L, job drill-down, glass/service/wastage views | uses Step-9 numbers only (no recompute) | Medium | 🟡 | P2 |
| Dashboards (7) | Yes | Overview + PI/JO/DN/TI/ST/SheetStore | via metrics service (N+1 removed per header) | Medium-good | 🟡 | P2 |
| Reports (PI/JO/DN/TI/ST + Sales + VAT) | Yes | report views + exports | per-module repositories | Medium | 🟡 | P2 |
| Optimization | Stub | "Coming soon" placeholder + license-gated nav | none | n/a | ⚪ NOT IMPLEMENTED | P3/Backlog |
| Attachments | Yes (mixed) | PI/DN=file-based; TI=blob; validation + preview | across PI→TI chain | Mixed | 🟠 — backup gap | P0/P1 |
| Backup / Restore | Yes | Full-dataset sets, auto-version, restore drill, retention | Settings → Data | Good for DBs | 🟠 — attachments not included | P0/P1 |
| Licensing | Yes | RSA-PSS signed keys, offline activation + trial ledger + feature gating + read-only fallback | license gate on navigation | High statically | 🟡 — 🟢 after public-key/marginal-key decision & trial runtime pass | P1 |
| Login / Users / Roles | Yes | PBKDF2-210k, default admin seeded, force-change + reset windows, sessions | audit.db | Good | 🟡 | P1 |
| Audit / Activity log | Yes | global activity, per-record history | all modules | Good | 🟢-lean | P2 |
| AI Agent | Yes | Tool-calling agent w/ permissions, voice (Whisper+System.Speech), memory, redaction, boundary, whitelist | reads: inventory/chain/profit/statement/reports; writes: ONE (sheet stock qty) | Medium | 🟡 (🔵 runtime — model downloads, mic) | P2 |
| Settings | Yes | 12 sections incl. data/backup, appearance, voice, AI, updates, license, features | global | Good | 🟡 | P2 |
| Updates (OTA) | Yes | Velopack, GitHubSource, update history + diagnostics | needs GitHub releases published | Untested end-to-end 🔵 | 🔵 MANUAL TEST | P1 |
| Crash reporting | Yes | crash dialog + listing + send/upload flow | global handlers | Good statically | 🟡 | P2 |
| Localization/i18n | Yes (16 language XAML files) | L10n dictionaries + country plans | Modules via `UiStrings` | Medium | 🟡 | P3 |
| Documentation/Roadmaps | Yes | ~130 docs | — | — | — | — |

Statuses where 🔵: "works on paper but runtime cannot be established from static analysis."

---

## 4. End-to-End Workflow Status

```
Inquiry ──► Proforma ──► LPO/Approval ──► Job Order ──► Production ──► Delivery ──► Tax Invoice ──► Payment ──► Profitability

Inquiry →  Proforma      🟡  exists (ConvertToPiCommand, dup-guard  — runtime unseen)
Proforma → Approval/LPO  🟡  exists (InvoiceApprovalDialog + upload choice; LPO = text field)
PI       → Job Order     🟡  exists (JobOrderProformaPicker; SourceProformaId persisted ✔)
JO       → Production    🟡/🔵 production stages views exist; status roll-up UNVERIFIED
JO       → Delivery Note 🟡  exists (CreateDnFromJobOrder; JobOrderId+ProformaId persisted ✔; balance ✔)
DN       → Tax Invoice   🟡  exists (CreateFromDeliveryNote/InitFromDeliveryNoteFamily; references persisted ✔)
TI       → Payment       🟠  NO payment entity exists. Only "MarkPaid" status statement-side and
                              "PartiallyPaid"/"Paid" statuses reconcile via TI status + statement aggregator.
TI/DN    → Statements    🟡  Statement generator aggregates TI; outstanding computed from status only.
Spec/Inventory feed      🟡  Sheet store consumption via "use" flow + history ✔ (deduction tracked)
All → Profitability      🟡  JobProfitability + Step 9 (cost vs net) ✔ (from snapshots)
Persistence fidelity    🟢  Snapshots (FullSnapshotJson) prevent retro-mutation on master-data edits ✔
Duplicate prevention    🟡  PI dup-guard ✔; TI family sequencing ✔; client-side checks mostly, DB constraints partial
Reopen/edit            🟡  Saved-lists exist for all documents; re-side transitions governed by StatusTransitions ✔
History/audit          🟡  Activity log + per-record history service exist; depth unverified per module
Failure handling       🟡  WAL+busy_timeout, retry helper, coordinator with compensation, startup reconciler ✔
```

**Verdict on the chain:** the chain **exists and is structurally sound**; the biggest functional gap is the missing payment leg (TI→Payment is status-only, not financial truth) and the missing native LPO module.

---

## 5. Database Status

- **11 separate SQLite files** under a single AppData root (or Demo sibling) — one per bounded context. Rationale: document-store isolation + zero cross-file FK enforcement (by design).
- **All** contexts go through `SqliteResilience.ConfigureFor`: WAL, busy_timeout 5000, FK enforced, sync=NORMAL, spawn + more pragmas. This is the right set for this topology.
- **Schema management:** `SchemaMigrationRunner` — `PRAGMA user_version` versioned, transaction-wrapped, **backs up the file before migrating**, refuses if backup fails; additive-only. `DatabaseInitializer.Initialize()` on startup; `SchemaHealthService` + `DependencyGuard` + `IndexingService` for drift guards.
- **Cross-DB consistency:** `DocumentOperationCoordinator` wraps multi-file ops with Result+CorrelationId+compensation; `TaxInvoiceDnReconciler` runs a startup sweep re-attaching DN↔TI links idempotently. Known-residual risk: a crash exactly between two writes is repaired on next launch; mid-session loss would leave a one-session divergence.
- **Backup:** `BackupService` v2 creates timestamped sets of all `*.db` + `config.json`, with safe restore + pre-restore snapshot + retention + restore-drill. **GAP: `GetFiles("*.db", TopDirectoryOnly)` — subfolders (e.g. `ProformaInvoices\<id>\…` attachment files) are NOT included.** TI attachments are safe (blobs inside `taxinvoices.db`); PI/DN attachment FILES are not backed up.

Risks rated: MEDIUM overall — solid for a single-user desktop ERP; main open items are the attachment backup gap and manual recovery experience.

---

## 6. Calculation Status

Audited calculation centres:

| Calculation | Where | Status |
|---|---|---|
| SQM (w×h×qty/mm→m²) | `Models/ProformaInvoice/*` snapshot model + Step-5 VM | Implemented; tested (`InvoiceTotalsCalculatorTests`, `DguLamPricingTests`) |
| Glass price (sheet vs SQM basis, per-family basis signal) | PI Step4 + Step9 + `PriceBasisSignalTests` | Implemented; dedicated tests |
| SGU/DGU/LAM/DGULAM pricing | PI Step4 SguParameters/DguParameters/LamParameters/DguLamParameters | Implemented; dedicated tests |
| ASP per cavity + spacer | Step-9 ASP field (commit `0a3fed1` per log) | Implemented |
| Wastage % and colour sync | Step-6 Wastages + `WASTAGE_COLOUR_SYNC_FIX.md` | Implemented (pin: no per-cell drift) |
| Sheet pricing | SheetStore repository + history | Implemented |
| Cutting/selling/margin/profit | Step 9 P&L (composes by Step 4/5/6 numbers) | Implemented (no recompute downstream) |
| Tax/VAT | TI (`InvoiceTotalsCalculator`, round-to) | Implemented; `InvoiceRoundToTests` |
| Discounts/totals | PI snapshots + `CrossStepValidator` | Implemented |
| Delivery balance | `DeliveryNoteValidationService` (delivered+prev ≤ ordered) | Implemented |
| LPO amount validation | — | ⚠ No LPO amounts beyond text field |
| Statement outstanding | StatementAggregator (status-based) | Implemented — **requires TI status discipline since no payments exist** |

⚠ DUPLICATED BUSINESS LOGIC: mostly avoided deliberately (documents capture snapshots, JobProfitability reads Step-9 numbers; documents don't recompute), but `InvoiceTotalsCalculator` (TI) and PI Step-9 totals are separate implementations of money math by design. Risk of numeric drift between TI and PI when a TI is created from DN data is explicitly controlled via snapshots + `TaxInvoiceValidationService` — verified structurally, 🔵 verify with one real document.

---

## 7. AI / Local Agent Status

Verified implementation (not just UI): `Agent/Core/ProGlassAgent.cs` (sanitize → teach-rules → intent detect → permission gate → tool exec → audit), permission service with role+confirmation gating, audit service, redaction (Google/sk- patterns), network whitelist, data boundary.

| Capability | Status |
|---|---|
| Navigate app | Implemented (`NavigateTool`) |
| Search PI/JO/DN/TI/customers | Implemented (5 search tools) |
| Inventory read (stock/history/detail) | Implemented |
| Activity read | Implemented |
| Profitability read | Implemented |
| Document-chain read | Implemented |
| Screen-context read | Implemented |
| Statement read | Implemented |
| Sales analytics / reporting read | Implemented |
| Proforma review | Implemented |
| **Update sheet stock (write)** | **Implemented — the ONLY write. Requires `SheetStore.Edit` permission + user confirmation; audited; over-issue refused.** |
| Create demo package | Implemented |
| Voice in/out | Implemented (Whisper.net STT, NAudio capture, System.Speech TTS; model download at setup) |
| Cloud LLM (Gemini/Groq/Cohere via SecretVault + consent) | Implemented with consent/network controls |
| Local LLM (LLamaSharp) | Implemented (offline only; model catalog + manager) |
| Memory | Implemented (AIMemory DbContext) |
| Teaching rules | Implemented (active-rules fetch before intent detection) |

**Agent capability bottom line:** READ-heavy by design; only one narrowly-scoped WRITE (stock quantity adjust) under confirmation + role gate + audit. No file system access beyond sanctioned paths; processes can't be started (`ProcessStartGuard`). Example supported commands: "show stock for 6mm clear", "what's left to deliver on JO-2026-…", "open the tax invoices list", "review the last proforma", "mark 2 sheets of … used". 🔵 Runtime columns (mic, whisper model, LLM download) require a manual test on real hardware.

---

## 8. UI / UX Status

- One central design system (DesignTokens + per-family Theme dictionaries; Theme Studio; Appearance section). Light/dark + Crystal family verified consistent in file scan.
- Navigation: rail-based (MainViewModel + per-module VMs), 16+ view areas, license-gated where applicable.
- Patterns: settings tile + section pattern is uniform; module list pages share `ModuleList*` styles; 3 defect-class hand-built tables got geometry fixes recently (sheet store, delivery report, Step-6).
- Empty/loading/confirmation states: dedicated EmptyState/LoadingState controls + dialogs exist throughout.
- Validation UX: per-module validation services surface messages near inputs (PI has `CrossStepValidator` + field visibility).
- Accessibility/keyboard: partially handled (focus visuals, tooltips); no formal keyboard map or screen-reader pass — P2 item.
- ⚠ No high-DPI scaling verification possible here; several fixed-pixel widths appear in report tables — 🔵 verify at 125-150% display scaling.

---

## 9. Security / Licensing Status

| Item | Status |
|---|---|
| Password storage | **PBKDF2-HMAC-SHA256, 210k iterations, 16-byte random salt, self-describing storage** (back-compat with seeded SHA-256 upgrades on first login) |
| Seeded admin | admin/admin seeded, **ForcePasswordChangeWindow** + Reset password windows exist; last-admin protection |
| Secrets (API keys) | SecretVault: Windows DPAPI with versioned entropy + health check; redaction in agent pipelines; no keys in repo (grep clean) |
| License files | Signed `.proglasslicense` (RSA-2048-PSS over canonical JSON), `keyId`, product pin, expiry, version policy, optional machine binding; Windows DPAPI at rest |
| Trial | Auto 30-day trial via tamper-evident `TrialLedger`; real license always wins; removal → trial fallback → read-only mode |
| Feature gate | `LicenseGate` map on navigation + `ReadOnlyGate.Demand` on **every repository write** (52 sites, pinned by tests) |
| Machine binding | CPU+OS+arch+runtime+volume serial+NIC MAC → SHA-256 fingerprint |
| Private signing key | **Lives in the private repo's `Tools/LicenseGenerator` + DPAPI-wrapped store**. Because the repo is private (verified: `isPrivate:true`) this is acceptable TODAY but must be moved to a secrets store before any public distribution/more contributors. |
| AI network egress | Consent + whitelist + boundary + redaction; local-only by default |
| Update channel | Velopack GitHubSource on the private repo — releases must be published for updates to work; signature verifier present |
| SQL injection | Parameterised EF Core throughout (no raw concat found) |
| Process execution | `ProcessStartGuard` allowlist |

Verdict: **licensing implemented (🟡🟢 boundary); secure enough for a pilot release; HSM/secret-store move is a P1 hardening step before scaling distribution.**

---

## 10. Performance Status

- Dashboards previously used GetAllAsync; now metric projection queries (per `DashboardMetricsService` header) — N+1 addressed for 6 of 7 dashboards. Statement + TI metric services use SQL aggregates.
- DataGrids: list rows are row-model projections (…ListRow / …AnalyticsRow types), not entity materialisation — good pattern.
- Step-5 grid: copy/paste bulk path exists (30 paste-related code paths); Step-6 metric tables recently geometry-fixed.
- Attachments-as-blobs (TI): FileBytes excluded from list queries (`HasContent` lazy door) — OK.
- PDF: QuestPDF composition; statement PDF multi-page OK structurally.
- SQLite: WAL+busy_timeout + retry helper + pooled connections cleared for backup — good.
- Async: 25 `async void` count (mostly RelayCommand → SafeFireAndForget path — acceptable); 34 `.Wait()/.Result` hits — concentrated in startup sync bridges; no obvious hot-path sync-over-async found.
- 🔵 Unknowns needing load testing: 10k-row PI data grid, 500+ DN balance sweep, statement over 1k invoices.

---

## 11. Build / Installer / Release Status

- **Target:** `net8.0-windows`, WinExe, x64; analyzers on, TreatWarningsAsErrors with curated not-as-errors list; deterministic + CI-friendly; **Version 2.0.0 single-sourced** in csproj.
- **Entry:** custom `[STAThread] Main()` runs the Velopack hook first (correct vpk pattern).
- **Packaging:** Velopack; manifest settings `installer/velopack.json` (id/version/channel stable); `scripts/build-installer.ps1` = publish → vpk pack → portable zip.
- **Auto-update:** `VelopackUpdateService` + `GithubSource("JUBERSALEEM/ProGlassAutomationADV")` private repo. Requires GitHub Releases to be published to function; **untested end-to-end.** 🔵
- **CI:** build/test workflow exists under `docs/CI/` + `docs/ci/` but is **NOT active** on the repo (App lacks workflows permission; activation steps documented). Dependabot for NuGet weekly is configured `.github/dependabot.yml`.
- **Clean machine experience:** DB init runs at startup; login seeds admin/admin + force-change; license activation on first run; data folder = %AppData%. 🔵 Matrix test on a bare Windows 10/11 VM is on the manual list.
- **Tooling included in repo:** LicenseGenerator (vendor tool, separate project) ✔; Tests project (1,056 facts incl. source-pins, xUnit) ✔. Solution file `.slnx` (requires VS 2022 17.10+).

**Can a normal customer install it?** Structurally yes (Setup.exe via Velopack), provided a release is published AND the license public key in the shipped binary matches what the vendor generator signs (currently consistent via pinned source test).

---

## 12. Top 10 Problems (ranked)

1. **🔴 Critical — No payment/receipt model at all.** TI supports only MarkPaid/PartiallyPaid status; statements derive "outstanding" from status flags, not amounts. There is no place to record "customer paid 30,000 on Tuesday". Any customer-facing AR story breaks here. *(Evidence: no Payment entity found in Models, `TaxInvoiceDetailViewModel.Commands.MarkPaidCommand`, `StatementAggregator` sums `Balance` from status.)*
2. **🔴 Critical — PI/DN attachment files are NOT covered by backup.** BackupService copies only `*.db`+`config.json` from the AppData root. `ProformaAttachmentService` stores files in `%AppData%\ProGlassAutomation\ProformaInvoices\…` and DN attachments the same pattern; TI blobs are safe. Restore after disk loss loses every PI/DN attachment.
3. **🔶 High — Auto-update path is unwired/unverified.** VelopackService targets GitHub Releases on a private repo. GitHub requires releases (and auth for private). No evidence a release was ever published; auto-update for customers may silently never work.
4. **🔶 High — CI is not active.** `docs/ci/build-test.yml` exists but never ran on the repo. 1,056 tests exist but nobody runs them on push. Regressions are caught by memory, not by machine.
5. **🔶 High — LPO is a text field, not a module.** Approval upload stores an LPO image path, but there is no LPO entity, validation, amount match, or persistent link beyond a string. In glass-industry workflows LPOs are contractual — this will be asked for at first demo.
6. **🔶 Medium — Status transitions exist but payment/AR statuses are unenforced.** `StatusService` covers document transitions, but without payments, "Issued→Paid" can be toggled at will with no audit of who/when/how-much.
7. **🔶 Medium — Production-stage roll-up unverified.** 8 stage VMs/views exist; whether JO status reflects stage completion, or whether production feeds sheet consumption automatically, is 🔵 unproven. Both answers are plausible from the code layout.
8. **🔶 Medium — Demo ↔ production data separation is a folder switch only.** AppDataRoot demo/prod isolation is good, but a settings slip (or future bug) could seed/delete in the wrong folder; pinned by `DemoIsolationTests` — keep that test alive.
9. **🔹 Low — Accessibility/keyboard is informal.** No focus-order/keyboard map audit; factory operators may need keyboard-driven flows.
10. **🔹 Low — Update package ships `*.pdb` by velopack.json include glob** but zipExclude excludes them in the release zip — inconsistency; decide one policy.

---

## 13. P0 — Release Blockers

1. **Attachment backup gap** (item 2 above) — silent data loss on restore. Fix: include `ProformaInvoices/` (and any DN attachment dir) subfolders in BackupService sets + restore + drill verification.
2. **Decide the payments story OR clearly de-scope it** — for a glass-company ERP, shipping without any payment capture is a known limitation that must be either closed (receipt entity + TI allocation + statement aging by amounts) or explicitly communicated as "v2.0: TI is the end of chain; receipts land in v2.1" with statements re-labelled. Silence is the blocker, not the gap itself.
3. **Ship one real update release** and test install→update→install on a clean VM (Velopack, license state persisted, DB migration applied).
4. **Windows runtime matrix** (clean Windows 10 + 11; 125% DPI; non-admin user) — the manual test list below.

*(I deliberately do NOT count compile/build as P0 — the current head is expected to compile; verify on the Windows box per memory: the `RowHeaderWidth` + tokens fix shipped in `400cfa8` this session.)*

---

## 14. P1 — First-Customer Fixes

1. **Activate CI** (copy `docs/ci/build-test.yml` → `.github/workflows/` from the owner account — documented one-line step).
2. **Move the signing private key out of the repo** (Azure Key Vault / DPAPI-only local store + hardware token) — the current repo-private state is tolerable for pilot, not for scale.
3. **Payments minimum viable:** receipts table + allocate-to-invoices + status roll-up (PartiallyPaid with amounts). Even a minimal version kills the #1 risk.
4. **LPO minimum viable:** uniqueness per customer, amount vs PI match warning, link record. Ship as fields + validation first, module later.
5. **Production roll-up check** (item 7) — decide JO status behavior and test one job through all 8 stages.
6. **Backup restore drill on a non-dev machine** including attachments (after P0#1).
7. **DPI/monitor matrix** + keyboard-only pass over PI wizard.
8. **First-run onboarding**: company setup → default company → user creation flow polish (the docs flag only license-slice done from the original spec).
9. **Crash-report review loop:** wire Settings → CrashReports visibility; test the diagnostic pack export.
10. **Agent voice + cloud-provider flow** smoke test on a customer-grade PC (mic permission, whisper model, Gemini key).

---

## 15. P2 — Improvements

1. Statement aging by amounts (post-payments).
2. Test coverage for the document-chain transitions via integration tests on temp DBs (currently mostly source-pin/analysis tests).
3. Performance baseline with scripted data sets (10k PI rows, 500 DNs, 1k TIs).
4. Accessibility: focus visuals, keyboard shortcuts, WCAG text contrast pass.
5. Localization completeness run (`scripts/i18n-audit.py` in CI).
6. Logging: rotate + size-cap log files; diagnostics pack download from Settings.
7. Trial-consume UX polish (expiring countdown, upgrade entry points).
8. Schema-health dashboard (Version/Drift view exists in Checks — surface it in Settings → Diagnostics).
9. Backup scheduling (auto-daily set + pruning) — currently manual only.
10. Documentation pruning: ~130 markdown files at repo root + docs/ — merge into a single `/docs/INDEX.md` entry point for supportability.

---

## 16. P3 — Future Ideas (post-stable)

1. **Optimization module** (genuinely needed for glass cutting; currently stub).
2. Multi-user/network server mode (bigger decision — current topology is single-machine).
3. E-invoicing (ZATCA/UAE FTA phase-2 e-invoicing) — will become mandatory in market; plan, don't build yet.
4. Customer portal for PI approval/sign-off.
5. Barcode/QR on delivery notes for warehouse scanning.
6. Mobile companion (delivery capture / photos).
7. Enhanced AI: voice-driven full workflows, model-bundled offline packs.
8. Multi-tenant / cloud sync (only if a real customer asks).
9. Supplier PO + purchasing.
10. Production machine integrations (cutting table CNC export formats by vendor).

---

## 17. What NOT To Build Now

1. **New AI features** — the agent does plenty; harden what exists (voice on real hardware, permissions, memory decay). No new tools.
2. **Optimization engine** — it's a stub today; do not start it until v2.x is stable and a customer pays for cutting optimization specifically.
3. **LPO full module** — do not build the full module; ship P1 fields+validation only until a customer needs more.
4. **Theme variants / more skins** — Crystal family is sufficient; no new families.
5. **Server/cloud backend** — current file-based topology is correct for the first customers; premature multi-user work burns runway.
6. **New document types** (quotations separate from PIs, supplier POs) — the chain is complete enough; don't fork it.
7. **Per-priced-cell styling polish on remaining "hand-built list views"** (e.g., GodViews, About) — I audited these; they are intentional layouts, not defects. Leave them.
8. **Speculative integrations** (accounting sync, WhatsApp, SMS) — not without a signed pilot.

---

## 18. Minimum Sellable Product (MVP / First-Customer Product)

The smallest reliable version you can sell:

1. **Master data**: companies (with default), clients, projects ✔
2. **PI wizard** (9 steps) with pricing estimator, data-grid, charges, preview **PDF**, attachments ✔
3. **LPO approval** with upload (fields + validation, no separate module) — patch on P1.
4. **Job order** creation from PI + status tracking ✔
5. **Delivery notes** with partial deliveries + balance enforcement ✔
6. **Tax invoices** (full/family) with VAT + PDF + mark-paid ✔
7. **Receipts** — minimal payment capture + TI allocation (P1#3) — *currently absent; MVP requires it*.
8. **Statements** with outstanding by amounts (post-receipts) ✔(-payments)
9. **Sheet store** with buy/use/history + CSV import ✔
10. **Job profitability** views ✔
11. **Backup & restore** incl. attachments (post-P0#1) + restore drill ✔(-attach)
12. **Licensing**: signed key + trial + read-only ✔
13. **Updates**: working OTA from GitHub releases (P0#3) — *currently unverified.*
14. **Login/Roles** + audit ✔

Everything else (agent incl., reports incl., VAT submission sheet) is **bonus, not MVP** — impressive in demos but not required to take the first customer's money.

---

## 19. Recommended Development Order

1. **Fix** the attachment backup gap (P0#1) + add pin test.
2. **Fix** the payment minimum (P1#3) + LPO fields (P1#4) — the two sales-blockers.
3. **Publish one real GitHub Release** and test clean install→update→license-on-clean-machine.
4. **Switch on CI** from the owner account (P1#1) — watch the 1,056 tests go green or expose real breakage.
5. **Test** the complete document chain once by hand on a clean Windows VM (manual list §33-item below).
6. **Test** voice/AI on a customer-grade machine.
7. **Stabilize** anything caught in 5–6 (reserve a week).
8. **Package** the customer-facing installer + quick-start guide (from `docs/INSTALL.md`), publish the trial policy.
9. **Pilot** with one real glass company; instrument crash-report upload before/after pilot.

---

## 20. FINAL VERDICT

### 🟠 NOT READY — SIGNIFICANT WORK REQUIRED

**Why (in 8 sentences):** The architecture, code quality, testing culture, and product thinking are demonstrably well above average for a solo team — the document chain is systemically sound, snapshots prevent data-rot, licensing is real, and there are 1,050+ meaningful tests. But the business definition of a glass-company ERP requires money to be trackable, and today there is no payment/receipt entity anywhere — only status flags — which makes Statements' "outstanding" a badge, not a number. The backup system would silently lose PI/DN attachment files on a restore. The auto-update pipeline has never demonstrably shipped a release. CI is inert by permission, not by design — nobody is currently verifying the 1,000 tests on every push. The Optimization module on the rail is a "coming soon" stub — fine, but it shouldn't promise itself on first-run tours. Net: the product is structurally ~80–85% towards a sellable first release (not the 35–40% of the older audit), but the remaining 15% is exactly the part customers pay for (money, attachments-safety, updates, and clean-machine confidence). Close the P0+P1 list — roughly 2–4 weeks of focused work — and this becomes "NEARLY READY" with a realistic pilot.

**Confidence overall:** 🟡 MEDIUM-HIGH for structure, 🟡 MEDIUM for runtime behaviour (the manual test list below is mandatory before quoting a date).

---

## MANUAL TEST REQUIRED (unverifiable by static analysis)

1. Clean Windows 10 + 11 VMs: install via Setup.exe, first run, license prompt, trial start.
2. Full chain by hand: Company → Client → Inquiry → PI (9 steps, SGU+DGU mix) → LPO approval (with PDF) → JO → 2 partial DNs → TI family (A/B invoices) → statement PDF.
3. Over-delivery attempt: deliver beyond ordered; confirm refusal message.
4. Restore backup onto a fresh machine: verify data AND attachments open.
5. Crash one write mid-way (kill process at DN→TI) → relaunch; confirm reconciler repairs links.
6. Apply an OTA update from a published release on the clean machine; confirm version bumps + DB migration backup.
7. Agent: mic-permissioned voice commands + one permissioned write (stock adjust) with audit entry; cloud provider with a real Gemini key; local model download.
8. License: expired key → read-only mode write attempt at one repository (confirm Demand throws + UI shows upgrade path); trial expiry behaviour.
9. 125%/150% DPI pass over the rail + PI wizard + statement wizard.
10. Performance: 10k PI line-items grid scroll/copy-paste; statement over 1k invoices; startup with 500 saved DNs.

*End of report — awaiting further instructions. No code was modified during this audit beyond the pre-existing working tree state (commit `400cfa8` was made before this audit began and contains only the build-break repair from this session, already pushed).*
