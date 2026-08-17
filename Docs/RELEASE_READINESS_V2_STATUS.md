# PROGLASS AUTOMATION — V2.0 RELEASE-READINESS STATUS

Current commit audited: `3f0a6f1` (+ `5699222` on this branch).
Codebase at audit: **502 C# files / 127 XAML files / 11 SQLite DbContexts**.

This document is a **current-state status snapshot** for V2.0. It does not
repeat the full prior audit (`Docs/ProductionReadinessReport.md`) — it
refreshes the status against the current code, records the fixes made in this
round, and gives a prioritized path to a publishable release.

---

## 0. Verification boundary (honest statement)

This environment has **no .NET SDK and no outbound network**, so `dotnet build`
and any runtime execution are **impossible here**. That is not new — every
previous round ran under the same constraint.

What was done instead:

- The pre-existing static gate (`tools/layout-verify/verify_all.sh`):
  **127 XAML well-formed, 0 stray attributes, 0 duplicate properties,
  0 property-element-order issues; 483 C# brace-balanced, 0 missing usings —
  ALL PASS.**
- Additional scans: Job Order resource keys (Dynamic + Static) all resolve;
  DataContext scan clean; ScrollViewer scan reports 2 low-severity findings.
- Every changed file re-read line-by-line; every referenced API verified to
  exist with the signature used.

What is **NOT** possible here and must be done on a Windows machine with the
SDK before publish:
Release build / compiler + nullable warnings, the runtime matrix (startup,
CRUD, documents, backup, shutdown), memory profiling, large-data performance,
printer/PDF failure, CSV/Excel import fuzzing, and packaged-MSIX behaviour.
These were also untested in every prior round.

---

## 1. Work completed in this round

### FIXED — Job Order list status/priority change lost the change (data integrity)

**Severity:** HIGH (release-critical).

**Location:**
`ViewModels/JobOrder/SavedJobOrdersList/SavedJobOrdersListViewModel.cs` and
`Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml.cs`.

**The bug.** The right-click context menu changed a job order's Status/Priority
by mutating **only the indexed column** (`entity.Status` / `entity.Priority`)
and saving. The embedded `FullSnapshotJson` blob was never updated. But three
consumers read those fields from the **snapshot**:

- `JobOrderDetailViewModel.PopulateDisplayFields` — reads `snapshot.Production.Status`
- the detail **save** path — `UpdateEntityFromSnapshot` writes the snapshot's
  status back into the column, so saving silently **reverted** the list change
- `JobOrderPdfLayout` / `JobOrderExcelLayout` — print the status/priority from
  `_snapshot.Production`

So a "quick" status change from the list was shown stale in the detail view and
**silently reverted on the next save**, and printed stale on the production
sheet. For a module that commits a factory to cutting glass, that is a genuine
data-integrity defect.

**The fix (2 files, +75/−48).**
1. The context menu now routes status/priority through the ViewModel commands
   instead of re-implementing load→set→save inline (removes duplicated logic —
   the code-behind had four inline copies of essentially the same update).
2. `ChangeStatusAsync` / `ChangePriorityAsync` now deserialize the snapshot,
   update `Production.Status`/`Priority` (plus `LastEditedAt/By`), re-serialize
   into `FullSnapshotJson`, and then save — so column and snapshot cannot
   diverge.
3. Added an audit entry for each status/priority transition, matching the
   app's existing audit convention (delete/create already audit).
4. The command accepts `Tuple<...>` / `ValueTuple<...>` shapes, mirroring the
   Proforma Invoice module's established `BUG-72` convention.

**Why not also "fix" Proforma Invoice the same way:** the PI module is *not*
affected the same way — its PDF/Excel layouts read
`snapshot.Company?.ProformaStatus ?? entity.Status`, i.e. they **fall back to
the indexed column**, so a stale snapshot cannot revert a list change there.
No change needed.

