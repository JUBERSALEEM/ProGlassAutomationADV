// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/Workflow/WorkflowWarningViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Workflow Engine (Chat 17).
//
// 2026-09-02 (Phase 3.7): the warning dialog's three decision handlers
// moved out of Views/Reports/Workflow/WorkflowWarningDialog.xaml.cs into
// this VM. The dialog's DataContext was previously the WorkflowResult
// itself, so this VM wraps it and re-exposes the four top-level binding
// paths the XAML uses; item-level bindings inside the ItemsControl are
// unaffected.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.Models.Statement.Workflow;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.Workflow
{
    /// <summary>Modal-dialog VM: "uninvoiced deliveries detected" —
    /// Create invoice / Skip / Abort decision.</summary>
    public sealed class WorkflowWarningViewModel : ViewModelBase
    {
        public WorkflowResult Result { get; }

        /// <summary>User's decision — read by caller after the dialog
        /// closes.</summary>
        public UserWarningDecision UserDecision { get; private set; }
            = UserWarningDecision.SkipAndContinue;

        /// <summary>View wiring: result=true → DialogResult=true (+Close).</summary>
        public Action<bool>? RequestClose { get; set; }

        public ICommand CreateInvoiceCommand { get; }
        public ICommand SkipCommand { get; }
        public ICommand AbortCommand { get; }

        public WorkflowWarningViewModel(WorkflowResult partialResult)
        {
            Result = partialResult;
            CreateInvoiceCommand = new RelayCommand(_ => Decide(
                UserWarningDecision.CreateInvoice, true));
            SkipCommand = new RelayCommand(_ => Decide(
                UserWarningDecision.SkipAndContinue, true));
            AbortCommand = new RelayCommand(_ => Decide(
                UserWarningDecision.Abort, false));
        }

        // ── Passthroughs for the dialog's top-level bindings ───────────
        public int DeliveredQty => Result.DeliveredQty;
        public int InvoicedQty => Result.InvoicedQty;
        public int UninvoicedDeliveredQty => Result.UninvoicedDeliveredQty;
        public List<UninvoicedDeliveryNote> UninvoicedDeliveryNotes =>
            Result.UninvoicedDeliveryNotes;

        private void Decide(UserWarningDecision decision, bool dialogResult)
        {
            UserDecision = decision;
            RequestClose?.Invoke(dialogResult);
        }
    }
}
