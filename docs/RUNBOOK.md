# 🧯 ProGlass Automation — Runbook & Incident Response

**Version:** Phase 10 — 2026-08-21
**Audience:** operator / support / developer-on-call

This runbook pairs with the observability + backup + update-safety work in
Phase 10. It answers "what do I look at first" when a user reports a
problem, and how to recover from the failures that matter most for a
glass-production ERP.

---

## 1. Every issue starts with the Correlation ID

A single business operation (PI → JO → DN → TI) carries one
`CORR-XXXXXX` (or `CORR-TAXINVOICE-XXXXXX-XXXXXX` for document ops) that
flows through three places:

| Where | What it looks like |
|---|---|
| App log | `%AppData%\ProGlassAutomation\logs\app-yyyyMMdd.log` — every line for the operation carries the ID |
| Activity log | in-app → Activity (section per module) — user-visible audit trail |
| Error dialog | the `(ERR-XXXXXX)` shown to the user maps to the log line |

**Triage order:**
1. Ask the user for the exact error ID / correlation ID in the dialog.
2. `grep` (or search the Diagnostics log viewer) the app log for that ID.
3. Read the lines from `op:…` start through the failure — they show which
   step of the multi-DB write failed.

The PI→JO→DN→TI span trace (`Services/Observability/DocumentPipelineTracer`)
mirrors the same correlation, so an OpenTelemetry exporter (if attached)
shows the identical journey.

---

## 2. Incident: "invoice saved but the delivery note still shows not invoiced"

This is the classic **D-4 cross-DB inconsistency** (TI in `taxinvoices.db`,
DN flag in `deliverynotes.db` — SQLite has no cross-file transaction).

**What already protects you (automatic):**
- `DocumentOperationCoordinator.SaveTaxInvoiceWithDnLinkAsync` marks DNs
  and verifies the write; on failure it logs `CRITICAL D-4` with the
  correlation id and relies on the reconciler.
- `TaxInvoiceDnReconciler.ReconcileAsync` runs at startup and heals the
  mismatch idempotently.

**Manual check:**
```text
log line contains:  CRITICAL D-4: TI <id> saved but DN marking failed
```

**Recovery:** restart the app (startup sweep re-reconciles). If it still
shows, open the Tax Invoice and re-save — the coordinator is idempotent.

---

## 3. Incident: "restore didn't work / backup is corrupt"

**Prevention (run the drill — Phase 10):**
A backup is only a backup if you have restored it. Run the restore drill:

- Settings → Backups → **Run restore drill** (calls
  `RestoreDrillService.RunDrill()`), or
- programmatically: `RestoreDrillService.RunDrill()`.

The drill creates a fresh backup set, restores it into an isolated scratch
folder, verifies every file byte-for-byte (SHA-256), runs
`PRAGMA integrity_check` on every SQLite file, then cleans up. It never
touches live data, so it is safe on a 1 GB database.

**What the result means:**
- `IsClean == true` → the newest backup set is restorable.
- `IsClean == false` → read the per-DB `IntegrityResults`; a failed DB is
  corrupt **before you need it** — create a new set and investigate disk.

**Restore procedure (manual):**
1. Settings → Backups → pick a set → Restore.
2. `BackupService.RestoreBackupSet` takes a safety snapshot first
   (`pre-restore safety`), so restore is always undoable.
3. Restart the app to load the restored data.

**Retention:** 10 backup sets, auto-pruned; auto-backup throttled to one
per hour (never one per exit).

---

## 4. Incident: "update won't install / was blocked"

The updater now verifies the Authenticode signature of the downloaded
installer (signature **and** file hash via WinVerifyTrust) before install.

**What each state means:**
- `UPDATE_SIGNATURE_INVALID` (audit log) → the file failed signature
  verification and was deleted. The release was either unsigned or
  tampered with. **Do not bypass** — get the publisher to sign the release
  (see §5).
- `UPDATE_INSTALL_BLOCKED_SIGNATURE` → the file was re-verified at launch
  and failed (possibly swapped between download and install).
- `UPDATE_DOWNLOAD_BLOCKED` → the asset URL was not HTTPS-from-GitHub.

**Recovery:** re-check for updates; if the publisher has not signed the
release, the safe path is manual installation from the GitHub release page
with the publisher's confirmation.

---

## 5. Release checklist (before tagging)

```bash
# 1) bump <Version> in ProGlassAutomation.csproj (single source of truth)
# 2) CI green:  https://github.com/<owner>/ProGlassAutomationADV/actions
# 3) configure code-signing secrets (Settings → Secrets):
#      CODESIGN_PFX_BASE64   (base64 of the .pfx)
#      CODESIGN_PFX_PASSWORD
# 4) tag + push (this triggers the release workflow — Docs/CI/release.yml,
#    copy to .github/workflows/release.yml once to activate)
git tag v2.0.1 && git push origin v2.0.1
```

The release workflow signs every `.exe`/`.dll`, packages Velopack delta
updates, and publishes the GitHub Release. Users receive it on their next
startup check (once/day) or via Settings → Updates → Check.

---

## 6. Log locations quick reference

| Data | Path |
|---|---|
| App technical log | `%AppData%\ProGlassAutomation\logs\app-yyyyMMdd.log` (14-day retention) |
| Backups | `%AppData%\ProGlassAutomation\Backups\set_yyyy-MM-dd_HH-mm-ss\` |
| Live data | `%AppData%\ProGlassAutomation\*.db` + `config.json` |
| Update history | in-app Settings → Updates (backed by `UpdateHistoryService`) |

---

## 7. Escalation

- **Data inconsistency (cross-DB):** reconciler + coordinator (see §2).
- **Corrupt backup:** restore drill (see §3).
- **Update blocked:** signature verification (see §4) — never bypass it.
- **Performance:** Phase 9 services — `SpecificationMatcherCache`,
  `ReportingCacheService`, `IndexingService` (30 indexes across 6 DBs).
- **Unknown:** capture the correlation id, the app log lines, and the
  audit trail, and escalate with all three.