**Verification:** static gate passes; changed files re-read; APIs
(`JobOrderMapper.DeserializeSnapshot`/`SerializeSnapshot`, `AuditService.Log`,
`RelayCommand`, `Tuple`) confirmed to exist with matching signatures. Not
compiled — no SDK.

### FIXED — AI real-data egress is now opt-in, with a consent gate (privacy/Store)

**Severity:** HIGH (Microsoft Store privacy-policy blocker, previously open).

**Location:** `Services/AI/AIService/AiSettings.cs`,
`ViewModels/Settings/SettingsViewModel.cs`.

**The issue.** `AiSettings.UseRealData` defaulted to `true`, so with a
configured AI key the assistant sent a live summary containing real customer
names, project names and AED totals to a third-party LLM (Gemini / Groq /
Cohere) **by default**. The disclosure text was already honest, but the
behaviour was opt-out with no consent moment.

**The fix.**
1. `UseRealData` now defaults to **`false`** — real-data egress is opt-in.
2. Enabling it in Settings now requires an **explicit consent confirmation**
   dialog that names the recipients, what is sent, and that it leaves the
   machine; the toggle stays off unless the user confirms.
3. When disabled the assistant still works (learning/memory context only) —
   no customer PII or financial totals leave the device.
4. Existing users who already enabled it keep their explicit choice (the
   saved `config.json` value wins); only fresh installs / configs without the
   field default to off.

This removes the Store privacy blocker. Per-request consent or full alignment
with the `Agent/` LocalOnly-by-default model remains optional future work.

**Verification:** static gate passes; only `UseRealData` readers/writers are
the chat gate (`AiChatViewModel`, correct) and the settings toggle. Not
compiled — no SDK.

---

## 2. Remaining blockers before a commercial V2.0 publish

The following are the **genuinely still-open** items. They are a mix of
"needs runtime on Windows" and "needs an owner decision" — none can be closed
by more static analysis here.

### A. Runtime / build verification on Windows (blocker)
- A **Release build** has never been produced in any environment available to
  this project's audits. Compiler + nullable + analyzer warnings are
  **unverified**.
- The full runtime matrix (startup → CRUD → documents → backup/restore →
  shutdown), memory profiling, and large-data (10k-row) performance have never
  been executed. The 59+ security test cases in `Tests/Agent/` have **never
  been run**.
- **Action:** run a Release build, then the documented phase matrix on a
  Windows machine with the SDK. This is the single highest-priority item.

### B. Data architecture — 11 separate SQLite databases (architectural)
The PI→JO→DN→TI→Statement chain is stored across 11 SQLite files and cannot be
committed atomically (`D-4`). A power loss / End-task mid-save can leave a
partially-linked chain. There is a close-during-write guard and the failure is
logged + audited, but the architectural limit remains. **Consolidation is a
planned, separate project** — do not attempt during a patch round.

### C. AI egress (Store blocker) — **RESOLVED** (see §1)
`AiSettings.UseRealData` now defaults to **`false` (opt-in)** instead of `true`,
and the Settings screen requires an **explicit consent confirmation** before
the user can enable real-data access. When disabled, the assistant still works
but only receives learning/memory context (no customer PII or AED totals leave
the machine). Existing users who already opted in keep their choice; only fresh
installs / configs without the field default to off. The disclosure text
naming the recipients (Gemini / Groq / Cohere) and what is sent was already in
place. This resolves the Microsoft Store privacy-policy blocker. A per-request
consent or a full alignment with the `Agent/` LocalOnly-by-default model
remains an optional future refinement, not a release blocker.

### D. `UpdateService` origin ≠ authenticity
The updater restricts the download to a GitHub HTTPS allowlist but does not
**signature-verify** the payload. A compromised release asset would still
install. Recommend Velopack (already referenced in the project) for signed
updates, and disable the updater entirely when packaged for the Store.

