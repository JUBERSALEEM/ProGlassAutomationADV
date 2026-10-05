// ═══════════════════════════════════════════════════════════════════════
// FILE: StatementWorkflowEngine.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.Attachments;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Reconciliation;
using ProGlassAutomation.Services.TaxInvoice.Pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Statement.Workflow
{
        public partial class StatementWorkflowEngine
    {
            private async Task LogDnLineReconciliation(
            string deliveryNoteId,
            Dictionary<string, PiLineInfo> piLineMap,
            Dictionary<string, int> invoicedMap,
            Dictionary<string, int> runningDeliveredMap,
            WorkflowStepState step)
        {
            try
            {
                var dnEntity = await App.DeliveryNotes
                    .GetByDeliveryNoteIdAsync(deliveryNoteId);

                if (dnEntity?.Items == null || dnEntity.Items.Count == 0)
                {
                    step.AddDetail(
                        $"    (no line items in this DN)");
                    return;
                }

                step.AddDetail("");
                step.AddDetail(
                    $"    ── Line-Item Reconciliation for {deliveryNoteId} ──");

                int overInvoicedCount = 0;
                int overDeliveredCount = 0;

                foreach (var item in dnEntity.Items
                    .OrderBy(i => i.SpecGroupNumber)
                    .ThenBy(i => i.SerialNumber))
                {
                    string key = $"{item.SpecGroupNumber}|" +
                                 $"{item.SerialNumber}";

                    // Get PI ordered qty
                    if (!piLineMap.TryGetValue(key, out var piLine))
                    {
                        step.AddDetail(
                            $"      ⚠ Spec {item.SpecGroupNumber}, " +
                            $"S/N {item.SerialNumber}: " +
                            $"not found in PI");
                        continue;
                    }

                    int ordered = piLine.OrderedQty;
                    int previouslyDelivered =
                        runningDeliveredMap.TryGetValue(key, out int prev)
                            ? prev : 0;
                    int thisDn = item.DeliveredQty;
                    int cumulativeDelivered = previouslyDelivered + thisDn;
                    int balance = Math.Max(0, ordered - cumulativeDelivered);
                    int invoiced = invoicedMap.TryGetValue(key, out int inv)
                        ? inv : 0;
                    int diff = invoiced - cumulativeDelivered;

                    // Determine per-line status icon
                    string statusIcon;
                    if (diff > 0)
                    {
                        statusIcon = "🔴 OVER-INVOICED";
                        overInvoicedCount++;
                    }
                    else if (cumulativeDelivered > ordered)
                    {
                        statusIcon = "🟠 OVER-DELIVERED";
                        overDeliveredCount++;
                    }
                    else if (balance == 0 && diff == 0)
                    {
                        statusIcon = "✅ OK";
                    }
                    else if (balance > 0)
                    {
                        statusIcon = "🟡 partial";
                    }
                    else
                    {
                        statusIcon = "🔵 under-invoiced";
                    }

                    step.AddDetail(
                        $"      {piLine.GlassRef} " +
                        $"(Spec {item.SpecGroupNumber}, " +
                        $"S/N {item.SerialNumber}):");
                    step.AddDetail(
                        $"        📦 PI Ordered:        {ordered} pc" +
                        (ordered == 1 ? "" : "s"));
                    step.AddDetail(
                        $"        📋 Prev Delivered:    {previouslyDelivered} pc" +
                        (previouslyDelivered == 1 ? "" : "s"));
                    step.AddDetail(
                        $"        🚚 This DN Delivers:  {thisDn} pc" +
                        (thisDn == 1 ? "" : "s"));
                    step.AddDetail(
                        $"        📊 Balance Remaining: {balance} pc" +
                        (balance == 1 ? "" : "s"));
                    step.AddDetail(
                        $"        🧾 Total Invoiced:    {invoiced} pc" +
                        (invoiced == 1 ? "" : "s") +
                        (diff > 0 ? $"  ⚠ EXCEEDS delivered by {diff}!" : ""));
                    step.AddDetail(
                        $"        💡 Status:            {statusIcon}");

                    await Task.Delay(22);

                    // Update running delivered map for next DN's calculation
                    runningDeliveredMap[key] = cumulativeDelivered;
                }

                // Add warnings for critical issues
                if (overInvoicedCount > 0)
                {
                    step.AddWarning(
                        $"DN {deliveryNoteId}: {overInvoicedCount} line(s) " +
                        $"OVER-INVOICED (invoiced more than delivered)");
                }

                if (overDeliveredCount > 0)
                {
                    step.AddWarning(
                        $"DN {deliveryNoteId}: {overDeliveredCount} line(s) " +
                        $"over-delivered (exceeds PI ordered qty)");
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[Workflow] DN reconciliation error " +
                    $"{deliveryNoteId}: {ex.Message}");
                step.AddDetail(
                    $"    ⚠ Reconciliation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Internal DTO for PI line items used by Step 3 reconciliation.
        /// Populated by LoadPiLineMapAsync.
        /// </summary>
        private sealed class PiLineInfo
        {
            public int SpecGroupNumber { get; init; }
            public int SerialNumber { get; init; }
            public string GlassRef { get; init; } = "";
            public int OrderedQty { get; init; }
        }

        // ─────────────────────────────────────────────────────────────────
        //  HELPER — Resolve attachment file path (3 strategies)
        //
        //  Strategy 1: FilePath stored in DB — use directly if file exists.
        //  Strategy 2: Rebuild from AppData + AttachmentType subfolder.
        //              Handles cases where machine/drive changed.
        //  Strategy 3: Search both Images + PDFs subfolders.
        //              Handles cases where AttachmentType was recorded wrong.
        //
        //  Returns resolved path string, or null if not found anywhere.
        // ─────────────────────────────────────────────────────────────────

        private static string? ResolveAttachmentPath(
            string storedPath,
            string fileName,
            string attType,
            string deliveryNoteId,
            WorkflowStepState step)
        {
            // ── Strategy 1: stored path ───────────────────────────────────
            if (!string.IsNullOrWhiteSpace(storedPath) &&
                File.Exists(storedPath))
            {
                AppLogService.Debug($"[Workflow]   ✓ Strategy 1 (stored path)");
                return storedPath;
            }

            AppLogService.Debug(
                $"[Workflow]   ✗ Strategy 1 failed: " +
                $"'{storedPath}' — " +
                (string.IsNullOrWhiteSpace(storedPath)
                    ? "path is empty"
                    : "file not found"));

            if (string.IsNullOrWhiteSpace(fileName))
            {
                AppLogService.Debug(
                    $"[Workflow]   ✗ Cannot try Strategy 2/3: " +
                    $"fileName is empty");
                return null;
            }

            // ── Strategy 2: rebuild from known folder structure ───────────
            string primarySubfolder = string.Equals(
                attType, "Pdf",
                StringComparison.OrdinalIgnoreCase)
                ? "PDFs" : "Images";

            string rebuilt = Path.Combine(
                DnAttachmentRoot,
                primarySubfolder,
                deliveryNoteId,
                fileName);

            if (File.Exists(rebuilt))
            {
                AppLogService.Debug(
                    $"[Workflow]   ✓ Strategy 2 (rebuilt): '{rebuilt}'");
                step.AddDetail($"    🔧 Rebuilt path: {fileName}");
                return rebuilt;
            }

            AppLogService.Debug(
                $"[Workflow]   ✗ Strategy 2 failed: '{rebuilt}'");

            // ── Strategy 3: search both subfolders ───────────────────────
            foreach (var subfolder in new[] { "Images", "PDFs" })
            {
                if (subfolder == primarySubfolder) continue; // already tried

                string tryPath = Path.Combine(
                    DnAttachmentRoot,
                    subfolder,
                    deliveryNoteId,
                    fileName);

                if (File.Exists(tryPath))
                {
                    AppLogService.Debug(
                        $"[Workflow]   ✓ Strategy 3 ({subfolder}): " +
                        $"'{tryPath}'");
                    step.AddDetail(
                        $"    🔧 Found in {subfolder}: {fileName}");
                    return tryPath;
                }

                AppLogService.Debug(
                    $"[Workflow]   ✗ Strategy 3 ({subfolder}): " +
                    $"not found");
            }

            return null; // not found via any strategy
        }

        /// <summary>
        /// Resolves a Proforma Invoice Step 8 attachment file path with a
        /// fallback: use the manifest's stored FilePath first; if that is
        /// stale, rebuild it from the known AppData folder structure
        /// ({ProformaId}\\{Images|PDFs}\\{filename}) based on AttachmentType.
        /// </summary>
        private static string? ResolvePiAttachmentPath(
            ProGlassAutomation.Models.ProformaInvoice.Attachments.ProformaAttachmentRecord record,
            string proformaId)
        {
            string fileName = record.FileName ?? "";

            // Strategy 1: stored absolute path
            if (!string.IsNullOrWhiteSpace(record.FilePath) &&
                File.Exists(record.FilePath))
                return record.FilePath;

            if (string.IsNullOrWhiteSpace(fileName)) return null;

            // Strategy 2: rebuild from AppData structure
            string primarySubfolder = string.Equals(
                record.AttachmentType, "Pdf",
                StringComparison.OrdinalIgnoreCase)
                ? "PDFs" : "Images";

            string rebuilt = Path.Combine(
                PiAttachmentRoot, proformaId, primarySubfolder, fileName);
            if (File.Exists(rebuilt)) return rebuilt;

            // Strategy 3: search both subfolders (type may have been mis-recorded)
            foreach (var subfolder in new[] { "Images", "PDFs" })
            {
                if (subfolder == primarySubfolder) continue;
                string tryPath = Path.Combine(
                    PiAttachmentRoot, proformaId, subfolder, fileName);
                if (File.Exists(tryPath)) return tryPath;
            }

            return null;
        }

        // ═════════════════════════════════════════════════════════════════
        //  STEP 4 — VALIDATE QUANTITIES
        //  BUG 2 FIX: partial delivery (balance > 0) → MarkWarning
        // ═════════════════════════════════════════════════════════════════

        private async Task Step4_ValidateQuantities(WorkflowResult result)
        {
            var step = Steps[3];
            step.MarkRunning("Comparing quantities...");
            await Task.Delay(90);

            int ordered = result.OrderedQty;
            int delivered = result.DeliveredQty;
            int balance = Math.Max(0, ordered - delivered);
            int pending = result.PendingProductionQty;

            step.AddDetail($"📦 Total Ordered:       {ordered} pcs");
            step.AddDetail($"🚚 Total Delivered:     {delivered} pcs");
            step.AddDetail($"📊 Balance to Deliver:  {balance} pcs");
            step.AddDetail($"⏳ Pending Production:  {pending} pcs");

            await Task.Delay(90);

            if (ordered > 0)
            {
                double pct = (double)delivered / ordered * 100.0;
                step.AddDetail(
                    $"📈 Delivery Progress:   {pct:0.#}% " +
                    $"({delivered} of {ordered})");
            }

            await Task.Delay(45);

            if (delivered > ordered && ordered > 0)
            {
                int over = delivered - ordered;
                step.AddWarning(
                    $"Over-delivered by {over} pcs " +
                    $"(ordered {ordered}, delivered {delivered})");
                step.MarkWarning(
                    $"Ordered {ordered} · Delivered {delivered} · " +
                    $"Over by {over} pcs");
            }
            else if (ordered > 0 && delivered == 0)
            {
                step.AddWarning(
                    "No deliveries yet — statement may be premature");
                step.MarkWarning(
                    $"Ordered {ordered} pcs · No deliveries yet");
            }
            else if (ordered == 0)
            {
                step.AddWarning(
                    "Could not extract ordered qty from Job Order");
                step.MarkWarning(
                    $"Ordered qty unknown · Delivered {delivered} pcs");
            }
            else if (ordered > 0 && balance > 0)
            {
                // BUG 2 FIX: partial delivery → warn, not success
                step.AddWarning(
                    $"Partial delivery: {balance} pcs still to deliver " +
                    $"(ordered {ordered}, delivered {delivered})");
                step.MarkWarning(
                    $"Ordered {ordered} · Delivered {delivered} · " +
                    $"Balance {balance} pcs outstanding");
            }
            else
            {
                // Fully delivered
                step.MarkSuccess(
                    $"Ordered {ordered} · Delivered {delivered} · " +
                    $"Fully delivered ✓");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  STEP 5 — READ TAX INVOICES
        // ═════════════════════════════════════════════════════════════════

        private async Task Step5_ReadTaxInvoices(
            StatementFilter filter,
            string statementNumber,
            WorkflowResult result)
        {
            var step = Steps[4];
            step.MarkRunning("Loading tax invoices...");
            await Task.Delay(90);

            try
            {
                var snapshot = await _aggregator.BuildAsync(
                    filter, statementNumber);

                if (snapshot == null)
                {
                    step.MarkWarning("No invoices matched filters.");
                    return;
                }

                result.Snapshot = snapshot;
                result.InvoicedQty = snapshot.InvoiceRows.Sum(r => r.TotalQty);

                int fullCount = snapshot.InvoiceRows.Count(r =>
                    string.Equals(r.InvoiceType, "Full",
                        StringComparison.OrdinalIgnoreCase));
                int partialCount = snapshot.InvoiceRows.Count - fullCount;

                step.AddDetail(
                    $"🔍 Found {snapshot.InvoiceRows.Count} invoice(s)");
                step.AddDetail(
                    $"   Full: {fullCount}  ·  Partial: {partialCount}");
                step.AddDetail(
                    $"   Total invoiced: {result.InvoicedQty} pcs");
                step.AddDetail(
                    $"   Total value: AED " +
                    $"{snapshot.Financial.TotalInvoiced:N2}");

                await Task.Delay(70);

                foreach (var row in snapshot.InvoiceRows)
                {
                    step.AddDetail(
                        $"  🧾 {row.InvoiceNumber} · {row.InvoiceType} · " +
                        $"{row.TotalQty} pcs · " +
                        $"AED {row.GrandTotal:N2} · {row.Status}");
                    await Task.Delay(35);
                }

                step.MarkSuccess(
                    $"{snapshot.InvoiceRows.Count} invoice(s) · " +
                    $"{result.InvoicedQty} pcs · " +
                    $"AED {snapshot.Financial.TotalInvoiced:N2}");
            }
            catch (Exception ex)
            {
                step.MarkWarning($"Invoice load error: {ex.Message}");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  STEP 6 — CROSS-MATCH COVERAGE + RECONCILIATION (Chat 22 v3)
        //
        //  Two-part check:
        //    Part A (existing): DN-level coverage — is each DN invoiced?
        //    Part B (Chat 22 v3): Line-item reconciliation — for each
        //                          glass row, cross-reference PI/DN/TI
        //                          quantities and flag over-invoicing.
        //
        //  Populates result.ReconciliationSummaries with per-PI data
        //  so the Review step can display it in the warnings feed.
        // ═════════════════════════════════════════════════════════════════

        private async Task Step6_CrossMatchCoverage(WorkflowResult result)
        {
            var step = Steps[5];
            step.MarkRunning("Checking invoice coverage...");
            await Task.Delay(90);

            if (result.Snapshot == null)
            {
                step.MarkSkipped("No snapshot to cross-match.");
                return;
            }

            int delivered = result.DeliveredQty;
            int invoiced = result.InvoicedQty;
            int uninvoiced = result.UninvoicedDeliveredQty;

            step.AddDetail($"🚚 Delivered:  {delivered} pcs");
            step.AddDetail($"🧾 Invoiced:   {invoiced} pcs");
            step.AddDetail($"❓ Uninvoiced: {uninvoiced} pcs");
            await Task.Delay(90);

            // ── PART A: DN-level coverage check (existing) ────────────────

            var invoicedDnRefs = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var row in result.Snapshot.InvoiceRows)
            {
                if (!string.IsNullOrWhiteSpace(row.DeliveryNoteRef))
                {
                    foreach (var dnRef in row.DeliveryNoteRef.Split(
                        new[] { ',' },
                        StringSplitOptions.RemoveEmptyEntries))
                    {
                        invoicedDnRefs.Add(dnRef.Trim());
                    }
                }
            }

            foreach (var dnAtt in result.SignedDeliveryNotes)
            {
                if (dnAtt.DeliveredQty <= 0) continue;

                if (!invoicedDnRefs.Contains(dnAtt.DeliveryNoteId))
                {
                    result.UninvoicedDeliveryNotes.Add(
                        new UninvoicedDeliveryNote
                        {
                            DeliveryNoteId = dnAtt.DeliveryNoteId,
                            DeliveryNoteType = dnAtt.DeliveryNoteType,
                            DeliveredQty = dnAtt.DeliveredQty,
                            DeliveryDate = dnAtt.DeliveryDate,
                            ProformaId = result.Snapshot.InvoiceRows
                                .FirstOrDefault()?.ProformaId ?? "",
                            JobOrderId = ""
                        });

                    step.AddDetail(
                        $"  ❌ {dnAtt.DeliveryNoteId}: " +
                        $"{dnAtt.DeliveredQty} pcs — NO INVOICE");
                    await Task.Delay(45);
                }
                else
                {
                    step.AddDetail(
                        $"  ✅ {dnAtt.DeliveryNoteId}: " +
                        $"{dnAtt.DeliveredQty} pcs — invoiced");
                    await Task.Delay(35);
                }
            }

            // ═══════════════════════════════════════════════════════════════
            //  PART B: LINE-ITEM RECONCILIATION (Chat 22 v3)
            //  Cross-reference PI ordered qty vs DN delivered qty vs
            //  TI invoiced qty per line item. Flag over-invoicing.
            // ═══════════════════════════════════════════════════════════════

            step.AddDetail("");
            step.AddDetail("── Line-Item Reconciliation ──");
            await Task.Delay(70);

            // Extract unique Proforma IDs from the snapshot
            var uniqueProformas = result.Snapshot.InvoiceRows
                .Select(r => r.ProformaId ?? "")
                .Where(pid => !string.IsNullOrWhiteSpace(pid))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (uniqueProformas.Count == 0)
            {
                step.AddDetail("  ⚠ No PI references — skipping reconciliation");
            }
            else
            {
                step.AddDetail(
                    $"  🔍 Reconciling {uniqueProformas.Count} PI family(s)...");
                await Task.Delay(70);

                int totalOverInvoicedLines = 0;
                int totalUnderInvoicedLines = 0;
                int totalPartialLines = 0;
                int totalMatchedLines = 0;

                foreach (var proformaId in uniqueProformas)
                {
                    OrderReconciliationSummary summary;

                    try
                    {
                        summary = await _reconciliationService
                            .BuildForProformaAsync(proformaId);
                    }
                    catch (Exception ex)
                    {
                        step.AddDetail(
                            $"  ⚠ {proformaId}: reconciliation failed " +
                            $"({ex.Message})");
                        continue;
                    }

                    // Store summary in workflow result for Review step access
                    result.ReconciliationSummaries.Add(summary);

                    if (!summary.HasProformaData)
                    {
                        step.AddDetail(
                            $"  ⚠ {proformaId}: PI data unavailable");
                        continue;
                    }

                    // Aggregate counts
                    totalOverInvoicedLines += summary.OverInvoicedCount;
                    totalUnderInvoicedLines += summary.UnderInvoicedCount;
                    totalPartialLines += summary.PartialDeliveryCount;
                    totalMatchedLines += summary.MatchedCount;

                    // Log PI-level summary
                    step.AddDetail(
                        $"  {summary.OverallIcon} {proformaId}: " +
                        $"O:{summary.TotalOrderedQty}  " +
                        $"D:{summary.TotalDeliveredQty}  " +
                        $"I:{summary.TotalInvoicedQty}  " +
                        $"→ {summary.SummaryDisplay}");

                    // Detail: log ONLY problem lines (skip matched to reduce noise)
                    foreach (var row in summary.Rows.Where(r =>
                        r.Status != ReconciliationStatus.FullyMatched &&
                        r.Status != ReconciliationStatus.NotYetStarted))
                    {
                        string diffText = row.InvoiceVsDeliveryDiff > 0
                            ? $"+{row.InvoiceVsDeliveryDiff}"
                            : row.InvoiceVsDeliveryDiff.ToString();

                        step.AddDetail(
                            $"    {row.StatusIcon} " +
                            $"{row.GlassRef} " +
                            $"(Spec {row.SpecGroupNumber}, Row {row.SerialNumber}): " +
                            $"Ordered={row.OrderedQty}, " +
                            $"Delivered={row.DeliveredQty}, " +
                            $"Invoiced={row.InvoicedQty} " +
                            $"→ {row.StatusLabel} ({diffText})");
                        await Task.Delay(28);
                    }

                    // Add step-level warnings for over-invoiced items
                    if (summary.HasOverInvoicing)
                    {
                        step.AddWarning(
                            $"{proformaId}: {summary.OverInvoicedCount} " +
                            $"line(s) OVER-INVOICED — investigate immediately");
                    }

                    if (summary.HasUnderInvoicing)
                    {
                        step.AddWarning(
                            $"{proformaId}: {summary.UnderInvoicedCount} " +
                            $"line(s) under-invoiced — revenue leakage risk");
                    }

                    await Task.Delay(45);
                }

                // ── Final reconciliation summary line ──────────────────────
                step.AddDetail("");
                var summaryParts = new List<string>();
                if (totalMatchedLines > 0)
                    summaryParts.Add($"{totalMatchedLines} matched");
                if (totalPartialLines > 0)
                    summaryParts.Add($"{totalPartialLines} partial");
                if (totalUnderInvoicedLines > 0)
                    summaryParts.Add($"{totalUnderInvoicedLines} under-invoiced");
                if (totalOverInvoicedLines > 0)
                    summaryParts.Add(
                        $"{totalOverInvoicedLines} 🔴 OVER-INVOICED");

                step.AddDetail(
                    $"  📊 Reconciliation: " +
                    (summaryParts.Count > 0
                        ? string.Join(" · ", summaryParts)
                        : "no data"));
            }

            await Task.Delay(70);

            // ═══════════════════════════════════════════════════════════════
            //  FINAL STEP OUTCOME
            //  Priority: OVER-INVOICED (critical) > uninvoiced DNs > success
            // ═══════════════════════════════════════════════════════════════

            bool hasOverInvoicing = result.HasOverInvoicing;
            bool hasUninvoicedDns = result.UninvoicedDeliveryNotes.Count > 0;

            if (hasOverInvoicing)
            {
                // Critical: over-invoicing detected
                int overCount = result.TotalOverInvoicedLineCount;
                string msg =
                    $"🔴 {overCount} line(s) OVER-INVOICED";

                if (hasUninvoicedDns)
                    msg += $" · {result.UninvoicedDeliveryNotes.Count} uninvoiced DN(s)";

                step.MarkWarning(msg);
            }
            else if (hasUninvoicedDns)
            {
                // Some DNs not invoiced (existing behavior)
                step.AddWarning(
                    $"{result.UninvoicedDeliveryNotes.Count} DN(s) not invoiced");
                step.MarkWarning(
                    $"{result.UninvoicedDeliveryNotes.Count} uninvoiced DN(s)");
            }
            else if (result.HasReconciliationDiscrepancies)
            {
                // Minor: partial delivery or under-invoicing (not critical)
                step.MarkWarning(
                    "All DNs invoiced · Minor reconciliation notes above");
            }
            else
            {
                // Full success
                step.MarkSuccess(
                    "All delivered items invoiced · Reconciliation clean ✓");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  STEP 7 — REGENERATE TAX INVOICE PDFs
        //  BUG 1 FIX: after regenerating each invoice PDF, load the
        //  invoice's own TaxInvoiceAttachment records from DB and
        //  store in invoicePdf.Attachments for use by the exporter.
        // ═════════════════════════════════════════════════════════════════

        private async Task Step7_RegenerateTaxInvoicePdfs(
            WorkflowResult result)
        {
            var step = Steps[6];
            step.MarkRunning("Regenerating tax invoice PDFs...");
            await Task.Delay(90);

            if (result.Snapshot == null)
            {
                step.MarkSkipped("No snapshot to generate.");
                return;
            }

            int okCount = 0;
            int failCount = 0;
            int attTotal = 0;
            int total = result.Snapshot.InvoiceRows.Count;
            int current = 0;

            foreach (var row in result.Snapshot.InvoiceRows)
            {
                current++;
                step.CurrentMessage =
                    $"Generating {current}/{total}: " +
                    $"{row.InvoiceNumber}...";

                var invoicePdf = new WorkflowTaxInvoicePdf
                {
                    InvoiceNumber = row.InvoiceNumber ?? "",
                    InvoiceType = row.InvoiceType ?? ""
                };

                try
                {
                    var entity = await App.TaxInvoices
                        .GetByInvoiceNumberAsync(row.InvoiceNumber);

                    if (entity == null)
                    {
                        invoicePdf.Error = "Entity not found";
                        failCount++;
                        step.AddDetail(
                            $"  ❌ {row.InvoiceNumber}: not found");
                    }
                    else
                    {
                        // ── Regenerate the invoice PDF ────────────────────
                        byte[] bytes = await TaxInvoicePdfExporter
                            .GenerateAsync(entity);

                        if (bytes != null && bytes.Length > 0)
                        {
                            invoicePdf.Bytes = bytes;
                            okCount++;
                            step.AddDetail(
                                $"  ✅ {row.InvoiceNumber}: " +
                                $"{bytes.Length:N0} bytes");
                        }
                        else
                        {
                            invoicePdf.Error = "Empty output";
                            failCount++;
                            step.AddDetail(
                                $"  ❌ {row.InvoiceNumber}: empty PDF");
                        }

                        // ── BUG 1 FIX: Load TI attachments from DB ────────
                        try
                        {
                            step.CurrentMessage =
                                $"Loading attachments for " +
                                $"{row.InvoiceNumber}...";

                            var tiAtts = await App.TaxInvoices
                                .GetAttachmentsAsync(row.InvoiceNumber);

                            if (tiAtts != null && tiAtts.Count > 0)
                            {
                                var ordered = tiAtts
                                    .Where(a => a.FileBytes != null &&
                                                a.FileBytes.Length > 0)
                                    .OrderBy(a => GetTiAttachmentOrder(
                                                      a.AttachmentType))
                                    .ThenBy(a => a.UploadedAt)
                                    .ToList();

                                foreach (var att in ordered)
                                {
                                    invoicePdf.Attachments.Add(
                                        (att.FileBytes!, att.FileName ?? ""));
                                    attTotal++;
                                }

                                step.AddDetail(
                                    $"    📎 {ordered.Count} TI attachment(s): " +
                                    string.Join(", ",
                                        ordered.Select(a => a.AttachmentType)));
                            }
                            else
                            {
                                step.AddDetail(
                                    $"    📎 No TI attachments for " +
                                    $"{row.InvoiceNumber}");
                            }
                        }
                        catch (Exception attEx)
                        {
                            step.AddDetail(
                                $"    ⚠ TI attachment load failed " +
                                $"({row.InvoiceNumber}): {attEx.Message}");
                            step.AddWarning(
                                $"TI attachment load failed for " +
                                $"{row.InvoiceNumber}: {attEx.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    invoicePdf.Error = ex.Message;
                    failCount++;
                    step.AddDetail(
                        $"  ❌ {row.InvoiceNumber}: {ex.Message}");
                    step.AddWarning(
                        $"Invoice {row.InvoiceNumber}: {ex.Message}");
                }

                result.TaxInvoicePdfs.Add(invoicePdf);
                await Task.Delay(70);
            }

            if (failCount > 0)
                step.MarkWarning(
                    $"{okCount} generated · {failCount} failed · " +
                    $"{attTotal} TI attachment(s) loaded");
            else
                step.MarkSuccess(
                    $"{okCount} invoice PDF(s) · " +
                    $"{attTotal} TI attachment(s) loaded ✓");
        }
    }
}
