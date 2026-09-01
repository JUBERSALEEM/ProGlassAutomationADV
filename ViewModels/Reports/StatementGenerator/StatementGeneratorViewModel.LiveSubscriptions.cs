// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.LiveSubscriptions.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.3 — StatementGeneratorViewModel partial split. This part owns the
// LiveSubscriptions members; fields + constructor live in StatementGeneratorViewModel.cs.
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
        //  LIVE STEP UPDATE SUBSCRIPTIONS
        // ═════════════════════════════════════════════════════════════════

        private void SubscribeToLiveStepUpdates(List<WorkflowStepState> steps)
        {
            foreach (var step in steps)
            {
                step.PropertyChanged += OnLiveStepPropertyChanged;

                var currentStep = step;
                NotifyCollectionChangedEventHandler handler =
                    (s, e) => OnLiveDetailRowsChanged(currentStep, e);

                step.DetailRows.CollectionChanged += handler;
                _liveDetailSubscriptions[step] = handler;
            }
        }

        private void UnsubscribeFromLiveStepUpdates(List<WorkflowStepState> steps)
        {
            foreach (var step in steps)
            {
                step.PropertyChanged -= OnLiveStepPropertyChanged;

                if (_liveDetailSubscriptions.TryGetValue(step, out var handler))
                    step.DetailRows.CollectionChanged -= handler;
            }
            _liveDetailSubscriptions.Clear();
        }

        private void OnLiveStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not WorkflowStepState step) return;
            if (e.PropertyName != nameof(WorkflowStepState.Status)) return;

            RecalculateLiveProgress();

            if (step.IsRunning)
            {
                LiveAddLogEntry(LogSeverity.Section,
                    $"═══ Step {(int)step.Step}: {step.Title} ═══");
            }
            else if (step.IsComplete && !string.IsNullOrWhiteSpace(step.CurrentMessage))
            {
                var severity = step.Status switch
                {
                    StepStatus.Success => LogSeverity.Success,
                    StepStatus.Warning => LogSeverity.Warning,
                    StepStatus.Blocked => LogSeverity.Error,
                    StepStatus.Skipped => LogSeverity.Info,
                    _ => LogSeverity.Info
                };
                LiveAddLogEntry(severity,
                    $"› {step.StatusIcon} {step.CurrentMessage}");
            }
        }

        private void OnLiveDetailRowsChanged(
            WorkflowStepState step,
            NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add) return;
            if (e.NewItems == null) return;

            foreach (var item in e.NewItems.OfType<string>())
            {
                var severity = StatementLogHelper.InferLogSeverity(item);
                LiveAddLogEntry(severity, "› " + item);

                if (severity == LogSeverity.Info)
                    InfoCount++;
            }
        }

        private void RecalculateLiveProgress()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                int totalSteps = LastWorkflowSteps.Count;
                if (totalSteps == 0)
                {
                    LastWorkflowProgressPercent = 0;
                    return;
                }

                int completedSteps = LastWorkflowSteps.Count(s => s.IsComplete);
                int runningSteps = LastWorkflowSteps.Count(s => s.IsRunning);

                double progress = (completedSteps * 100.0 + runningSteps * 50.0) / totalSteps;
                LastWorkflowProgressPercent = Math.Min(100, progress);
            });
        }

        private void LiveAddLogEntry(LogSeverity severity, string text)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                AppLogService.Debug($"[LOG-DEBUG] NO DISPATCHER — dropping: {text}");
                return;
            }

            dispatcher.Invoke(() =>
            {
                ProcessingLog.Add(new ProGlassAutomation.Models.Statement.Workflow.LogEntry
                {
                    Severity = severity,
                    Text = text
                });
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  POST-COMPLETION LIFT
        // ═════════════════════════════════════════════════════════════════

        private void LiftWorkflowResultAfterCompletion(
            WorkflowResult wf,
            string pdfPath,
            double elapsedSec)
        {
            if (wf == null) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                LastWorkflowStatusText = wf.Outcome switch
                {
                    WorkflowOutcome.Success => "Pipeline Complete",
                    WorkflowOutcome.Warning => "Completed with Warnings",
                    WorkflowOutcome.Blocked => "Pipeline Blocked",
                    WorkflowOutcome.Cancelled => "Pipeline Cancelled",
                    _ => "Ready"
                };

                IsLastWorkflowComplete =
                    wf.Outcome == WorkflowOutcome.Success ||
                    wf.Outcome == WorkflowOutcome.Warning;

                OrderedQtyMetric = wf.OrderedQty;
                DeliveredQtyMetric = wf.DeliveredQty;
                BalanceQtyMetric = wf.PendingProductionQty;

                DeliveryRatePercent = wf.OrderedQty > 0
                    ? Math.Round((double)wf.DeliveredQty / wf.OrderedQty * 100.0, 1)
                    : 0;

                // ── Deep-detail result data for the Review step ────────────
                LastSnapshot = wf.Snapshot;

                LastReconciliationSummaries.Clear();
                foreach (var s in wf.ReconciliationSummaries)
                    LastReconciliationSummaries.Add(s);

                // Split the flattened line items into "Included in statement"
                // (delivered and/or invoiced) vs "Due / Balance" (not yet
                // delivered AND not yet invoiced), so the Review step can show
                // exactly which lines are in the statement and which are pending.
                LastIncludedLines.Clear();
                LastDueLines.Clear();
                foreach (var summary in wf.ReconciliationSummaries)
                {
                    foreach (var row in summary.Rows)
                    {
                        if (row.DeliveredQty > 0 || row.InvoicedQty > 0)
                            LastIncludedLines.Add(row);
                        else
                            LastDueLines.Add(row);
                    }
                }
                OnPropertyChanged(nameof(LastIncludedLines));
                OnPropertyChanged(nameof(LastDueLines));

                // Group the flattened lines by specification for the
                // DataGrid-like view (spec header → rows → summary).
                IncludedGroups.Clear();
                foreach (var g in ReconciliationSpecGroup.Build(LastIncludedLines))
                    IncludedGroups.Add(g);
                DueGroups.Clear();
                foreach (var g in ReconciliationSpecGroup.Build(LastDueLines))
                    DueGroups.Add(g);
                OnPropertyChanged(nameof(IncludedGroups));
                OnPropertyChanged(nameof(DueGroups));
                OnPropertyChanged(nameof(IncludedTotalQty));
                OnPropertyChanged(nameof(IncludedTotalSqm));
                OnPropertyChanged(nameof(DueTotalQty));
                OnPropertyChanged(nameof(DueTotalSqm));

                OnPropertyChanged(nameof(LastHasReconciliationIssue));

                BuildWorkflowWarnings(wf);

                LastPdfPath = pdfPath;
                LastPdfGeneratedAt = DateTime.Now;
                LastPdfGenerationSec = elapsedSec;

                if (!string.IsNullOrWhiteSpace(pdfPath) && File.Exists(pdfPath))
                {
                    try
                    {
                        var fi = new FileInfo(pdfPath);
                        LastPdfSize = FormatFileSize(fi.Length);
                    }
                    catch { LastPdfSize = ""; }

                    try
                    {
                        using var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.InformationOnly);
                        LastPdfPageCount = doc.PageCount;
                    }
                    catch { LastPdfPageCount = 0; }
                }
                else
                {
                    LastPdfSize = "";
                    LastPdfPageCount = 0;
                }

                OnPropertyChanged(nameof(HasLastPdf));
                // [CHAT 22 v3] Notify navigation gates now that state is final
                NotifyAllNavigationStates();

                BuildPdfSections(wf);
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  BUILD WARNINGS FEED
        // ═════════════════════════════════════════════════════════════════

        private void BuildWorkflowWarnings(WorkflowResult wf)
        {
            WorkflowWarnings.Clear();
            int errCount = 0;
            int warnCount = 0;

            if (wf.Snapshot != null)
            {
                var overdueRows = wf.Snapshot.InvoiceRows
                    .Where(r => r.IsOverdue)
                    .OrderByDescending(r => r.DaysOverdue)
                    .ToList();

                foreach (var row in overdueRows)
                {
                    WorkflowWarnings.Add(new WorkflowWarningItem
                    {
                        Severity = LogSeverity.Error,
                        Icon = "\uE7BA",
                        Title = "Overdue Invoice",
                        Detail = $"{row.InvoiceNumber} is overdue by " +
                                 $"{row.DaysOverdue} days " +
                                 $"(due: {row.DueDate:MMM d, yyyy})",
                        AmountDisplay = $"AED {row.Balance:N2}",
                        ActionLabel = "→ Open invoice",
                        ActionKind = WarningActionKind.OpenInvoice,
                        TargetId = row.InvoiceNumber ?? "",
                        StepLabel = "Post-workflow · Overdue Check",
                        HasQuantities = false
                    });
                    errCount++;
                }
            }

            foreach (var udn in wf.UninvoicedDeliveryNotes)
            {
                WorkflowWarnings.Add(new WorkflowWarningItem
                {
                    Severity = LogSeverity.Warning,
                    Icon = "\uE7BA",
                    Title = "Uninvoiced Delivery",
                    Detail = $"{udn.DeliveryNoteId} delivered {udn.DeliveredQty} units " +
                             $"on {udn.DeliveryDate:MMM d, yyyy} — no invoice yet",
                    AmountDisplay = "",
                    ActionLabel = "→ Create invoice",
                    ActionKind = WarningActionKind.CreateInvoiceFromDn,
                    TargetId = udn.DeliveryNoteId ?? "",
                    StepLabel = "Step 6 · Coverage Check",
                    HasQuantities = true,
                    OrderedText = wf.OrderedQty.ToString("N0"),
                    DeliveredText = udn.DeliveredQty.ToString("N0"),
                    DifferenceText = $"-{Math.Max(0, wf.OrderedQty - udn.DeliveredQty):N0}"
                });
                warnCount++;
            }

            // ── Line-item reconciliation issues (over/under-invoiced) ──────
            // Each problematic line becomes an actionable alert that can jump
            // to the related Proforma Invoice, so the real "deep detail" is
            // one click away instead of buried in the terminal log.
            foreach (var summary in wf.ReconciliationSummaries)
            {
                foreach (var line in summary.Rows.Where(r =>
                    r.HasDiscrepancy))
                {
                    WorkflowWarnings.Add(new WorkflowWarningItem
                    {
                        Severity = line.Status == ReconciliationStatus.OverInvoiced
                            ? LogSeverity.Error
                            : LogSeverity.Warning,
                        Icon = "\uE7BA",
                        Title = line.Status == ReconciliationStatus.OverInvoiced
                            ? "Over-Invoiced Line"
                            : "Under-Invoiced Line",
                        Detail = $"{line.GlassRef} ({line.DimensionDisplay}) — " +
                                 $"ordered {line.OrderedQty}, delivered {line.DeliveredQty}, " +
                                 $"invoiced {line.InvoicedQty}",
                        AmountDisplay = $"Diff {line.InvoiceVsDeliveryDiff:+0;-#}",
                        ActionLabel = "→ Open PI",
                        ActionKind = WarningActionKind.OpenProforma,
                        TargetId = summary.ProformaId ?? "",
                        StepLabel = $"Step 6 · Reconciliation · {summary.ProformaId}",
                        HasQuantities = true,
                        OrderedText = line.OrderedQty.ToString("N0"),
                        DeliveredText = line.DeliveredQty.ToString("N0"),
                        DifferenceText = line.InvoiceVsDeliveryDiff.ToString("+0;-#")
                    });

                    if (line.Status == ReconciliationStatus.OverInvoiced) errCount++;
                    else warnCount++;
                }
            }

            foreach (var step in wf.Steps)
            {
                foreach (var w in step.Warnings)
                {
                    WorkflowWarnings.Add(new WorkflowWarningItem
                    {
                        Severity = LogSeverity.Warning,
                        Icon = "\uE7BA",
                        Title = "Workflow Warning",
                        Detail = w,
                        AmountDisplay = "",
                        ActionLabel = "→ Review",
                        ActionKind = WarningActionKind.GoToReview,
                        StepLabel = $"Step {(int)step.Step} · {step.Title}",
                        HasQuantities = false
                    });
                    warnCount++;
                }
            }

            // ── Informational entries — every included invoice + a summary,
            //    so the feed shows the FULL list of what ran, not a single
            //    collapsed line. ────────────────────────────────────────────
            int infoCount = 0;

            if (wf.Snapshot != null)
            {
                // One info card per included invoice (the "many infos" that
                // previously collapsed into a single summary).
                foreach (var row in wf.Snapshot.InvoiceRows)
                {
                    WorkflowWarnings.Add(new WorkflowWarningItem
                    {
                        Severity = LogSeverity.Info,
                        Icon = "ℹ",
                        Title = row.InvoiceNumber ?? "Invoice",
                        Detail = $"{row.InvoiceType} · {row.TotalQty} pcs · " +
                                 $"AED {row.GrandTotal:N2} · {row.Status}",
                        AmountDisplay = row.Balance > 0 ? $"AED {row.Balance:N2} outstanding" : "Paid",
                        ActionLabel = "→ Open invoice",
                        ActionKind = WarningActionKind.OpenInvoice,
                        TargetId = row.InvoiceNumber ?? "",
                        StepLabel = "Included in Statement",
                        HasQuantities = false
                    });
                    infoCount++;
                }

                WorkflowWarnings.Add(new WorkflowWarningItem
                {
                    Severity = LogSeverity.Info,
                    Icon = "ℹ",
                    Title = "Statement Generated",
                    Detail = $"{wf.Snapshot.Financial.TotalInvoiceCount} invoice(s) included · " +
                             $"AED {wf.Snapshot.Financial.TotalInvoiced:N2}",
                    AmountDisplay = "",
                    ActionLabel = "→ View PDF",
                    ActionKind = WarningActionKind.OpenPdf,
                    StepLabel = "Pipeline Complete",
                    HasQuantities = false
                });
                infoCount++;
            }

            if (errCount == 0 && warnCount == 0)
            {
                WorkflowWarnings.Add(new WorkflowWarningItem
                {
                    Severity = LogSeverity.Info,
                    Icon = "ℹ",
                    Title = "All Clear",
                    Detail = "No warnings or errors — every invoice was issued and matched to a real delivery.",
                    AmountDisplay = "",
                    ActionLabel = "",
                    StepLabel = "Pipeline Complete",
                    HasQuantities = false
                });
                infoCount++;
            }

            ErrorCount = errCount;
            WarningCount = warnCount;
            InfoCount = infoCount;
        }

    }
}