### E. Latent decimal storage affinity (`D-6`)
Four money columns are declared `decimal(18,2)`/`HasPrecision(18,2)`, giving
NUMERIC affinity (REAL storage) on SQLite instead of the exact TEXT storage the
other 19 money columns use. **No SQL-side aggregation currently runs against
them, so no value is corrupted today.** A fix is a column-type migration on
live data and must not be done blind — needs a migration, backup and verified
round-trip on real data, which cannot be executed here.

### F. Diagnosability debt (`R-1` recurrence)
~90 `catch` blocks still do nothing observable in a Release build except
`Debug.WriteLine` (compiled out). Most are low-value (cosmetic refreshes,
optional lookups) and already surface errors by other means. This is a
consistency debt, not 90 hidden crashes — convert the remaining
business-significant ones opportunistically, but do not block the release on it.

---

## 3. Confirmed healthy (no action)

- **Secrets:** no hard-coded credentials beyond the documented public
  default; DPAPI `CurrentUser` for API keys at rest; no plaintext fallback.
- **SQL injection:** 12 raw-SQL statements; only 2 interpolate and both use
  compile-time literals only — no user input reaches an identifier.
- **TLS:** no `http://`, no cert-bypass callbacks, no protocol overrides.
- **Authorization:** `OperationGuard.Demand` on all 34 mutating repository
  methods across 7 repositories; fails closed. Job Order repository enforces
  `JobOrderEdit` (insert/update) and `JobOrderDelete` consistently with the UI.
- **Authentication boundary (local):** a sign-in window runs before
  `MainWindow`; PBKDF2 hashing with legacy auto-migration; uniform error text
  blocks username enumeration; 3-attempt/15s in-memory lockout.
- **Crash handling:** `AppDomain.UnhandledException`,
  `TaskScheduler.UnobservedTaskException`, `DispatcherUnhandledException`, and a
  startup guard that surfaces corrupt/locked DB cleanly instead of a raw OS
  crash.
- **Static-event subscriptions:** all owned by app-lifetime singletons —
  navigation does not create new subscribers, so no unbounded leak.
- **Virtualization:** list hosts run `CanContentScroll=True` + `ScrollUnit`
  resolved by `SmoothScrollBehavior`, so virtualization and smooth scroll work
  together (PERF-1 mechanism).
- **Licence:** QuestPDF set to Community (eligible < USD 1M revenue);
  ClosedXML/CsvHelper/etc. are permissive. No CVE lookup possible offline.
- **Resource integrity (Job Order):** every Dynamic + Static resource key used
  by the module resolves; duplicate-property / attribute / element-order scans
  clean.

---

## 4. Prioritized action list

| # | Priority | Item | Owner | When |
|---|----------|------|-------|------|
| 1 | BLOCKER | Release build + runtime matrix on Windows | dev + QA | before any publish |
| 2 | ~~BLOCKER (Store)~~ **DONE** | AI egress: default-off + consent prompt implemented (§6) | product owner | ✅ resolved |
| 3 | HIGH | Signature-verified or Velopack updater; disable self-update when packaged | dev | before Store |
| 4 | MEDIUM | D-6 decimal affinity migration (with backup + round-trip test) | dev | scheduled |
| 5 | MEDIUM | D-4 DB consolidation project | architecture | planned |
| 6 | LOW | Convert remaining business-significant silent catches | dev | opportunistic |
| 7 | LOW | Two ScrollViewer `HorizontalScrollBarVisibility="Auto"` wrapping-inert findings | dev | cosmetic |

**Packaging (MSIX/installer) is intentionally last** — it should not begin
until items 1–3 are resolved.

---

## 5. Status

**CONDITIONALLY READY — blocked by verification, not by known code defects.**
No known critical crash, security hole, or data-loss path was found in this
round's audit. The correctness defect (Job Order status/priority divergence)
and the Store privacy blocker (AI real-data egress) are both fixed. What
separates this from "ready" is that **no Release build and no runtime pass has
ever been executed in any environment this project has been audited in**, plus
the remaining owner decisions in §2B/D.

The correct next step is a Windows Release build and the runtime matrix — not
more static analysis, and not packaging.
