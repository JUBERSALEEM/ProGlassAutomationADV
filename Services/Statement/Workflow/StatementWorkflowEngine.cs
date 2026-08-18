// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Workflow/StatementWorkflowEngine.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Workflow Engine
//
// CHAT 22 v3 ADDITIONS:
//   • Step 3 — Per-DN line-item reconciliation logging
//     Shows Ordered/Prev Delivered/This DN/Balance/Invoiced per line
//     Flags over-invoicing at earliest possible pipeline stage
//     Uses cached PI + TI data (loaded once, reused per DN)
//   • Step 6 — Line-item reconciliation via OrderReconciliationService
//     Populates WorkflowResult.ReconciliationSummaries
//     Adds warnings for over-invoiced/under-invoiced items
//
// CHAT 18 PRESERVED:
//   BUG 1 FIX: Step 7 — loads TI attachments from DB after PDF regen.
//   BUG 2 FIX: Step 4 — partial delivery → MarkWarning (not Success).
//   BUG 3 FIX: Step 8 — tiAttCount included in sections total.
//   BUG 4 FIX: Step 3 — DN attachment file resolution with 3-strategy
//              fallback (stored path → rebuild from AppData → search
//              both subfolders). Full debug logging on every file check.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Reconciliation;
using ProGlassAutomation.Services.TaxInvoice.Pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Statement.Workflow
{
    public delegate Task<UserWarningDecision> ResolveMissingInvoicesCallback(
        WorkflowResult partialResult);

    public class StatementWorkflowEngine
    {
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        private readonly StatementAggregator _aggregator;

        // CHAT 22 v3: Reconciliation service for Step 6 line-item cross-check
        private readonly OrderReconciliationService _reconciliationService;

        // Valid DN types to include — all others are skipped
        private static readonly HashSet<string> ValidDeliveryTypes = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "Original", "Master", ""
        };

        // TI attachment sort order: SignedPI → SignedDN → Other
        private static int GetTiAttachmentOrder(string? type)
        {
            if (string.Equals(type, "SignedPI",
                StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(type, "SignedDN",
                StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        // Known DN attachment root folder (matches DeliveryNoteAttachment spec)
        // Structure: Attachments\Images\{DeliveryNoteId}\{filename}
        //        or: Attachments\PDFs\{DeliveryNoteId}\{filename}
        private static string DnAttachmentRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation",
            "DeliveryNotes",
            "Attachments");

        public List<WorkflowStepState> Steps { get; }

        public StatementWorkflowEngine()
        {
            _aggregator = new StatementAggregator();
            _reconciliationService = new OrderReconciliationService();

            Steps = new List<WorkflowStepState>
            {
                new(WorkflowStep.ReadProformaInvoice),
                new(WorkflowStep.ReadJobOrder),
                new(WorkflowStep.ReadDeliveryNotes),
                new(WorkflowStep.ValidateQuantities),
                new(WorkflowStep.ReadTaxInvoices),
                new(WorkflowStep.CrossMatchCoverage),
                new(WorkflowStep.GenerateStatementPdf),
                new(WorkflowStep.MergeIntoFinalPdf)
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  MAIN ENTRY POINT
        // ═════════════════════════════════════════════════════════════════

        public async Task<WorkflowResult> RunAsync(
            StatementFilter filter,
            string statementNumber,
            ResolveMissingInvoicesCallback? resolveMissingInvoices = null)
        {
            var result = new WorkflowResult { Steps = Steps };

            try
            {
                await Step1_ReadProformaInvoice(filter, result);
                await Task.Delay(300);

                await Step2_ReadJobOrder(filter, result);
                await Task.Delay(300);

                await Step3_ReadDeliveryNotes(filter, result);
                await Task.Delay(300);

                await Step4_ValidateQuantities(result);
                await Task.Delay(300);

                await Step5_ReadTaxInvoices(filter, statementNumber, result);
                await Task.Delay(300);

                await Step6_CrossMatchCoverage(result);
                await Task.Delay(300);

                // User decision point for uninvoiced DNs
                if (result.UninvoicedDeliveryNotes.Count > 0 &&
                    resolveMissingInvoices != null)
                {
                    var decision = await resolveMissingInvoices(result);

                    switch (decision)
                    {
                        case UserWarningDecision.Abort:
                            return FinalizeCancelled(result,
                                "Workflow aborted by user.");

                        case UserWarningDecision.CreateInvoice:
                            return FinalizeCancelled(result,
                                "Create missing invoice first, then re-run.");

                        case UserWarningDecision.SkipAndContinue:
                            result.AllWarnings.Add(
                                $"Bypassed {result.UninvoicedDeliveryNotes.Count} " +
                                "uninvoiced delivery note(s).");
                            break;
                    }
                }

                await Step7_RegenerateTaxInvoicePdfs(result);
                await Task.Delay(300);

                await Step8_PrepareMerge(result);

                return FinalizeSuccess(result);
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "[Statement] Workflow execution failed.", ex,
                    new LogContext
                    {
                        Module = "Statement",
                        Class = nameof(StatementWorkflowEngine),
                        Operation = "ExecuteAsync"
                    });
                MarkRemainingPendingAsSkipped();
                result.Outcome = WorkflowOutcome.Blocked;
                result.Summary = $"Workflow failed: {ex.Message}";
                return result;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  STEP 1 — READ PROFORMA INVOICE (WARN on Draft)
        // ═════════════════════════════════════════════════════════════════

        private async Task Step1_ReadProformaInvoice(
            StatementFilter filter, WorkflowResult result)
        {
            var step = Steps[0];
            step.MarkRunning("Searching for proforma invoice...");
            await Task.Delay(200);

            var allInvoices = await App.TaxInvoices.GetAllAsync();
            if (allInvoices == null || allInvoices.Count == 0)
            {
                step.MarkWarning("No tax invoices in database.");
                return;
            }

            var matching = allInvoices
                .Where(i => StatementSnapshotFilter.MatchesFilter(i, filter))
                .OrderBy(i => i.IssueDate)
                .FirstOrDefault();

            if (matching == null)
            {
                step.MarkWarning("No tax invoices match filters.");
                return;
            }

            string proformaId = matching.ProformaId ?? "";
            step.AddDetail($"🔍 Found PI reference: {proformaId}");
            await Task.Delay(150);

            if (string.IsNullOrWhiteSpace(proformaId))
            {
                step.MarkWarning("No PI reference on invoice. Continuing.");
                return;
            }

            var pi = await App.Invoices.GetByProformaIdAsync(proformaId);
            if (pi == null)
            {
                step.AddDetail("⚠ PI not found in database");
                step.MarkWarning("PI not found. Continuing.");
                return;
            }

            string piStatus = pi.Status ?? "Unknown";
            bool isDraft = string.Equals(piStatus, "Draft",
                StringComparison.OrdinalIgnoreCase);

            step.AddDetail($"📋 PI status: {piStatus}");
            await Task.Delay(150);

            // Load signed PI attachment
            bool hasSignedPi = false;
            try
            {
                var atts = await App.TaxInvoices
                    .GetAttachmentsAsync(matching.InvoiceNumber);

                var signedPi = atts.FirstOrDefault(a =>
                    string.Equals(a.AttachmentType, "SignedPI",
                        StringComparison.OrdinalIgnoreCase));

                if (signedPi?.FileBytes != null &&
                    signedPi.FileBytes.Length > 0)
                {
                    result.SignedProformaInvoiceBytes = signedPi.FileBytes;
                    result.SignedProformaInvoiceFileName = signedPi.FileName ?? "";
                    hasSignedPi = true;
                    step.AddDetail($"📎 Signed PI: {signedPi.FileName} ✓");
                }
                else
                {
                    step.AddDetail("📎 Signed PI: not found");
                }
            }
            catch (Exception ex)
            {
                step.AddDetail(
                    $"📎 Signed PI: lookup failed ({ex.Message})");
            }

            await Task.Delay(150);

            if (isDraft)
            {
                step.AddWarning($"PI {proformaId} is Draft (Step 8 pending)");
                step.MarkWarning(
                    $"PI {proformaId} · Draft" +
                    (hasSignedPi ? " · Signed ✓" : " · No attachment"));
            }
            else if (!hasSignedPi)
            {
                step.AddWarning($"No signed PI for {proformaId}");
                step.MarkWarning(
                    $"PI {proformaId} · {piStatus} · No signed attachment");
            }
            else
            {
                step.MarkSuccess(
                    $"PI {proformaId} · {piStatus} · Signed ✓");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  STEP 2 — READ JOB ORDER (WARN on Draft)
        // ═════════════════════════════════════════════════════════════════

        private async Task Step2_ReadJobOrder(
            StatementFilter filter, WorkflowResult result)
        {
            var step = Steps[1];
            step.MarkRunning("Searching for job order...");
            await Task.Delay(200);

            var allInvoices = await App.TaxInvoices.GetAllAsync();
            var firstMatching = allInvoices?
                .Where(i => StatementSnapshotFilter.MatchesFilter(i, filter))
                .OrderBy(i => i.IssueDate)
                .FirstOrDefault();

            if (firstMatching == null)
            {
                step.MarkWarning("No invoice to derive JO from.");
                return;
            }

            string proformaId = firstMatching.ProformaId ?? "";
            if (string.IsNullOrWhiteSpace(proformaId))
            {
                step.MarkWarning("No PI reference to lookup JO.");
                return;
            }

            var jobOrders = await App.Jobs
                .GetBySourceProformaIdAsync(proformaId);
            if (jobOrders == null || jobOrders.Count == 0)
            {
                step.AddDetail("⚠ No job order found");
                step.MarkWarning("No JO found. Continuing.");
                return;
            }

            var jo = jobOrders.First();
            step.AddDetail($"🔍 Found JO: {jo.JobOrderId}");
            step.AddDetail($"📋 Status:   {jo.Status}");
            step.AddDetail($"⚡ Priority: {jo.Priority}");
            await Task.Delay(200);

            var releasedStatuses = new[]
            {
                "Released", "InProduction", "Completed", "Delivered"
            };

            bool isReleased = releasedStatuses.Any(s =>
                string.Equals(s, jo.Status,
                    StringComparison.OrdinalIgnoreCase));

            int orderedQty = JobOrderQtyExtractor.ExtractOrderedQtyFromJobOrder(jo);
            result.OrderedQty = orderedQty;
            step.AddDetail($"📦 Ordered qty: {orderedQty} pcs");

            if (!isReleased)
            {
                step.AddWarning(
                    $"JO {jo.JobOrderId} status '{jo.Status}' — not released");
                step.MarkWarning(
                    $"JO {jo.JobOrderId} · {jo.Status} · {orderedQty} pcs " +
                    "(not released — continuing)");
            }
            else
            {
                step.MarkSuccess(
                    $"JO {jo.JobOrderId} · {jo.Status} · {orderedQty} pcs");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  STEP 3 — READ DELIVERY NOTES + LINE-ITEM RECONCILIATION
        //  (Chat 22 v3 — skip Dummy/Rework/Return)
        //
        //  BUG 4 FIX: DN attachment file resolution — 3-strategy fallback:
        //    Strategy 1: Use stored FilePath directly (if file exists)
        //    Strategy 2: Rebuild path from known AppData folder structure
        //    Strategy 3: Search both Images + PDFs subfolders by filename
        //
        //  CHAT 22 v3 ENHANCEMENT:
        //    Per-DN line-item reconciliation shown in log.
        //    Cached PI + TI invoiced map (loaded once, reused per DN).
        //    Flags over-invoicing at DN-processing time (earliest detection).
        // ═════════════════════════════════════════════════════════════════

        private async Task Step3_ReadDeliveryNotes(
            StatementFilter filter, WorkflowResult result)
        {
            var step = Steps[2];
            step.MarkRunning("Loading delivery notes...");
            await Task.Delay(200);

            var allInvoices = await App.TaxInvoices.GetAllAsync();
            var firstMatching = allInvoices?
                .Where(i => StatementSnapshotFilter.MatchesFilter(i, filter))
                .OrderBy(i => i.IssueDate)
                .FirstOrDefault();

            if (firstMatching == null)
            {
                step.MarkSkipped("No invoice to derive DNs from.");
                return;
            }

            string proformaId = firstMatching.ProformaId ?? "";
            var jobOrders = await App.Jobs
                .GetBySourceProformaIdAsync(proformaId);
            if (jobOrders == null || jobOrders.Count == 0)
            {
                step.MarkWarning("No JO — cannot look up DNs.");
                return;
            }

            var jo = jobOrders.First();
            var dnList = await App.DeliveryNotes
                .GetByJobOrderIdAsync(jo.JobOrderId);

            if (dnList == null || dnList.Count == 0)
            {
                step.MarkWarning($"No DNs for JO {jo.JobOrderId}.");
                return;
            }

            step.AddDetail($"🔍 Found {dnList.Count} total delivery note(s)");
            step.AddDetail($"📁 Attachment root: {DnAttachmentRoot}");
            await Task.Delay(150);

            // ── CHAT 22 v3 — Pre-load PI line-item data + TI invoiced map ─
            // Load ONCE, reuse for every DN in this JO. Massive perf win
            // when JO has many DNs (avoids reloading same PI + TIs N times).
            var piLineMap = await LoadPiLineMapAsync(proformaId, step);
            var invoicedMap = await LoadInvoicedMapForStep3Async(
                proformaId, step);

            bool hasReconciliationData = piLineMap != null &&
                                          piLineMap.Count > 0;

            if (hasReconciliationData)
            {
                step.AddDetail(
                    $"📊 Reconciliation data loaded: " +
                    $"{piLineMap!.Count} PI line(s), " +
                    $"{invoicedMap.Count} invoiced entry(s)");
                await Task.Delay(100);
            }

            int totalDelivered = 0;
            int includedCount = 0;
            int skippedCount = 0;
            int withAttachments = 0;
            int withoutAttachments = 0;

            // Running total of delivered qty per line across processed DNs
            // (so we can compute "previously delivered" for each DN in order)
            var runningDeliveredMap = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var dn in dnList)
            {
                string dnType = dn.DeliveryType ?? "";

                // Skip Dummy/Rework/Return
                if (!ValidDeliveryTypes.Contains(dnType))
                {
                    skippedCount++;
                    step.AddDetail(
                        $"  ⏭ {dn.DeliveryNoteId}: " +
                        $"{dn.TotalDeliveredQty} pcs · {dnType} · SKIPPED");
                    await Task.Delay(80);
                    continue;
                }

                includedCount++;
                totalDelivered += dn.TotalDeliveredQty;

                // Load attachment records from DB
                var attachments = await App.DeliveryNotes
                    .GetAttachmentsAsync(dn.DeliveryNoteId);

                var dnAtt = new WorkflowDeliveryNoteAttachment
                {
                    DeliveryNoteId = dn.DeliveryNoteId,
                    DeliveryNoteType = dnType,
                    DeliveredQty = dn.TotalDeliveredQty,
                    DeliveryDate = dn.DeliveryDate
                };

                if (attachments != null && attachments.Count > 0)
                {
                    step.AddDetail(
                        $"  📋 {dn.DeliveryNoteId}: " +
                        $"{attachments.Count} attachment record(s) in DB");

                    int loadedCount = 0;
                    int failedCount = 0;

                    foreach (var att in attachments)
                    {
                        string storedPath = att.FilePath ?? "";
                        string fileName = att.FileName ?? "";
                        string attType = att.AttachmentType ?? "Image";

                        Debug.WriteLine(
                            $"[Workflow] DN {dn.DeliveryNoteId} " +
                            $"att: file='{fileName}' " +
                            $"storedPath='{storedPath}'");

                        string? resolvedPath = ResolveAttachmentPath(
                            storedPath, fileName, attType,
                            dn.DeliveryNoteId, step);

                        if (resolvedPath != null)
                        {
                            try
                            {
                                byte[] bytes =
                                    await File.ReadAllBytesAsync(resolvedPath);

                                if (bytes.Length > 0)
                                {
                                    dnAtt.AttachmentBytes.Add(bytes);
                                    dnAtt.AttachmentFileNames.Add(fileName);
                                    loadedCount++;
                                    Debug.WriteLine(
                                        $"[Workflow]   ✓ Loaded: " +
                                        $"{bytes.Length:N0} bytes");
                                }
                                else
                                {
                                    failedCount++;
                                    Debug.WriteLine(
                                        $"[Workflow]   ✗ Empty file: " +
                                        $"'{resolvedPath}'");
                                    step.AddDetail(
                                        $"    ⚠ Empty file: {fileName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                failedCount++;
                                Debug.WriteLine(
                                    $"[Workflow]   ✗ Read error " +
                                    $"'{resolvedPath}': {ex.Message}");
                                step.AddDetail(
                                    $"    ⚠ Read failed: {fileName} " +
                                    $"({ex.Message})");
                                step.AddWarning(
                                    $"DN {dn.DeliveryNoteId}: " +
                                    $"read failed '{fileName}'");
                            }
                        }
                        else
                        {
                            // File not found via any strategy
                            failedCount++;
                            step.AddDetail(
                                $"    ❌ File not found on disk: {fileName}");
                            step.AddWarning(
                                $"DN {dn.DeliveryNoteId}: " +
                                $"'{fileName}' not found on disk");
                            Debug.WriteLine(
                                $"[Workflow]   ✗ File not found " +
                                $"anywhere: '{fileName}'");
                        }
                    }

                    if (loadedCount > 0)
                    {
                        withAttachments++;
                        step.AddDetail(
                            $"  ✅ {dn.DeliveryNoteId}: " +
                            $"{dn.TotalDeliveredQty} pcs · {dn.Status} · " +
                            $"{loadedCount}/{attachments.Count} file(s) loaded" +
                            (failedCount > 0
                                ? $" · {failedCount} missing"
                                : ""));
                    }
                    else
                    {
                        // Records exist in DB but zero files found on disk
                        withoutAttachments++;
                        dnAtt.WasBypassed = true;
                        step.AddDetail(
                            $"  ⚠ {dn.DeliveryNoteId}: " +
                            $"{attachments.Count} DB record(s) · " +
                            $"0 files found on disk");
                        step.AddWarning(
                            $"DN {dn.DeliveryNoteId}: " +
                            $"{attachments.Count} record(s) in DB " +
                            $"but no files found on disk");
                    }
                }
                else
                {
                    // No attachment records in DB at all
                    withoutAttachments++;
                    dnAtt.WasBypassed = true;
                    step.AddDetail(
                        $"  ⚠ {dn.DeliveryNoteId}: " +
                        $"{dn.TotalDeliveredQty} pcs · {dn.Status} · " +
                        $"No attachment records in DB");
                    step.AddWarning(
                        $"DN {dn.DeliveryNoteId} has no attachments " +
                        $"({dn.TotalDeliveredQty} pcs)");
                }

                // ═════════════════════════════════════════════════════════
                //  CHAT 22 v3 — Per-DN Line-Item Reconciliation
                //  Loads THIS DN's own items, shows per-line breakdown
                //  cross-referenced with PI ordered + running delivered
                //  + total invoiced. Detects over-invoicing per DN.
                // ═════════════════════════════════════════════════════════

                if (hasReconciliationData)
                {
                    await LogDnLineReconciliation(
                        dn.DeliveryNoteId,
                        piLineMap!,
                        invoicedMap,
                        runningDeliveredMap,
                        step);
                }

                result.SignedDeliveryNotes.Add(dnAtt);
                await Task.Delay(100);
            }

            result.DeliveredQty = totalDelivered;

            string summary =
                $"{includedCount} DN(s) · {totalDelivered} pcs delivered";
            if (skippedCount > 0)
                summary +=
                    $" · {skippedCount} skipped (Dummy/Rework)";

            if (withoutAttachments > 0)
                step.MarkWarning(
                    summary +
                    $" · {withoutAttachments} missing attachment(s)");
            else
                step.MarkSuccess(summary + " · All attachments ✓");
        }

        // ═════════════════════════════════════════════════════════════════
        //  CHAT 22 v3 — RECONCILIATION HELPERS FOR STEP 3
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads PI line items (spec, serial, glass ref, ordered qty)
        /// into a dictionary keyed by "specGroupNumber|serialNumber".
        /// Returns null if PI not found or has no line items.
        /// Called ONCE per Step 3 run, cached for all DN iterations.
        /// </summary>
        private static async Task<Dictionary<string, PiLineInfo>?>
            LoadPiLineMapAsync(string proformaId, WorkflowStepState step)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return null;

            try
            {
                var pi = await App.Invoices.GetByProformaIdAsync(proformaId);
                if (pi == null)
                {
                    step.AddDetail(
                        $"⚠ Reconciliation: PI {proformaId} not found");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(pi.FullSnapshotJson) ||
                    pi.FullSnapshotJson == "{}")
                {
                    step.AddDetail(
                        $"⚠ Reconciliation: PI {proformaId} has no snapshot");
                    return null;
                }

                var snapshot = JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(
                    pi.FullSnapshotJson, _jsonOpts);

                if (snapshot?.SpecGroups == null ||
                    snapshot.SpecGroups.Count == 0)
                {
                    return null;
                }

                var map = new Dictionary<string, PiLineInfo>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var spec in snapshot.SpecGroups)
                {
                    if (spec.ItemRows == null) continue;

                    foreach (var row in spec.ItemRows)
                    {
                        string key = $"{spec.GroupNumber}|{row.SerialNumber}";
                        map[key] = new PiLineInfo
                        {
                            SpecGroupNumber = spec.GroupNumber,
                            SerialNumber = row.SerialNumber,
                            GlassRef = row.GlassRef ?? "",
                            OrderedQty = row.Qty
                        };
                    }
                }

                return map;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"[Workflow] PI line map load error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sums invoiced qty per line item across ALL non-cancelled TIs
        /// for a proforma family. Returns dictionary keyed by
        /// "specGroupNumber|serialNumber".
        /// Called ONCE per Step 3 run.
        /// </summary>
        private static async Task<Dictionary<string, int>>
            LoadInvoicedMapForStep3Async(
                string proformaId, WorkflowStepState step)
        {
            var map = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(proformaId)) return map;

            try
            {
                var tis = await App.TaxInvoices
                    .GetByProformaIdAsync(proformaId);

                if (tis == null || tis.Count == 0) return map;

                var activeStatuses = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                { "Issued", "Paid", "PartiallyPaid", "Overdue" };

                var activeTis = tis
                    .Where(ti => activeStatuses.Contains(ti.Status ?? ""))
                    .ToList();

                foreach (var tiListItem in activeTis)
                {
                    try
                    {
                        var fullTi = await App.TaxInvoices
                            .GetByInvoiceNumberAsync(
                                tiListItem.InvoiceNumber);

                        if (fullTi?.Items == null) continue;

                        foreach (var item in fullTi.Items)
                        {
                            var key = $"{item.SpecGroupNumber}|" +
                                      $"{item.SerialNumber}";

                            if (map.ContainsKey(key))
                                map[key] += item.Qty;
                            else
                                map[key] = item.Qty;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"[Workflow] TI load error " +
                            $"{tiListItem.InvoiceNumber}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"[Workflow] Invoiced map load error: {ex.Message}");
            }

            return map;
        }

        /// <summary>
        /// Logs per-line reconciliation for ONE DN.
        /// Loads DN's own item rows, cross-references with cached PI + TI
        /// data plus the running-delivered map (which tracks cumulative
        /// deliveries across previously-processed DNs in this Step 3 run).
        /// Updates the running-delivered map after logging.
        /// Adds Step 3 warnings for over-invoiced/over-delivered lines.
        /// </summary>
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

                    await Task.Delay(50);

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
                Debug.WriteLine(
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
                Debug.WriteLine($"[Workflow]   ✓ Strategy 1 (stored path)");
                return storedPath;
            }

            Debug.WriteLine(
                $"[Workflow]   ✗ Strategy 1 failed: " +
                $"'{storedPath}' — " +
                (string.IsNullOrWhiteSpace(storedPath)
                    ? "path is empty"
                    : "file not found"));

            if (string.IsNullOrWhiteSpace(fileName))
            {
                Debug.WriteLine(
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
                Debug.WriteLine(
                    $"[Workflow]   ✓ Strategy 2 (rebuilt): '{rebuilt}'");
                step.AddDetail($"    🔧 Rebuilt path: {fileName}");
                return rebuilt;
            }

            Debug.WriteLine(
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
                    Debug.WriteLine(
                        $"[Workflow]   ✓ Strategy 3 ({subfolder}): " +
                        $"'{tryPath}'");
                    step.AddDetail(
                        $"    🔧 Found in {subfolder}: {fileName}");
                    return tryPath;
                }

                Debug.WriteLine(
                    $"[Workflow]   ✗ Strategy 3 ({subfolder}): " +
                    $"not found");
            }

            return null; // not found via any strategy
        }

        // ═════════════════════════════════════════════════════════════════
        //  STEP 4 — VALIDATE QUANTITIES
        //  BUG 2 FIX: partial delivery (balance > 0) → MarkWarning
        // ═════════════════════════════════════════════════════════════════

        private async Task Step4_ValidateQuantities(WorkflowResult result)
        {
            var step = Steps[3];
            step.MarkRunning("Comparing quantities...");
            await Task.Delay(200);

            int ordered = result.OrderedQty;
            int delivered = result.DeliveredQty;
            int balance = Math.Max(0, ordered - delivered);
            int pending = result.PendingProductionQty;

            step.AddDetail($"📦 Total Ordered:       {ordered} pcs");
            step.AddDetail($"🚚 Total Delivered:     {delivered} pcs");
            step.AddDetail($"📊 Balance to Deliver:  {balance} pcs");
            step.AddDetail($"⏳ Pending Production:  {pending} pcs");

            await Task.Delay(200);

            if (ordered > 0)
            {
                double pct = (double)delivered / ordered * 100.0;
                step.AddDetail(
                    $"📈 Delivery Progress:   {pct:0.#}% " +
                    $"({delivered} of {ordered})");
            }

            await Task.Delay(100);

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
            await Task.Delay(200);

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

                await Task.Delay(150);

                foreach (var row in snapshot.InvoiceRows)
                {
                    step.AddDetail(
                        $"  🧾 {row.InvoiceNumber} · {row.InvoiceType} · " +
                        $"{row.TotalQty} pcs · " +
                        $"AED {row.GrandTotal:N2} · {row.Status}");
                    await Task.Delay(80);
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
            await Task.Delay(200);

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
            await Task.Delay(200);

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
                    await Task.Delay(100);
                }
                else
                {
                    step.AddDetail(
                        $"  ✅ {dnAtt.DeliveryNoteId}: " +
                        $"{dnAtt.DeliveredQty} pcs — invoiced");
                    await Task.Delay(80);
                }
            }

            // ═══════════════════════════════════════════════════════════════
            //  PART B: LINE-ITEM RECONCILIATION (Chat 22 v3)
            //  Cross-reference PI ordered qty vs DN delivered qty vs
            //  TI invoiced qty per line item. Flag over-invoicing.
            // ═══════════════════════════════════════════════════════════════

            step.AddDetail("");
            step.AddDetail("── Line-Item Reconciliation ──");
            await Task.Delay(150);

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
                await Task.Delay(150);

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
                        await Task.Delay(60);
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

                    await Task.Delay(100);
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

            await Task.Delay(150);

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
            await Task.Delay(200);

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
                await Task.Delay(150);
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

        // ═════════════════════════════════════════════════════════════════
        //  STEP 8 — PREPARE MERGE
        //  BUG 3 FIX: tiAttCount included in sections total + summary
        // ═════════════════════════════════════════════════════════════════

        private async Task Step8_PrepareMerge(WorkflowResult result)
        {
            var step = Steps[7];
            step.MarkRunning("Preparing final PDF merge...");
            await Task.Delay(200);

            int sections = 1; // Statement summary always present

            // Signed PI
            bool hasPi = result.SignedProformaInvoiceBytes != null &&
                         result.SignedProformaInvoiceBytes.Length > 0;
            if (hasPi)
            {
                sections++;
                step.AddDetail("📎 Signed PI: included");
            }
            else
            {
                step.AddDetail("📎 Signed PI: not available");
            }

            // DN attachments
            int dnAttCount = result.SignedDeliveryNotes
                .Sum(d => d.AttachmentBytes.Count);
            sections += dnAttCount;
            step.AddDetail(
                $"📎 DN attachments: {dnAttCount} file(s)");

            // Invoice PDFs (valid + placeholders both count as sections)
            int validInv = result.TaxInvoicePdfs.Count(p => p.IsValid);
            int failedInv = result.TaxInvoicePdfs.Count(p => !p.IsValid);
            sections += validInv + failedInv;
            step.AddDetail(
                $"🧾 Invoice PDFs: {validInv} valid" +
                (failedInv > 0
                    ? $" · {failedInv} placeholder(s)"
                    : ""));

            // BUG 3 FIX: TI attachments per invoice
            int tiAttCount = result.TaxInvoicePdfs
                .Sum(p => p.AttachmentFileCount);
            sections += tiAttCount;
            step.AddDetail(
                $"📎 TI attachments: {tiAttCount} file(s) " +
                $"(across {result.TaxInvoicePdfs.Count} invoice(s))");

            await Task.Delay(200);

            step.AddDetail($"📄 Total sections: {sections}");
            step.AddDetail("");
            step.AddDetail(
                $"📊 Summary: Statement" +
                (hasPi ? " + Signed PI" : "") +
                $" + {dnAttCount} DN att(s)" +
                $" + {validInv} Invoice PDF(s)" +
                $" + {tiAttCount} TI att(s)" +
                $" = {sections} total");

            await Task.Delay(200);

            step.MarkSuccess(
                $"Ready to merge {sections} sections into final PDF ✓");
        }

        // ═════════════════════════════════════════════════════════════════
        //  FINALIZATION
        // ═════════════════════════════════════════════════════════════════

        private WorkflowResult FinalizeBlocked(WorkflowResult result)
        {
            MarkRemainingPendingAsSkipped();
            result.Outcome = WorkflowOutcome.Blocked;
            result.Summary = "Workflow blocked. Fix the issues above.";
            CollectAllWarnings(result);
            return result;
        }

        private WorkflowResult FinalizeCancelled(
            WorkflowResult result, string msg)
        {
            MarkRemainingPendingAsSkipped();
            result.Outcome = WorkflowOutcome.Cancelled;
            result.Summary = msg;
            CollectAllWarnings(result);
            return result;
        }

        private WorkflowResult FinalizeSuccess(WorkflowResult result)
        {
            CollectAllWarnings(result);

            if (result.AllWarnings.Count > 0)
            {
                result.Outcome = WorkflowOutcome.Warning;
                result.Summary =
                    $"Statement ready with " +
                    $"{result.AllWarnings.Count} warning(s).";
            }
            else
            {
                result.Outcome = WorkflowOutcome.Success;
                result.Summary = "Statement ready. All checks passed ✓";
            }

            return result;
        }

        private void MarkRemainingPendingAsSkipped()
        {
            foreach (var step in Steps)
                if (step.Status == StepStatus.Pending)
                    step.MarkSkipped();
        }

        private void CollectAllWarnings(WorkflowResult result)
        {
            result.AllWarnings.Clear();
            foreach (var step in Steps)
                foreach (var w in step.Warnings)
                    result.AllWarnings.Add(w);
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════


    }
}