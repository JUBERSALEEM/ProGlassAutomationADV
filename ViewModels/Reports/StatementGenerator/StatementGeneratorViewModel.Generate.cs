// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.Generate.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.3 — StatementGeneratorViewModel partial split. This part owns the
// Generate members; fields + constructor live in StatementGeneratorViewModel.cs.
// ═══════════════════════════════════════════════════════════════════════

using PdfSharp.Pdf;
using ProGlassAutomation.Services.Logging;
using PdfSharp.Pdf.IO;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.Reports.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Database;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public partial class StatementGeneratorViewModel : ViewModelBase
    {
        // ═════════════════════════════════════════════════════════════════
        //  GENERATE — INLINE EXECUTION (unchanged from Chat 21 v2)
        // ═════════════════════════════════════════════════════════════════

        private async Task RunGenerateAsync()
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.StatementGenerate,
                "generate a statement"))
                return;

            if (_previewSnapshot == null) return;

            var selectedNumbers = PreviewVM.GetSelectedInvoiceNumbers();
            if (selectedNumbers.Count == 0)
            {
                StatusMessage = "No invoices selected. Please select at least one invoice.";
                return;
            }

            IsGenerating = true;
            StatusMessage = "Preparing workflow…";
            var generationStart = DateTime.Now;

            var engine = new StatementWorkflowEngine();
            PrepareForNewWorkflowRun();

            // Phase 1 C-1 fix: non-blocking dispatch — the background thread
            // no longer stalls waiting for the UI thread to process this.
            ViewModels.Shared.UiDispatch.Run(() =>
            {
                LastWorkflowSteps.Clear();
                foreach (var step in engine.Steps)
                    LastWorkflowSteps.Add(step);
            });

            SubscribeToLiveStepUpdates(engine.Steps);

            LastWorkflowStatusText = "Running Pipeline…";
            LiveAddLogEntry(LogSeverity.Section, "═══ STATEMENT GENERATION STARTED ═══");
            LiveAddLogEntry(LogSeverity.Info, $"› Started at {DateTime.Now:HH:mm:ss}");
            LiveAddLogEntry(LogSeverity.Info, $"› Selected invoices: {selectedNumbers.Count}");

            WorkflowResult? wfResult = null;
            string pdfPath = "";

            try
            {
                var filter = BuildFilter();

                // ── Duplicate guard ─────────────────────────────────────────
                // If a statement with the same filters already exists, ask the
                // user before producing another identical one (prevents the
                // "4 statements, same filters" pile-up).
                var duplicate = await FindDuplicateStatementAsync(filter);
                if (duplicate != null && !IsEditMode)
                {
                    IsGenerating = false;
                    LastWorkflowStatusText = "Duplicate detected";
                    LiveAddLogEntry(LogSeverity.Warning,
                        $"› ⚠ A statement with these filters already exists: {duplicate.StatementNumber}");
                    var confirm = _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.StatementWithTheSameFilters"), duplicate.StatementNumber, duplicate.SubTitle),
                        LocalizationService.Instance.T("Msg.CapDuplicateStatement"),
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning,
                        System.Windows.MessageBoxResult.No);
                    if (confirm != System.Windows.MessageBoxResult.Yes)
                    {
                        StatusMessage = "Generation cancelled — duplicate statement exists.";
                        return;
                    }
                    IsGenerating = true;
                }

                var number = BuildStatementNumber(filter);

                LiveAddLogEntry(LogSeverity.Info, $"› Statement number: {number}");

                wfResult = await engine.RunAsync(
                    filter,
                    number,
                    ShowMissingInvoicesDialogAsync,
                    ShowMissingAttachmentsDialogAsync);

                if (wfResult == null)
                {
                    StatusMessage = "Workflow was cancelled.";
                    LastWorkflowStatusText = "Cancelled";
                    LiveAddLogEntry(LogSeverity.Warning, "› Workflow cancelled — no result returned");
                    return;
                }

                if (!wfResult.ShouldGeneratePdf)
                {
                    StatusMessage = wfResult.Summary;
                    LastWorkflowStatusText = wfResult.Outcome switch
                    {
                        WorkflowOutcome.Cancelled => "Cancelled by User",
                        WorkflowOutcome.Blocked => "Pipeline Blocked",
                        _ => "Cannot Generate PDF"
                    };
                    LiveAddLogEntry(LogSeverity.Warning, $"› {wfResult.Summary}");
                    LiftWorkflowResultAfterCompletion(wfResult, "", 0);
                    return;
                }

                if (wfResult.Snapshot == null)
                {
                    StatusMessage = "Workflow returned no snapshot.";
                    LastWorkflowStatusText = "No Data";
                    LiveAddLogEntry(LogSeverity.Error, "› ✗ Engine returned null snapshot");
                    return;
                }

                var selectedSet = selectedNumbers.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var filteredRows = wfResult.Snapshot.InvoiceRows
                    .Where(r => selectedSet.Contains(r.InvoiceNumber))
                    .ToList();

                if (filteredRows.Count == 0)
                {
                    StatusMessage = "No selected invoices found in workflow result.";
                    LiveAddLogEntry(LogSeverity.Error, "› ✗ Selected invoices not found in result");
                    return;
                }

                wfResult.Snapshot = StatementSnapshotProjector.FilterByInvoices(
                    wfResult.Snapshot, selectedNumbers);
                wfResult.TaxInvoicePdfs = wfResult.TaxInvoicePdfs
                    .Where(p => selectedSet.Contains(p.InvoiceNumber))
                    .ToList();

                StatusMessage = "Generating final PDF…";
                LiveAddLogEntry(LogSeverity.Section, "═══ PDF GENERATION ═══");
                LiveAddLogEntry(LogSeverity.Info, "› Merging statement + invoices + attachments…");

                pdfPath = await StatementPdfExporter.ExportFromWorkflowAsync(wfResult);

                LiveAddLogEntry(LogSeverity.Success,
                    $"› ✓ PDF ready: {Path.GetFileName(pdfPath)}");

                double elapsedSec = (DateTime.Now - generationStart).TotalSeconds;
                LiftWorkflowResultAfterCompletion(wfResult, pdfPath, elapsedSec);

                AuditService.Log(
                    AuditSections.Statement, "Statement generated",
                    $"{wfResult.Snapshot.Header.StatementNumber} — " +
                    $"{wfResult.Snapshot.Financial.TotalInvoiceCount} invoices, " +
                    $"{elapsedSec:0.#}s",
                    "STMT", wfResult.Snapshot.Header.StatementNumber);

                StatusMessage = "Saving statement record…";
                LiveAddLogEntry(LogSeverity.Info, "› Saving to database…");

                bool dbSaveSucceeded = false;
                try
                {
                    var entity = new SavedStatement
                    {
                        StatementNumber = wfResult.Snapshot.Header.StatementNumber,
                        Title = wfResult.Snapshot.Header.Title,
                        FilterCompany = filter.CompanyName ?? "",
                        FilterCustomer = filter.CustomerName ?? "",
                        FilterProject = filter.ProjectName ?? "",
                        FilterSalesman = filter.SalesmanName ?? "",
                        FilterDateFrom = filter.DateFrom,
                        FilterDateTo = filter.DateTo,
                        InvoiceCount = wfResult.Snapshot.Financial.TotalInvoiceCount,
                        TotalInvoiced = wfResult.Snapshot.Financial.TotalInvoiced,
                        TotalOutstanding = wfResult.Snapshot.Financial.TotalOutstanding,
                        OverdueAmount = wfResult.Snapshot.Financial.OverdueAmount,
                        PdfPath = pdfPath,
                        HasAttachments = true,
                        FullSnapshotJson = JsonSerializer.Serialize(wfResult.Snapshot),
                        FilterJson = JsonSerializer.Serialize(filter)
                    };

                    foreach (var row in wfResult.Snapshot.InvoiceRows)
                    {
                        entity.LineItems.Add(new StatementLineItem
                        {
                            InvoiceNumber = row.InvoiceNumber,
                            ProformaId = row.ProformaId,
                            DeliveryNoteRef = row.DeliveryNoteRef,
                            InvoiceType = row.InvoiceType,
                            Status = row.Status,
                            IssueDate = row.IssueDate,
                            DueDate = row.DueDate,
                            TotalQty = row.TotalQty,
                            TotalSqm = row.TotalSqm,
                            Subtotal = row.Subtotal,
                            VatAmount = row.VatAmount,
                            GrandTotal = row.GrandTotal,
                            PaidAmount = row.PaidAmount,
                            DaysOverdue = row.DaysOverdue
                        });
                    }

                    if (_editId > 0)
                    {
                        entity.Id = _editId;
                        await Task.Run(() => _repo.UpdateAsync(entity));
                        dbSaveSucceeded = true;
                        LiveAddLogEntry(LogSeverity.Success, "› ✓ Updated in database");
                    }
                    else
                    {
                        int retryCount = 0;
                        const int maxRetries = 5;
                        while (retryCount < maxRetries)
                        {
                            try
                            {
                                await Task.Run(() => _repo.InsertAsync(entity));
                                dbSaveSucceeded = true;
                                LiveAddLogEntry(LogSeverity.Success,
                                    $"› ✓ Saved as {entity.StatementNumber}");
                                break;
                            }
                            catch (Exception retryEx) when (
                                retryEx.Message.Contains("UNIQUE constraint failed") ||
                                retryEx.InnerException?.Message.Contains("UNIQUE constraint failed") == true)
                            {
                                retryCount++;
                                LiveAddLogEntry(LogSeverity.Warning,
                                    $"› ⚠ Number {entity.StatementNumber} taken — retrying ({retryCount}/{maxRetries})…");

                                if (retryCount >= maxRetries) throw;

                                string newNumber = BuildStatementNumber(filter);
                                entity.StatementNumber = newNumber;
                                wfResult.Snapshot.Header.StatementNumber = newNumber;
                            }
                        }
                    }
                }
                catch (Exception dbEx)
                {
                    LiveAddLogEntry(LogSeverity.Error,
                        $"› ✗ DB save failed: {dbEx.Message}");
                    LiveAddLogEntry(LogSeverity.Warning,
                        $"› ⚠ PDF is available on disk but not registered in statement list");
                    AppLogService.Debug($"[StatementGenerator] DB save error: {dbEx}");
                }

                LiveAddLogEntry(LogSeverity.Section, "═══ COMPLETE ═══");
                LiveAddLogEntry(LogSeverity.Success,
                    $"› ✓ Statement generated in {elapsedSec:N1}s");
                LiveAddLogEntry(LogSeverity.Info,
                    $"› PDF: {Path.GetFileName(pdfPath)}");

                StatusMessage = dbSaveSucceeded
                    ? (wfResult.AllWarnings.Count > 0
                        ? $"✓ Statement generated with {wfResult.AllWarnings.Count} warning(s)."
                        : "✓ Statement generated successfully.")
                    : "⚠ PDF generated but database save failed. Check log.";

                LiveAddLogEntry(LogSeverity.Info,
                    "› Click 'Download PDF' in the results section to view the PDF");

                if (dbSaveSucceeded)
                {
                    LiveAddLogEntry(LogSeverity.Info,
                        "› Click 'Back' in the top bar to return to the statement list");
                    _pendingListRefresh = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Generation failed: {ex.Message}";
                LastWorkflowStatusText = "Pipeline Failed";
                LiveAddLogEntry(LogSeverity.Error, $"› ✗ FATAL ERROR: {ex.Message}");
                LiveAddLogEntry(LogSeverity.Error, $"› {ex.GetType().Name}");
                AppLogService.Debug($"[StatementGenerator] Generate error: {ex}");
            }
            finally
            {
                UnsubscribeFromLiveStepUpdates(engine.Steps);
                IsGenerating = false;
                LastWorkflowProgressPercent = 100;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  MISSING INVOICES DIALOG
        // ═════════════════════════════════════════════════════════════════

        private async Task<UserWarningDecision> ShowMissingInvoicesDialogAsync(
            WorkflowResult partialResult)
        {
            LiveAddLogEntry(LogSeverity.Warning,
                $"› ⚠ Uninvoiced deliveries detected — awaiting user decision");

            // Demo recordings run unattended. Auto-resolve the decision so the
            // pipeline never blocks on the modal WorkflowWarningDialog and the
            // demo can capture Execute (step 3) and Review (step 4).
            if (_database.IsDemo)
            {
                LiveAddLogEntry(LogSeverity.Info,
                    "› Demo mode — auto-continuing past uninvoiced deliveries");
                return UserWarningDecision.SkipAndContinue;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                return UserWarningDecision.Abort;

            return await dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var dialog = _dialogs.Create<WorkflowWarningDialog>(partialResult);
                    dialog.ShowDialog();

                    LiveAddLogEntry(LogSeverity.Info,
                        $"› User decision: {dialog.UserDecision}");

                    return dialog.UserDecision;
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[StatementGenerator] Warning dialog error: {ex}");
                    return UserWarningDecision.Abort;
                }
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  MISSING ATTACHMENTS DIALOG
        // ═════════════════════════════════════════════════════════════════

        private async Task<UserWarningDecision> ShowMissingAttachmentsDialogAsync(
            MissingAttachmentSummary summary)
        {
            LiveAddLogEntry(LogSeverity.Warning,
                "› ⚠ Missing signed attachments detected — awaiting user decision");

            // Demo recordings run unattended.
            if (_database.IsDemo)
            {
                LiveAddLogEntry(LogSeverity.Info,
                    "› Demo mode — auto-continuing past missing attachments");
                return UserWarningDecision.SkipAndContinue;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                return UserWarningDecision.Abort;

            return await dispatcher.InvokeAsync(() =>
            {
                var missing = new List<string>();
                if (summary.ProformaMissing)
                    missing.Add("•  Signed Proforma Invoice (attach it in Proforma Invoice → Step 8).");
                foreach (var dn in summary.MissingDeliveryNoteIds)
                    missing.Add($"•  Signed Delivery Note {dn} (attach the signed copy in the Delivery Note module).");

                var guide =
                    "\n\nHOW TO COMPLETE THE STATEMENT:\n" +
                    "  1. In Proforma Invoice → Step 8, attach the signed PI (JPG or PDF).\n" +
                    "  2. In Delivery Note, open the dispatched DN and attach the signed copy.\n" +
                    "  3. Return to Statement Generator and run the pipeline again.\n\n" +
                    "Continue generating without these signed attachments?";

                var result = _dialogs.Show(
                    "The following required signed attachments are missing:\n\n" +
                    string.Join("\n", missing) + guide,
                    LocalizationService.Instance.T("Msg.CapMissingSignedAttachments"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxResult.No);

                return result == System.Windows.MessageBoxResult.Yes
                    ? UserWarningDecision.SkipAndContinue
                    : UserWarningDecision.Abort;
            });
        }

    }
}
