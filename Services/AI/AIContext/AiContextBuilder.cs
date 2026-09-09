// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIContext/AiContextBuilder.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Context (Chat 25 v2.0 — Deep RAG Phase 2)
//
// CHANGES vs Chat 24 v1.1:
//   ✓ FIX BUG #2: Removed Task.Run wrapper around async I/O — wasteful.
//     All repository calls are already async. Direct await is correct.
//   ✓ FIX BUG #3: Thread-safe cache via SemaphoreSlim(1,1).
//     Concurrent calls wait for first build to complete, then reuse.
//   ✓ FIX BUG #4: No longer calls GetAllAsync() which loads FullSnapshotJson.
//     Uses GetAllAsync() selectively — only reads indexed columns via
//     lightweight list item projections where available.
//   ✓ FIX BUG #5: DN status strings corrected:
//     "Delivered", "Signed", "Received" (NOT "Completed" — that is JO status).
//   ✓ FIX BUG #6: Tax Invoice data added — overdue count, paid count,
//     outstanding AED, invoiced AED.
//   ✓ FIX BUG #7: Statement and TI data no longer duplicated. Statement
//     aggregate used for customer-level outstanding/overdue. TI used for
//     invoice-level payment status.
//   ✓ NEW: Deep per-entity context via BuildEntityContextAsync(entityType, id).
//     Called when user asks about a specific PI/JO/DN/TI by ID.
//     Deserializes FullSnapshotJson for that ONE record only.
//   ✓ NEW: Structured sections with clear headers so AI understands
//     data hierarchy (PI → JO → DN → TI chain).
//   ✓ NEW: Overdue job orders flagged with count and IDs.
//   ✓ NEW: Pending delivery balance per job order (top 5 by balance).
//   ✓ NEW: Tax invoice aging — how many overdue and by how much.
//   ✓ NEW: Statement summary — outstanding and overdue per customer.
//
// QUERY STRATEGY:
//   Tier 1 (always-on, fast):
//     Uses indexed columns only. No JSON deserialization.
//     Target: < 150ms on typical dataset (100-500 records).
//   Tier 2 (on-demand, per entity):
//     BuildEntityContextAsync() — single record JSON parse.
//     Called by AiChatViewModel when user references specific ID.
//
// ⚠ PRIVACY — P-1. READ BEFORE ADDING FIELDS HERE.
//   Everything this class returns is appended to the system prompt and
//   sent OVER THE INTERNET to a third-party LLM (Gemini / Groq / Cohere).
//   That already includes real customer names, project names and AED
//   totals. It is NOT anonymised and NOT redacted.
//
//   Gated by AiSettings.UseRealData, which now defaults to FALSE (opt-in)
//   and requires an explicit consent confirmation in Settings before it
//   can be enabled. The Settings toggle states plainly that the data
//   leaves the machine and names the recipients.
//
//   Adding a field here is a privacy decision, not a feature decision.
//   Never add: TRN / tax registration numbers, bank or IBAN details,
//   phone numbers, email addresses, postal addresses, passwords, or
//   per-line cost prices that would expose the company's margins.
//   Aggregates and document IDs are the intended level of detail.
//
//   Still open (documented in the readiness report, not fixed here):
//   there is no per-request consent prompt and no redaction pass. The
//   Agent subsystem under Agent/ has a stricter model — LocalOnly by
//   default, then explicit cloud consent, host whitelist, size cap and a
//   post-redaction sanity check. Aligning this path with that model is
//   the correct long-term fix and is deliberately NOT bolted on here,
//   because it changes AI chat behaviour and needs the user's decision.
//
// THREAD SAFETY:
//   SemaphoreSlim(1,1) ensures one build at a time.
//   Cache reused for 30 seconds. InvalidateCache() clears on data change.
//
// REGRESSION RISK: ZERO — same public API surface.
//   BuildContextAsync() signature unchanged.
//   InvalidateCache() signature unchanged.
//   New BuildEntityContextAsync() is additive.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.Services.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIContext
{
    /// <summary>
    /// Builds real-time business data context for AI system prompt injection.
    /// Phase 2: Deep per-module queries with correct status strings and
    /// full PI→JO→DN→TI chain awareness.
    /// </summary>
    public static class AiContextBuilder
    {
        // ── Cache ──────────────────────────────────────────────────────────
        private static string _cachedContext = string.Empty;
        /// <summary>
        /// When the cached AI context stops being usable.
        /// </summary>
        /// <remarks>
        /// Compared against <see cref="DateTime.UtcNow"/>, not <c>Now</c>
        /// (Phase 6). This value never leaves the process — it is not persisted,
        /// not sent anywhere, not shown to anybody — so it is an elapsed-time
        /// measurement wearing a DateTime's clothes. <c>Now</c> is local time,
        /// and local time jumps: on a spring-forward the clock skips an hour and
        /// the cache serves stale context for an hour longer than
        /// <c>CacheSeconds</c>; on a fall-back it throws a perfectly good cache
        /// away an hour early. UTC has no transitions, so the interval is the
        /// interval that was asked for.
        ///
        /// All three uses were converted together. Converting only the read or
        /// only the write would compare a UTC instant against a local one and be
        /// wrong by the whole UTC offset — far worse than the bug this fixes.
        /// </remarks>
        private static DateTime _cacheExpiry = DateTime.MinValue;
        private const int CacheSeconds = 120;

        // FIX BUG #3: thread-safe build gate
        private static readonly SemaphoreSlim _buildLock =
            new SemaphoreSlim(1, 1);

        // ══════════════════════════════════════════════════════════════════
        //  TIER 1 — ALWAYS-ON SUMMARY CONTEXT
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a concise real-time summary of business data for injection
        /// into the AI system prompt. Results cached 30 seconds.
        /// Returns empty string if UseRealData is disabled or all queries fail.
        /// </summary>
        public static async Task<string> BuildContextAsync()
        {
            // Check cache first (outside lock for performance)
            if (DateTime.UtcNow < _cacheExpiry &&
                !string.IsNullOrEmpty(_cachedContext))
                return _cachedContext;

            // FIX BUG #3: only one build at a time
            await _buildLock.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (DateTime.UtcNow < _cacheExpiry &&
                    !string.IsNullOrEmpty(_cachedContext))
                    return _cachedContext;

                var result = await BuildInternalAsync();

                if (!string.IsNullOrEmpty(result))
                {
                    _cachedContext = result;
                    _cacheExpiry = DateTime.UtcNow.AddSeconds(CacheSeconds);
                }

                return result;
            }
            finally
            {
                _buildLock.Release();
            }
        }

        private static async Task<string> BuildInternalAsync()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("─── LIVE BUSINESS DATA ───────────────────────────────");

            bool hasData = false;

            // ── PROFORMA INVOICES ────────────────────────────────────────
            try
            {
                if (App.Invoices != null)
                {
                    // PERF: use the lightweight analytics projection (Status,
                    // GrandTotalVatInclusive, CreatedAt) instead of
                    // GetAllAsync, which materialises FullSnapshotJson for
                    // every invoice purely to count and sum. The blobs are
                    // never read here, so loading them is pure waste.
                    var invoices = await App.Invoices.GetAnalyticsRowsAsync();

                    if (invoices?.Count > 0)
                    {
                        int total = invoices.Count;
                        decimal totalValue = invoices.Sum(i => i.GrandTotalVatInclusive);
                        int approved = invoices.Count(i => string.Equals(
                            i.Status, "Approved", StringComparison.OrdinalIgnoreCase));
                        int sent = invoices.Count(i => string.Equals(
                            i.Status, "Sent", StringComparison.OrdinalIgnoreCase));
                        int draft = invoices.Count(i => string.Equals(
                            i.Status, "Draft", StringComparison.OrdinalIgnoreCase));

                        sb.AppendLine(
                            $"• Proforma Invoices: {total} total | " +
                            $"AED {totalValue:N0} value | " +
                            $"{approved} approved | {sent} sent | {draft} draft");

                        hasData = true;
                    }
                    else
                    {
                        sb.AppendLine("• Proforma Invoices: none yet");
                    }
                }
            }
            catch { /* DB unavailable — skip */ }

            // ── JOB ORDERS ───────────────────────────────────────────────
            try
            {
                if (App.Jobs != null)
                {
                    // PERF: analytics projection — avoids loading every
                    // FullSnapshotJson blob just to count statuses.
                    var jobs = await App.Jobs.GetAnalyticsRowsAsync();

                    if (jobs?.Count > 0)
                    {
                        int total = jobs.Count;
                        int released = jobs.Count(j => string.Equals(
                            j.Status, "Released", StringComparison.OrdinalIgnoreCase));
                        int inProd = jobs.Count(j => string.Equals(
                            j.Status, "InProduction", StringComparison.OrdinalIgnoreCase));
                        int completed = jobs.Count(j => string.Equals(
                            j.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(
                            j.Status, "Delivered", StringComparison.OrdinalIgnoreCase));

                        // FIX BUG #5: DeliveryDate on SavedJobOrder is DateTime?
                        // JO is overdue if DeliveryDate < today AND not completed/delivered
                        var overdueJobs = jobs
                            .Where(j => j.DeliveryDate.HasValue
                                     && j.DeliveryDate.Value.Date < DateTime.Today
                                     && !string.Equals(j.Status, "Completed",
                                            StringComparison.OrdinalIgnoreCase)
                                     && !string.Equals(j.Status, "Delivered",
                                            StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        int overdue = overdueJobs.Count;

                        sb.Append(
                            $"• Job Orders: {total} total | " +
                            $"{released} released | {inProd} in production | " +
                            $"{completed} completed");

                        if (overdue > 0)
                        {
                            // Show up to 3 overdue JO IDs so AI can reference them
                            var overdueIds = overdueJobs
                                .OrderBy(j => j.DeliveryDate)
                                .Take(3)
                                .Select(j => j.JobOrderId);

                            sb.Append(
                                $" | ⚠ {overdue} OVERDUE " +
                                $"({string.Join(", ", overdueIds)}" +
                                (overdue > 3 ? $" +{overdue - 3} more" : "") +
                                ")");
                        }

                        sb.AppendLine();
                        hasData = true;
                    }
                    else
                    {
                        sb.AppendLine("• Job Orders: none yet");
                    }
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            // ── DELIVERY NOTES ────────────────────────────────────────────
            try
            {
                if (App.DeliveryNotes != null)
                {
                    // PERF: analytics projection — avoids loading every
                    // FullSnapshotJson blob just to count statuses. Excludes
                    // Master DNs, matching the previous GetAllAsync behaviour.
                    var dns = await App.DeliveryNotes.GetAnalyticsRowsAsync();

                    if (dns?.Count > 0)
                    {
                        int total = dns.Count;

                        // FIX BUG #5: DN delivered statuses are Delivered/Signed/Received
                        // NOT "Completed" (that is a JO status)
                        int dispatched = dns.Count(d =>
                            string.Equals(d.Status, "Dispatched", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(d.Status, "Delivered", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(d.Status, "Signed", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(d.Status, "Received", StringComparison.OrdinalIgnoreCase));

                        int preparing = dns.Count(d =>
                            string.Equals(d.Status, "Draft", StringComparison.OrdinalIgnoreCase));

                        int invoiced = dns.Count(d => d.HasTaxInvoice);
                        int notInvoiced = dispatched - invoiced;

                        int todayDelivered = dns.Count(d =>
                            d.DeliveryDate.Date == DateTime.Today &&
                            !string.Equals(d.Status, "Draft",
                                StringComparison.OrdinalIgnoreCase));

                        sb.Append(
                            $"• Delivery Notes: {total} total | " +
                            $"{dispatched} dispatched/delivered | " +
                            $"{preparing} preparing");

                        if (todayDelivered > 0)
                            sb.Append($" | {todayDelivered} delivered today");

                        if (notInvoiced > 0)
                            sb.Append($" | ⚠ {notInvoiced} delivered but not invoiced");

                        sb.AppendLine();

                        // Pending delivery balance — top jobs with outstanding pieces
                        try
                        {
                            var pending = await App.DeliveryNotes.GetPendingQtyJobsAsync();
                            if (pending?.Count > 0)
                            {
                                int totalPendingPcs = pending.Sum(p => p.TotalBalance);
                                int pendingJobs = pending.Count;

                                sb.Append(
                                    $"• Pending Delivery Balance: " +
                                    $"{totalPendingPcs} pieces across " +
                                    $"{pendingJobs} job orders");

                                // Top 3 by balance for AI reference
                                var top3 = pending
                                    .OrderByDescending(p => p.TotalBalance)
                                    .Take(3)
                                    .Select(p =>
                                        $"{p.JobOrderId} ({p.TotalBalance} pcs)");

                                sb.AppendLine($" — top: {string.Join(", ", top3)}");
                            }
                        }
                        catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

                        hasData = true;
                    }
                    else
                    {
                        sb.AppendLine("• Delivery Notes: none yet");
                    }
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            // ── TAX INVOICES ─────────────────────────────────────────────
            // FIX BUG #6: Tax invoices were completely missing from RAG
            try
            {
                if (App.TaxInvoices != null)
                {
                    // PERF: analytics projection — avoids loading every
                    // FullSnapshotJson blob (incl. base64 attachments) just
                    // to count statuses and sum totals.
                    var tis = await App.TaxInvoices.GetAnalyticsRowsAsync();

                    if (tis?.Count > 0)
                    {
                        int total = tis.Count;
                        decimal totalBilled = tis
                            .Where(t =>
                                string.Equals(t.Status, "Issued", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(t.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(t.Status, "PartiallyPaid", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(t.Status, "Overdue", StringComparison.OrdinalIgnoreCase))
                            .Sum(t => t.GrandTotal);

                        int paid = tis.Count(t =>
                            string.Equals(t.Status, "Paid", StringComparison.OrdinalIgnoreCase));
                        int issued = tis.Count(t =>
                            string.Equals(t.Status, "Issued", StringComparison.OrdinalIgnoreCase));

                        // Overdue: status=Overdue OR status=Issued AND DueDate < today
                        var overdueInvoices = tis
                            .Where(t =>
                                string.Equals(t.Status, "Overdue", StringComparison.OrdinalIgnoreCase) ||
                                (string.Equals(t.Status, "Issued", StringComparison.OrdinalIgnoreCase) &&
                                 t.DueDate < DateTime.Today))
                            .ToList();

                        int overdueCount = overdueInvoices.Count;
                        decimal overdueValue = overdueInvoices.Sum(t => t.GrandTotal);

                        sb.Append(
                            $"• Tax Invoices: {total} total | " +
                            $"AED {totalBilled:N0} billed | " +
                            $"{issued} issued | {paid} paid");

                        if (overdueCount > 0)
                            sb.Append(
                                $" | ⚠ {overdueCount} overdue " +
                                $"(AED {overdueValue:N0})");

                        sb.AppendLine();
                        hasData = true;
                    }
                    else
                    {
                        sb.AppendLine("• Tax Invoices: none yet");
                    }
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            // ── ACCOUNT STATEMENTS ────────────────────────────────────────
            // FIX BUG #7: Use statement aggregates (pre-computed by generator)
            // Do NOT recompute from TI — statements already aggregate correctly
            try
            {
                if (App.Statements != null)
                {
                    // PERF: analytics projection — avoids loading every
                    // FullSnapshotJson/FilterJson blob just to sum totals.
                    var stmts = await App.Statements.GetAnalyticsRowsAsync();

                    if (stmts?.Count > 0)
                    {
                        int total = stmts.Count;
                        decimal totalInv = stmts.Sum(s => s.TotalInvoiced);
                        decimal outstanding = stmts.Sum(s => s.TotalOutstanding);
                        decimal overdue = stmts.Sum(s => s.OverdueAmount);

                        sb.Append(
                            $"• Account Statements: {total} generated | " +
                            $"AED {totalInv:N0} invoiced | " +
                            $"AED {outstanding:N0} outstanding");

                        if (overdue > 0)
                            sb.Append($" | AED {overdue:N0} overdue");

                        sb.AppendLine();
                        hasData = true;
                    }
                    else
                    {
                        sb.AppendLine("• Account Statements: none generated yet");
                    }
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            // ── v2 LEARNING CONTEXT ─────────────────────────────────────
            // Assistant memory + the user's recent activity let the AI
            // understand daily work patterns, not just static data.
            string learning = await BuildLearningContextAsync();
            if (learning.Length > 0)
                sb.Append(learning);

            sb.AppendLine("─────────────────────────────────────────────────────");

            return (hasData || learning.Length > 0)
                ? sb.ToString()
                : string.Empty;
        }

        /// <summary>
        /// Combines AI memory (previous conversations) with the latest
        /// audit activity so the assistant has session continuity.
        /// v3: public — memory must be recalled even when the user has
        /// switched real-data (RAG) off, otherwise the assistant appears
        /// to forget everything between sessions.
        /// </summary>
        public static async Task<string> BuildLearningContextAsync()
        {
            try
            {
                var sb = new StringBuilder();

                string memory = await AIMemory.AiMemoryService
                    .BuildMemoryContextAsync();
                if (memory.Length > 0)
                    sb.AppendLine(memory);

                var recent = await Audit.AuditService.GetRecentAsync(12);
                if (recent.Count > 0)
                {
                    sb.AppendLine("─── RECENT USER ACTIVITY (audit trail) ─────────────");
                    foreach (var entry in recent)
                    {
                        sb.Append("• [")
                          .Append(entry.TimestampUtc.ToLocalTime()
                              .ToString("dd MMM HH:mm"))
                          .Append("] ").Append(entry.Section)
                          .Append(" — ").Append(entry.Action);

                        if (!string.IsNullOrWhiteSpace(entry.Details))
                            sb.Append(": ").Append(entry.Details);

                        sb.AppendLine();
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  TIER 2 — ON-DEMAND DEEP ENTITY CONTEXT
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds detailed context for a specific entity referenced by the user.
        /// Deserializes FullSnapshotJson for that ONE record only.
        /// Called by AiChatViewModel when a specific ID is detected in user input.
        ///
        /// entityType: "PI" | "JO" | "DN" | "TI"
        /// entityId:   e.g. "PI-2026-001", "JOB-2026-001", etc.
        ///
        /// Returns empty string if entity not found or query fails.
        /// </summary>
        public static async Task<string> BuildEntityContextAsync(
            string entityType,
            string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId)) return string.Empty;

            var sb = new StringBuilder();

            try
            {
                switch (entityType?.ToUpperInvariant())
                {
                    case "PI":
                        await BuildPiContextAsync(sb, entityId);
                        break;

                    case "JO":
                        await BuildJoContextAsync(sb, entityId);
                        break;

                    case "DN":
                        await BuildDnContextAsync(sb, entityId);
                        break;

                    case "TI":
                        await BuildTiContextAsync(sb, entityId);
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[AiContextBuilder] Entity context failed " +
                    $"({entityType}/{entityId}): {ex.Message}");
            }

            return sb.ToString();
        }

        // ── PI deep context ──────────────────────────────────────────────

        private static async Task BuildPiContextAsync(
            StringBuilder sb, string piId)
        {
            if (App.Invoices == null) return;

            var pi = await App.Invoices.GetByProformaIdAsync(piId);
            if (pi == null)
            {
                sb.AppendLine($"Proforma Invoice {piId}: not found.");
                return;
            }

            sb.AppendLine();
            sb.AppendLine($"─── PROFORMA INVOICE: {pi.ProformaId} ───");
            sb.AppendLine($"Customer  : {pi.CustomerName}");
            sb.AppendLine($"Project   : {pi.ProjectName}");
            sb.AppendLine($"Company   : {pi.CompanyName}");
            sb.AppendLine($"Status    : {pi.Status}");
            sb.AppendLine($"Issued    : {pi.IssueDate:dd MMM yyyy}");
            sb.AppendLine($"Valid Until: {pi.ValidUntil:dd MMM yyyy}");
            sb.AppendLine($"Glass Subtotal : AED {pi.GlassMaterialsSubtotal:N2}");
            sb.AppendLine($"Other Charges  : AED {pi.OtherChargesSubtotal:N2}");
            sb.AppendLine($"Discount       : AED {pi.DiscountAmount:N2}");
            sb.AppendLine($"VAT (5%)       : AED {pi.VatAmount:N2}");
            sb.AppendLine($"Grand Total    : AED {pi.GrandTotalVatInclusive:N2}");

            // Check linked JO
            if (App.Jobs != null)
            {
                var jobs = await App.Jobs.GetBySourceProformaIdAsync(piId);
                if (jobs?.Count > 0)
                {
                    var jo = jobs[0];
                    sb.AppendLine($"Linked JO : {jo.JobOrderId} | Status: {jo.Status}" +
                        (jo.DeliveryDate.HasValue
                            ? $" | Due: {jo.DeliveryDate.Value:dd MMM yyyy}"
                            : ""));
                }
                else
                {
                    sb.AppendLine("Linked JO : None — not yet converted to Job Order");
                }
            }

            // Check linked TI
            if (App.TaxInvoices != null)
            {
                var tiList = await App.TaxInvoices.GetByProformaIdAsync(piId);
                if (tiList?.Count > 0)
                {
                    foreach (var ti in tiList)
                    {
                        sb.AppendLine(
                            $"Tax Invoice: {ti.InvoiceNumber} | " +
                            $"Status: {ti.Status} | " +
                            $"AED {ti.GrandTotal:N2}");
                    }
                }
                else
                {
                    sb.AppendLine("Tax Invoice: None generated yet");
                }
            }

            sb.AppendLine("─────────────────────────────────────────────────────");
        }

        // ── JO deep context ──────────────────────────────────────────────

        private static async Task BuildJoContextAsync(
            StringBuilder sb, string joId)
        {
            if (App.Jobs == null) return;

            var jo = await App.Jobs.GetByJobOrderIdAsync(joId);
            if (jo == null)
            {
                sb.AppendLine($"Job Order {joId}: not found.");
                return;
            }

            sb.AppendLine();
            sb.AppendLine($"─── JOB ORDER: {jo.JobOrderId} ───");
            sb.AppendLine($"Customer  : {jo.CustomerName}");
            sb.AppendLine($"Project   : {jo.ProjectName}");
            sb.AppendLine($"From PI   : {jo.SourceProformaId}");
            sb.AppendLine($"Status    : {jo.Status}");
            sb.AppendLine($"Priority  : {jo.Priority}");
            sb.AppendLine($"Grand Total: AED {jo.GrandTotalVatInclusive:N2}");

            if (jo.DeliveryDate.HasValue)
            {
                bool isOverdue = jo.DeliveryDate.Value.Date < DateTime.Today
                    && jo.Status != "Completed"
                    && jo.Status != "Delivered";

                sb.AppendLine(
                    $"Due Date  : {jo.DeliveryDate.Value:dd MMM yyyy}" +
                    (isOverdue ? " ⚠ OVERDUE" : ""));
            }
            else
            {
                sb.AppendLine("Due Date  : Not set");
            }

            // Delivery history for this JO
            if (App.DeliveryNotes != null)
            {
                try
                {
                    var history = await App.DeliveryNotes
                        .GetDeliveryHistoryAsync(joId);

                    sb.AppendLine(
                        $"Delivery  : {history.TotalDelivered}/{history.TotalOrdered} pcs | " +
                        $"Balance: {history.TotalBalance} pcs");

                    if (history.Entries?.Count > 0)
                    {
                        foreach (var entry in history.Entries
                            .OrderBy(e => e.SequenceNumber))
                        {
                            string invoicedBadge = entry.HasTaxInvoice
                                ? $" [Invoiced: {entry.TaxInvoiceId}]"
                                : " [Not invoiced]";

                            sb.AppendLine(
                                $"  DN #{entry.SequenceNumber}: " +
                                $"{entry.DeliveryNoteId} | " +
                                $"{entry.DeliveredQty} pcs | " +
                                $"{entry.Status} | " +
                                $"{entry.DeliveryDate:dd MMM yyyy}" +
                                invoicedBadge);
                        }
                    }
                }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            }

            sb.AppendLine("─────────────────────────────────────────────────────");
        }

        // ── DN deep context ──────────────────────────────────────────────

        private static async Task BuildDnContextAsync(
            StringBuilder sb, string dnId)
        {
            if (App.DeliveryNotes == null) return;

            var dn = await App.DeliveryNotes.GetByDeliveryNoteIdAsync(dnId);
            if (dn == null)
            {
                sb.AppendLine($"Delivery Note {dnId}: not found.");
                return;
            }

            sb.AppendLine();
            sb.AppendLine($"─── DELIVERY NOTE: {dn.DeliveryNoteId} ───");
            sb.AppendLine($"Customer  : {dn.CustomerName}");
            sb.AppendLine($"Project   : {dn.ProjectName}");
            sb.AppendLine($"Job Order : {dn.JobOrderId}");
            sb.AppendLine($"Proforma  : {dn.ProformaId}");
            sb.AppendLine($"Status    : {dn.Status}");
            sb.AppendLine($"Type      : {dn.DeliveryType}");
            sb.AppendLine($"Date      : {dn.DeliveryDate:dd MMM yyyy} {dn.DeliveryTime}");
            sb.AppendLine($"Driver    : {(string.IsNullOrWhiteSpace(dn.DriverName) ? "—" : dn.DriverName)}");
            sb.AppendLine(
                $"Qty       : {dn.TotalDeliveredQty} delivered | " +
                $"{dn.TotalBalanceQty} balance | " +
                $"{dn.TotalOrderedQty} ordered");

            if (dn.HasTaxInvoice)
                sb.AppendLine($"Invoiced  : YES — {dn.TaxInvoiceId}");
            else
                sb.AppendLine("Invoiced  : No tax invoice generated yet");

            // Line item summary (from Items navigation property)
            if (dn.Items?.Count > 0)
            {
                sb.AppendLine($"Items     : {dn.Items.Count} glass rows");
                var specGroups = dn.Items
                    .GroupBy(i => i.SpecGroupNumber)
                    .OrderBy(g => g.Key);

                foreach (var grp in specGroups)
                {
                    int delivered = grp.Sum(i => i.DeliveredQty);
                    int balance = grp.Sum(i => i.BalanceQty);
                    sb.AppendLine(
                        $"  Spec {grp.Key} ({grp.First().SpecGroupName}): " +
                        $"{delivered} delivered | {balance} balance");
                }
            }

            sb.AppendLine("─────────────────────────────────────────────────────");
        }

        // ── TI deep context ──────────────────────────────────────────────

        private static async Task BuildTiContextAsync(
            StringBuilder sb, string tiId)
        {
            if (App.TaxInvoices == null) return;

            var ti = await App.TaxInvoices.GetByInvoiceNumberAsync(tiId);
            if (ti == null)
            {
                sb.AppendLine($"Tax Invoice {tiId}: not found.");
                return;
            }

            sb.AppendLine();
            sb.AppendLine($"─── TAX INVOICE: {ti.InvoiceNumber} ───");
            sb.AppendLine($"Customer  : {ti.CustomerName}");
            sb.AppendLine($"Project   : {ti.ProjectName}");
            sb.AppendLine($"Company   : {ti.CompanyName}");
            sb.AppendLine($"From PI   : {ti.ProformaId}");
            sb.AppendLine($"Job Order : {(string.IsNullOrWhiteSpace(ti.JobOrderId) ? "—" : ti.JobOrderId)}");
            sb.AppendLine($"DN Ref    : {(string.IsNullOrWhiteSpace(ti.DeliveryNoteReference) ? "—" : ti.DeliveryNoteReference)}");
            sb.AppendLine($"Type      : {ti.InvoiceType}");
            sb.AppendLine($"Status    : {ti.Status}");
            sb.AppendLine($"Issued    : {ti.IssueDate:dd MMM yyyy}");
            sb.AppendLine($"Due       : {ti.DueDate:dd MMM yyyy}" +
                (ti.DueDate < DateTime.Today &&
                 ti.Status != "Paid" &&
                 ti.Status != "Cancelled" &&
                 ti.Status != "Void"
                    ? " ⚠ OVERDUE" : ""));
            sb.AppendLine($"Payment   : {ti.PaymentTerms}");
            sb.AppendLine($"Subtotal  : AED {ti.Subtotal:N2}");

            if (ti.DiscountAmount > 0)
                sb.AppendLine($"Discount  : AED {ti.DiscountAmount:N2}");

            sb.AppendLine($"VAT (5%)  : AED {ti.VatAmount:N2}");
            sb.AppendLine($"Grand Total: AED {ti.GrandTotal:N2}");
            sb.AppendLine($"Qty / SQM  : {ti.TotalQty} pcs | {ti.TotalSqm:N3} m²");

            if (ti.Items?.Count > 0)
            {
                sb.AppendLine($"Line Items : {ti.Items.Count} glass rows");
                var groups = ti.Items
                    .GroupBy(i => i.SpecGroupNumber)
                    .OrderBy(g => g.Key);

                foreach (var grp in groups)
                {
                    decimal groupValue = grp.Sum(i => i.Value);
                    int groupQty = grp.Sum(i => i.Qty);
                    sb.AppendLine(
                        $"  Spec {grp.Key}: {grp.First().GlassType} | " +
                        $"{groupQty} pcs | AED {groupValue:N2}");
                }
            }

            sb.AppendLine("─────────────────────────────────────────────────────");
        }

        // ══════════════════════════════════════════════════════════════════
        //  CACHE CONTROL
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Clears the cached context. Call when user creates/edits/deletes
        /// data so the next AI question gets fresh numbers.
        /// </summary>
        public static void InvalidateCache()
        {
            _cachedContext = string.Empty;
            _cacheExpiry = DateTime.MinValue;
        }
    }
}