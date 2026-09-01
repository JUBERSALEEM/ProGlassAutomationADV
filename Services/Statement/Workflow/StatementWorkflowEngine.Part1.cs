// ═══════════════════════════════════════════════════════════════════════
// FILE: StatementWorkflowEngine.Part1.cs (Phase 3.8 split — member census verified)
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

        // Proforma Invoice Step 8 attachment root (matches ProformaAttachmentService)
        // Structure: ProformaInvoices\\Attachments\\{ProformaId}\\{Images|PDFs}\\{filename}
        private static string PiAttachmentRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation",
            "ProformaInvoices",
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
            ResolveMissingInvoicesCallback? resolveMissingInvoices = null,
            ResolveMissingAttachmentsCallback? resolveMissingAttachments = null)
        {
            var result = new WorkflowResult { Steps = Steps };

            try
            {
                await Step1_ReadProformaInvoice(filter, result);
                await Task.Delay(130);

                await Step2_ReadJobOrder(filter, result);
                await Task.Delay(130);

                await Step3_ReadDeliveryNotes(filter, result);
                await Task.Delay(130);

                // ── Missing signed-attachment gate ──────────────────────────
                // Require a signed PI + signed DNs before continuing; prompt the
                // user to attach what is missing (or abort the statement).
                var missingAttachments = BuildMissingAttachmentSummary(result);
                if (missingAttachments != null && resolveMissingAttachments != null)
                {
                    var attDecision = await resolveMissingAttachments(missingAttachments);
                    if (attDecision == UserWarningDecision.Abort)
                        return FinalizeCancelled(result,
                            "Statement aborted — required signed attachments are missing.");
                }

                await Step4_ValidateQuantities(result);
                await Task.Delay(130);

                await Step5_ReadTaxInvoices(filter, statementNumber, result);
                await Task.Delay(130);

                await Step6_CrossMatchCoverage(result);
                await Task.Delay(130);

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
                await Task.Delay(130);

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
            await Task.Delay(90);

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
            await Task.Delay(70);

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
            await Task.Delay(70);

            // Load signed PI attachment (from Tax Invoice "SignedPI" record)
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
                    step.AddDetail("📎 Signed PI: not found in Tax Invoice");
                }
            }
            catch (Exception ex)
            {
                step.AddDetail(
                    $"📎 Signed PI: lookup failed ({ex.Message})");
            }

            // ── Load Proforma Invoice Step 8 attachments (JPG + PDF) ───────
            // The PI Step 8 attachments live in a separate manifest (not the
            // Tax Invoice attachment table). Read every image + PDF so they
            // are merged into the statement, not dropped. Fall back to a
            // rebuilt path when the stored FilePath is stale (drive/user
            // changed), same as the DN attachment resolver.
            try
            {
                var piAttachmentService = new ProformaAttachmentService();
                var manifest = await piAttachmentService
                    .LoadManifestAsync(proformaId);

                result.ProformaAttachments.Clear();
                int loaded = 0;
                foreach (var record in manifest)
                {
                    string? resolved = ResolvePiAttachmentPath(record, proformaId);
                    if (resolved == null) continue;

                    try
                    {
                        var bytes = await File.ReadAllBytesAsync(resolved);
                        if (bytes.Length == 0) continue;

                        result.ProformaAttachments.Add(
                            (bytes, record.FileName ?? ""));
                        loaded++;
                    }
                    catch (Exception fileEx)
                    {
                        step.AddWarning(
                            $"PI attachment '{record.FileName}' read failed: {fileEx.Message}");
                    }
                }

                if (loaded > 0)
                {
                    hasSignedPi = true;   // PI has at least one Step 8 file
                    step.AddDetail(
                        $"📎 PI Step 8: {loaded} attachment(s) loaded");
                }
                else
                {
                    step.AddDetail("📎 PI Step 8: no attachments");
                }
            }
            catch (Exception ex)
            {
                step.AddDetail(
                    $"📎 PI Step 8: load failed ({ex.Message})");
            }

            await Task.Delay(70);

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
            await Task.Delay(90);

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
            await Task.Delay(90);

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
            await Task.Delay(90);

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
            await Task.Delay(70);

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
                await Task.Delay(45);
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
                    await Task.Delay(35);
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

                        AppLogService.Debug(
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
                                    AppLogService.Debug(
                                        $"[Workflow]   ✓ Loaded: " +
                                        $"{bytes.Length:N0} bytes");
                                }
                                else
                                {
                                    failedCount++;
                                    AppLogService.Debug(
                                        $"[Workflow]   ✗ Empty file: " +
                                        $"'{resolvedPath}'");
                                    step.AddDetail(
                                        $"    ⚠ Empty file: {fileName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                failedCount++;
                                AppLogService.Debug(
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
                            AppLogService.Debug(
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
                await Task.Delay(45);
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
                AppLogService.Debug(
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
                        AppLogService.Debug(
                            $"[Workflow] TI load error " +
                            $"{tiListItem.InvoiceNumber}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
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
    }
}
