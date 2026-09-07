// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.Preview.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.3 — StatementGeneratorViewModel partial split. This part owns the
// Preview members; fields + constructor live in StatementGeneratorViewModel.cs.
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
        //  PREVIEW
        // ═════════════════════════════════════════════════════════════════

        private async Task RunPreviewAsync()
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            IsLoading = true;
            StatusMessage = "Loading matching invoices…";
            PreviewSnapshot = null;

            ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() => PreviewVM.UpdateFromSnapshot(null));

            try
            {
                var filter = BuildFilter();
                var number = BuildStatementNumber(filter);

                if (token.IsCancellationRequested) return;

                var snapshot = await Task.Run(() => _aggregator.BuildAsync(filter, number), token);

                if (token.IsCancellationRequested) return;

                // v3 — the preview letterhead shows the same branding as the
                // final PDF.
                await EnrichCustomerBrandingAsync(snapshot);

                PreviewSnapshot = snapshot;

                ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() => PreviewVM.UpdateFromSnapshot(snapshot));

                if (snapshot == null)
                {
                    StatusMessage = "No invoices match the selected filters. Try adjusting your criteria.";
                }
                else
                {
                    int count = snapshot.InvoiceRows.Count;
                    StatusMessage =
                        $"Found {count} invoice{(count == 1 ? "" : "s")}  ·  " +
                        $"Total: AED {snapshot.Financial.TotalInvoiced:N2}  ·  " +
                        $"Outstanding: AED {snapshot.Financial.TotalOutstanding:N2}";
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"Preview failed: {ExceptionMessages.Deep(ex)}");
                StatusMessage = $"Preview failed: {ExceptionMessages.Deep(ex)}";
                PreviewSnapshot = null;
                ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() => PreviewVM.UpdateFromSnapshot(null));
            }
            finally
            {
                IsLoading = false;
            }
        }

    }
}
