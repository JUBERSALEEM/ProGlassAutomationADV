// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Specifications/SpecificationLedgerService.cs
// PROJECT: ProGlassAutomation
// MODULE: Specifications — ledger ground truth from saved invoices
//
// THE PROBLEM THIS SOLVES
// ───────────────────────
// Specifications used to be counted where they were BUILT (Step 4 APPLY),
// not where they were USED (a saved invoice's Step 5 groups). Every APPLY
// press bumped UsageCount and appended a usage row — including APPLYs on
// drafts that were never saved, and the silent auto-save on Step 4
// navigation. Nothing ever recorded actual Step 5 use (MarkAsUsedAsync
// had zero call sites), and deleting a PI left its spec rows behind. The
// Specifications Center therefore showed counts and PI numbers for
// invoices that do not exist.
//
// GROUND TRUTH, STATED ONCE
// ───────────────────────
//   • The reusable LIBRARY (SavedSpecification rows: name, rate, catalogue
//     details) is still written by Step 4 — the Step 5 dropdown MUST be
//     able to offer a spec before its invoice is saved. That write path
//     stays, but it no longer touches counts.
//   • COUNTS, HISTORY and PROVENANCE (UsageCount, LastUsedAt, ProformaId,
//     SpecificationUsages rows) are DERIVED here, from SAVED proforma
//     snapshots' Step 5 groups ONLY. 2 PIs × 2 specs = 4 verified uses; a
//     third PI reusing the same specs increments them; a Step-4-only
//     estimate increments nothing.
//   • PricePerSqm stays estimator-owned: the ledger never touches the
//     catalogue rate (a fresh Step 4 estimate must not be clobbered by an
//     old saved rate). Per-invoice quoted rates live on the usage rows,
//     which is what makes the price bands truthful.
//
// WHEN IT RUNS
// ───────────────────────
//   • At startup (Start → initial ReconcileAsync on a background thread),
//     which heals every existing install's inflated counts on first launch.
//   • After every invoice write/delete, via ProformaInvoiceRepository's
//     SavedInvoicesChanged signal (debounced ~1.5s so a save + stamp +
//     version burst reconciles once).
//   • On demand, from the Center's Refresh button.
// Single-flight with re-run coalescing: overlapping triggers queue one
// follow-up pass instead of racing.
//
// WHAT IT PURGES, AND WHAT IT KEEPS
// ───────────────────────
//   • PURGED: usage rows for PIs that no longer exist (deleted, or APPLYs
//     on drafts that were never saved — the "(unsaved draft)" rows), and
//     rows for groups a PI no longer carries after an edit. These are
//     DERIVED data: re-derivable at any time, so deleting them loses no
//     fact — it removes fiction. The purge is audit-logged with counts.
//   • KEPT: catalogue rows with zero verified uses (the estimator's
//     working library — Step 4/Step 5 need them; the Center labels them
//     "not on any saved invoice"), and all audit/version history (the
//     legitimate restore-stage need, which this service never touches).
//
// NEVER THROWS. A ledger failure is logged; the Center keeps showing the
// last verified state rather than taking the app down.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Specifications
{
    /// <summary>
    /// The most recent verified specification use, for the Center's last-
    /// activity card. Company/customer come from the saved invoice row —
    /// authoritative, not from the draft-time context Step 4 saw.
    /// </summary>
    public sealed class SpecLastActivity
    {
        public string SpecName { get; set; } = "";
        public string GlassType { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public DateTime UsedAt { get; set; }

        public string UsedAtDisplay =>
            UsedAt == default ? "—" : UsedAt.ToString("dd-MMM-yyyy HH:mm");

        /// <summary>"PI-2026-001 · ProGlass LLC", collapsing blanks.</summary>
        public string InvoiceContextDisplay
        {
            get
            {
                bool hasPi = !string.IsNullOrWhiteSpace(ProformaId);
                bool hasCompany = !string.IsNullOrWhiteSpace(CompanyName);
                if (hasPi && hasCompany) return $"{ProformaId} · {CompanyName}";
                if (hasPi) return ProformaId;
                if (hasCompany) return CompanyName;
                return "—";
            }
        }
    }

    /// <summary>
    /// Derives specification counts and history from saved invoices.
    /// Static by design, matching the other lifecycle services
    /// (ProformaLifecycleSync, InquiryLinkSync): one process-wide ledger,
    /// no per-view state, safe to call from any thread.
    /// </summary>
    public static class SpecificationLedgerService
    {
        private static readonly SemaphoreSlim _gate = new(1, 1);
        private static bool _rerunRequested;
        private static bool _started;
        private static readonly object _debounceLock = new();
        private static CancellationTokenSource? _debounceCts;

        private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(1500);

        /// <summary>
        /// Subscribes to invoice writes and runs the initial heal on a
        /// background thread. Called once from StartupCore after the
        /// databases are initialised. Never throws, never blocks.
        /// </summary>
        public static void Start()
        {
            if (_started) return;
            _started = true;

            try
            {
                ProformaInvoiceRepository.SavedInvoicesChanged += OnInvoicesChanged;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SpecLedger.Start] subscribe failed: {ex.Message}");
            }

            AsyncUtility.SafeFireAndForget(
                () => ReconcileAsync(),
                "Specification ledger startup reconcile failed");
        }

        /// <summary>Unsubscribes. Called once from App.OnExit.</summary>
        public static void Stop()
        {
            _started = false;

            try
            {
                ProformaInvoiceRepository.SavedInvoicesChanged -= OnInvoicesChanged;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SpecLedger.Stop] detach failed: {ex.Message}");
            }

            lock (_debounceLock)
            {
                try { _debounceCts?.Cancel(); }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[SpecLedger.Stop] cancel failed: {ex.Message}");
                }
                try { _debounceCts?.Dispose(); }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[SpecLedger.Stop] dispose failed: {ex.Message}");
                }
                _debounceCts = null;
            }
        }

        private static void OnInvoicesChanged()
        {
            // Debounce: one save raises several change signals (insert,
            // metadata stamp, version write). Reconcile once, shortly after
            // the burst ends, on a background thread.
            lock (_debounceLock)
            {
                try { _debounceCts?.Cancel(); }
                catch (Exception cancelEx)
                {
                    AppLogService.Debug($"[SpecLedger] debounce cancel failed: {cancelEx.Message}");
                }
                try { _debounceCts?.Dispose(); }
                catch (Exception disposeEx)
                {
                    AppLogService.Debug($"[SpecLedger] debounce dispose failed: {disposeEx.Message}");
                }

                var cts = new CancellationTokenSource();
                _debounceCts = cts;

                Task.Delay(DebounceDelay, cts.Token).ContinueWith(t =>
                {
                    if (!t.IsCanceled)
                        AsyncUtility.SafeFireAndForget(
                            () => ReconcileAsync(),
                            "Specification ledger reconcile failed");
                }, TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Full reconcile: derive counts + history from saved invoices.
        /// Single-flight — overlapping triggers coalesce into one
        /// follow-up pass instead of racing each other.
        /// </summary>
        public static async Task ReconcileAsync()
        {
            if (!await _gate.WaitAsync(0).ConfigureAwait(false))
            {
                _rerunRequested = true;
                return;
            }

            try
            {
                do
                {
                    _rerunRequested = false;
                    await ReconcileCoreAsync().ConfigureAwait(false);
                }
                while (_rerunRequested);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Most recent verified use across all specifications, or null when
        /// no saved invoice carries a spec yet. Always reads live data.
        /// </summary>
        public static async Task<SpecLastActivity?> GetLastActivityAsync()
        {
            try
            {
                using var db = new SpecificationsDbContext();
                var row = await db.SpecificationUsages
                    .AsNoTracking()
                    .OrderByDescending(u => u.UsedAt)
                    .ThenByDescending(u => u.Id)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (row == null) return null;

                return new SpecLastActivity
                {
                    SpecName = row.SpecName ?? "",
                    GlassType = row.GlassType ?? "",
                    ProformaId = row.ProformaId ?? "",
                    CompanyName = row.CompanyName ?? "",
                    CustomerName = row.CustomerName ?? "",
                    UsedAt = row.UsedAt
                };
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SpecLedger.LastActivity] {ex.Message}");
                return null;
            }
        }

        // ── Core ─────────────────────────────────────────────────────

        /// <summary>One verified (PI, Step-5 group) occurrence.</summary>
        private sealed class VerifiedUse
        {
            public string GlassType { get; set; } = "";
            public string SpecName { get; set; } = "";
            public string ProformaId { get; set; } = "";
            public string CompanyName { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public string ProjectName { get; set; } = "";
            public decimal PricePerSqm { get; set; }
            public int SheetWidthMm { get; set; }
            public int SheetHeightMm { get; set; }
            public DateTime PiStamp { get; set; }
        }

        private static async Task ReconcileCoreAsync()
        {
            var invoicesRepo = App.Invoices;
            if (invoicesRepo == null) return;

            List<SavedProformaInvoice> invoices;
            try
            {
                invoices = await invoicesRepo.GetAllAsync().ConfigureAwait(false)
                           ?? new List<SavedProformaInvoice>();
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SpecLedger] invoice read failed: {ex.Message}");
                return;
            }

            var savedIds = new HashSet<string>(
                invoices.Select(i => (i.ProformaId ?? "").Trim())
                        .Where(id => id.Length > 0),
                StringComparer.OrdinalIgnoreCase);

            // Catalogue snapshot for type adoption (a saved group whose type
            // is blank/unknown adopts the type of the catalogue row with the
            // same name, so the ledger never forks "SGU/X" and "/X").
            Dictionary<string, string> nameToType;
            try
            {
                var catalog = await App.Specs.GetAllAsync().ConfigureAwait(false)
                              ?? new List<SavedSpecification>();
                nameToType = catalog
                    .Where(s => !string.IsNullOrWhiteSpace(s.SpecName))
                    .GroupBy(s => s.SpecName.Trim(),
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(s => (s.GlassType ?? "").Trim())
                              .FirstOrDefault(t => t.Length > 0) ?? "",
                        StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SpecLedger] catalogue read failed: {ex.Message}");
                return;
            }

            // ── 1. Derive verified uses from saved snapshots ─────────
            var verified = new List<VerifiedUse>();
            foreach (var inv in invoices)
            {
                string piId = (inv.ProformaId ?? "").Trim();
                if (piId.Length == 0) continue;

                ProformaInvoiceSnapshot snap;
                try
                {
                    // Never throws (bad JSON → empty snapshot), but the
                    // ledger trusts nothing: one corrupt invoice must not
                    // abort the whole pass.
                    snap = ProformaInvoiceMapper.DeserializeSnapshot(inv);
                }
                catch (Exception snapEx)
                {
                    AppLogService.Debug(
                        $"[SpecLedger] snapshot unreadable for {piId}: {snapEx.Message}");
                    continue;
                }

                var groups = snap.SpecGroups;
                if (groups == null || groups.Count == 0) continue;

                DateTime stamp = inv.UpdatedAt != default ? inv.UpdatedAt
                    : inv.CreatedAt != default ? inv.CreatedAt
                    : DateTime.Now;

                foreach (var g in groups)
                {
                    if (g == null) continue;
                    string name = (g.SelectedSpecName ?? "").Trim();
                    if (name.Length == 0) continue;
                    if (name.Length > 500) name = name[..500]; // column cap

                    verified.Add(new VerifiedUse
                    {
                        GlassType = ResolveGlassType(
                            g.SelectedGlassType, name, nameToType),
                        SpecName = name,
                        ProformaId = piId,
                        CompanyName = Truncate(inv.CompanyName, 300),
                        CustomerName = Truncate(inv.CustomerName, 300),
                        ProjectName = Truncate(inv.ProjectName, 300),
                        PricePerSqm = g.BaseRate,
                        SheetWidthMm = Math.Max(0, g.OuterSheetWidthMm),
                        SheetHeightMm = Math.Max(0, g.OuterSheetHeightMm),
                        PiStamp = stamp
                    });
                }
            }

            // ── 2. Rewrite the usage ledger (multiset diff) ──────────
            int purgedOrphans = 0;
            int removedStale = 0;
            int inserted = 0;

            // Final per-spec timestamps/counts, computed from preserved +
            // new rows (NOT re-read: the pass must be one transaction).
            var finalUseTimes = new Dictionary<string, List<DateTime>>(
                StringComparer.OrdinalIgnoreCase);
            var finalUsePis = new Dictionary<string, List<(DateTime At, string Pi)>>(
                StringComparer.OrdinalIgnoreCase);

            try
            {
                using var db = new SpecificationsDbContext();
                var stored = await db.SpecificationUsages
                    .ToListAsync().ConfigureAwait(false);
                var catalogRows = await db.Specifications
                    .ToListAsync().ConfigureAwait(false);

                // 2a. Purge rows for PIs that do not exist: deleted invoices
                // AND never-saved drafts (blank PI). Derived fiction.
                var orphans = stored
                    .Where(u => string.IsNullOrWhiteSpace(u.ProformaId)
                        || !savedIds.Contains(u.ProformaId.Trim()))
                    .ToList();
                if (orphans.Count > 0)
                {
                    db.SpecificationUsages.RemoveRange(orphans);
                    purgedOrphans = orphans.Count;
                }

                var live = stored.Except(orphans).ToList();

                // 2b. Multiset diff per (PI, type, name, price): a PI can
                // carry the same spec twice at different rates, so matching
                // is by multiplicity, not by key-set.
                var want = verified
                    .GroupBy(UsageKey)
                    .ToDictionary(g => g.Key, g => g.ToList());
                var have = live
                    .GroupBy(u => UsageKey(
                        u.ProformaId ?? "", u.GlassType ?? "",
                        u.SpecName ?? "", u.PricePerSqm))
                    .ToDictionary(g => g.Key, g => g.ToList());

                var allKeys = new HashSet<string>(want.Keys);
                foreach (var k in have.Keys) allKeys.Add(k);

                foreach (string key in allKeys)
                {
                    want.TryGetValue(key, out var wantRows);
                    have.TryGetValue(key, out var haveRows);
                    wantRows ??= new List<VerifiedUse>();
                    haveRows ??= new List<SpecificationUsage>();

                    int keep = Math.Min(wantRows.Count, haveRows.Count);

                    // Kept rows: preserve first-quoted UsedAt, refresh the
                    // labels (the PI may have been edited since).
                    for (int i = 0; i < keep; i++)
                    {
                        var row = haveRows[i];
                        var truth = wantRows[i];
                        row.CompanyName = truth.CompanyName;
                        row.CustomerName = truth.CustomerName;
                        row.ProjectName = truth.ProjectName;
                        row.SheetWidthMm = truth.SheetWidthMm;
                        row.SheetHeightMm = truth.SheetHeightMm;
                        if (row.SpecificationId <= 0)
                            row.SpecificationId = FindSpecId(catalogRows, row);
                        TrackFinal(finalUseTimes, finalUsePis, truth, row.UsedAt);
                    }

                    // Surplus stored rows: the PI no longer carries this
                    // (group removed, renamed or repriced).
                    for (int i = keep; i < haveRows.Count; i++)
                    {
                        db.SpecificationUsages.Remove(haveRows[i]);
                        removedStale++;
                    }

                    // Missing rows: newly quoted (or never recorded).
                    for (int i = keep; i < wantRows.Count; i++)
                    {
                        var truth = wantRows[i];
                        var row = new SpecificationUsage
                        {
                            SpecificationId = 0, // backfilled after catalogue sync below
                            SpecName = truth.SpecName,
                            GlassType = truth.GlassType,
                            ProformaId = truth.ProformaId,
                            CompanyName = truth.CompanyName,
                            CustomerName = truth.CustomerName,
                            ProjectName = truth.ProjectName,
                            PricePerSqm = truth.PricePerSqm,
                            SheetWidthMm = truth.SheetWidthMm,
                            SheetHeightMm = truth.SheetHeightMm,
                            UserName = Environment.UserName,
                            UsedAt = truth.PiStamp
                        };
                        db.SpecificationUsages.Add(row);
                        inserted++;
                        TrackFinal(finalUseTimes, finalUsePis, truth, truth.PiStamp);
                    }
                }

                // 2c. Catalogue counts from verified truth. PricePerSqm is
                // DELIBERATELY untouched (estimator-owned — see class docs).
                int healedCounts = 0;
                int createdCatalog = 0;
                var bySpec = verified
                    .GroupBy(v => SpecKey(v.GlassType, v.SpecName))
                    .ToDictionary(g => g.Key, g => g.ToList());
                var seenSpecs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in catalogRows)
                {
                    string key = SpecKey(row.GlassType ?? "", row.SpecName ?? "");
                    seenSpecs.Add(key);
                    if (!bySpec.TryGetValue(key, out var uses) || uses.Count == 0)
                    {
                        // Not on any saved invoice: draft or import. The row
                        // STAYS (the estimator's library), but its counts
                        // and provenance are zeroed — no phantom PIs.
                        if (row.UsageCount != 0
                            || row.LastUsedAt != default
                            || !string.IsNullOrWhiteSpace(row.ProformaId))
                        {
                            row.UsageCount = 0;
                            row.LastUsedAt = default;
                            row.ProformaId = null;
                            healedCounts++;
                        }
                        continue;
                    }

                    var times = finalUseTimes.TryGetValue(key, out var t) ? t : new List<DateTime>();
                    var pis = finalUsePis.TryGetValue(key, out var p) ? p : new List<(DateTime, string)>();
                    DateTime lastUsed = times.Count > 0 ? times.Max() : DateTime.Now;
                    string latestPi = pis.Count > 0
                        ? pis.OrderByDescending(x => x.At).First().Pi
                        : uses[0].ProformaId;

                    if (row.UsageCount != uses.Count
                        || row.LastUsedAt != lastUsed
                        || !string.Equals(row.ProformaId ?? "", latestPi,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        row.UsageCount = uses.Count;
                        row.LastUsedAt = lastUsed;
                        row.ProformaId = latestPi;
                        healedCounts++;
                    }
                }

                // Specs quoted on saved invoices but missing from the
                // catalogue (typed freehand in Step 5, or the catalogue row
                // was deleted): recreate from verified truth so the Center
                // and the Step 5 dropdown agree on what exists.
                foreach (var (key, uses) in bySpec)
                {
                    if (seenSpecs.Contains(key)) continue;

                    var latest = uses.OrderByDescending(u => u.PiStamp).First();
                    var (color, thickness) = ParseOuterPane(latest.SpecName);
                    var times = finalUseTimes.TryGetValue(key, out var t) ? t : new List<DateTime>();

                    var created = new SavedSpecification
                    {
                        GlassType = latest.GlassType,
                        SpecName = latest.SpecName,
                        PricePerSqm = latest.PricePerSqm,
                        CreatedAt = DateTime.Now,
                        LastUsedAt = times.Count > 0 ? times.Max() : latest.PiStamp,
                        UsageCount = uses.Count,
                        Color = color,
                        ThicknessMm = thickness,
                        SheetWidthMm = latest.SheetWidthMm,
                        SheetHeightMm = latest.SheetHeightMm,
                        ProformaId = latest.ProformaId
                    };
                    db.Specifications.Add(created);
                    createdCatalog++;
                }

                await SqliteResilience.SaveWithRetryAsync(
                    _ => db.SaveChangesAsync()).ConfigureAwait(false);

                // Backfill SpecificationId on the rows inserted above (the
                // catalogue Ids only exist after SaveChanges). Best-effort
                // second write; the ledger never fails a pass on it.
                try
                {
                    var fresh = await db.Specifications
                        .AsNoTracking()
                        .ToListAsync().ConfigureAwait(false);
                    var orphansBySpec = await db.SpecificationUsages
                        .Where(u => u.SpecificationId <= 0)
                        .ToListAsync().ConfigureAwait(false);
                    bool touched = false;
                    foreach (var u in orphansBySpec)
                    {
                        int id = FindSpecId(fresh, u);
                        if (id > 0) { u.SpecificationId = id; touched = true; }
                    }
                    if (touched)
                        await SqliteResilience.SaveWithRetryAsync(
                            _ => db.SaveChangesAsync()).ConfigureAwait(false);
                }
                catch (Exception backfillEx)
                {
                    AppLogService.Debug(
                        $"[SpecLedger] spec-id backfill skipped: {backfillEx.Message}");
                }

                // 2d. Notify + audit (audit only when something changed —
                // a reconcile that heals nothing is not worth a trail row).
                if (purgedOrphans > 0 || removedStale > 0 || inserted > 0
                    || healedCounts > 0 || createdCatalog > 0)
                {
                    try
                    {
                        AuditService.Log(
                            AuditSections.Specifications, "Ledger reconciled",
                            $"{verified.Count} verified uses across {savedIds.Count} saved invoices — " +
                            $"{purgedOrphans} orphan rows purged, " +
                            $"{removedStale} stale rows removed, " +
                            $"{inserted} uses recorded, " +
                            $"{healedCounts} catalogue counts healed, " +
                            $"{createdCatalog} catalogue rows recreated.",
                            "SPEC", "");
                    }
                    catch (Exception auditEx)
                    {
                        // Audit must never fail the ledger — but the miss
                        // is still worth a debug line.
                        AppLogService.Debug(
                            $"[SpecLedger] audit skipped: {auditEx.Message}");
                    }
                }

                try { SpecCatalogNotifier.NotifyChanged(); }
                catch (Exception notifyEx)
                {
                    AppLogService.Debug(
                        $"[SpecLedger] notify failed: {notifyEx.Message}");
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Specification ledger reconcile failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Specifications",
                        Operation = "Reconcile"
                    });
            }
        }

        // ── Helpers ────────────────────────────────────────────────

        private static void TrackFinal(
            Dictionary<string, List<DateTime>> times,
            Dictionary<string, List<(DateTime At, string Pi)>> pis,
            VerifiedUse truth, DateTime usedAt)
        {
            string key = SpecKey(truth.GlassType, truth.SpecName);
            if (!times.TryGetValue(key, out var t))
            {
                t = new List<DateTime>();
                times[key] = t;
            }
            t.Add(usedAt);

            if (!pis.TryGetValue(key, out var p))
            {
                p = new List<(DateTime, string)>();
                pis[key] = p;
            }
            p.Add((usedAt, truth.ProformaId));
        }

        private static int FindSpecId(
            IEnumerable<SavedSpecification> catalog, SpecificationUsage u) =>
            catalog.FirstOrDefault(s =>
                string.Equals(s.GlassType ?? "", u.GlassType ?? "",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.SpecName ?? "", u.SpecName ?? "",
                    StringComparison.OrdinalIgnoreCase))?.Id ?? 0;

        private static string SpecKey(string glassType, string specName) =>
            (glassType ?? "").Trim().ToUpperInvariant()
            + "\0" + (specName ?? "").Trim();

        private static string UsageKey(VerifiedUse v) =>
            UsageKey(v.ProformaId, v.GlassType, v.SpecName, v.PricePerSqm);

        private static string UsageKey(
            string proformaId, string glassType, string specName, decimal price) =>
            (proformaId ?? "").Trim() + "\0"
            + (glassType ?? "").Trim().ToUpperInvariant() + "\0"
            + (specName ?? "").Trim() + "\0"
            + price.ToString("0.00");

        private static string ResolveGlassType(
            string? storedType, string specName,
            Dictionary<string, string> nameToType)
        {
            string type = (storedType ?? "").Trim().ToUpperInvariant();
            if (IsKnownType(type)) return type;

            // Blank or unrecognised: read the build from the name (same
            // grammar Step 5 and Step 9 trust), else adopt the catalogue's
            // type for this name so the ledger never forks one spec into two
            // rows. "" only when nothing anywhere can say — honest unknown.
            try
            {
                string detected = GlassTypeDetector.Detect(specName);
                if (!string.Equals(detected, "Unknown",
                        StringComparison.OrdinalIgnoreCase))
                    return detected;
            }
            catch (Exception detectEx)
            {
                AppLogService.Debug($"[SpecLedger] type detect failed: {detectEx.Message}");
            }

            if (nameToType.TryGetValue(specName, out string? adopted)
                && !string.IsNullOrWhiteSpace(adopted))
                return adopted.Trim().ToUpperInvariant();

            return "";
        }

        private static bool IsKnownType(string type) =>
            string.Equals(type, "SGU", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "DGU", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "LAM", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "DGU+LAM", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Outer pane colour/thickness from a spec name, for catalogue rows
        /// the ledger recreates (the Center's colour filter needs them).
        /// Same parse Step 4's catalogue details use; blank when unreadable.
        /// </summary>
        private static (string? Color, int ThicknessMm) ParseOuterPane(string specName)
        {
            try
            {
                var panes = SpecPaneResolver.Parse(specName);
                if (panes.Count == 0) return (null, 0);
                return (panes[0].Color, panes[0].ThicknessMm);
            }
            catch
            {
                return (null, 0);
            }
        }

        private static string Truncate(string? value, int maxLen)
        {
            if (string.IsNullOrEmpty(value)) return "";
            value = value.Trim();
            return value.Length <= maxLen ? value : value[..maxLen];
        }
    }
}
