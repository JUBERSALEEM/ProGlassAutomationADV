// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Reports/Workflow/WorkflowWarningDialog.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Workflow Engine (Chat 17)
//
// PURPOSE:
//   Modal dialog shown when workflow detects uninvoiced deliveries.
//   Shows the missing DN list and asks user: Create / Skip / Abort.
//
// 2026-09-02 (Phase 3.7): decisions moved to WorkflowWarningViewModel
// (which wraps the WorkflowResult so the dialog's top-level bindings are
// unchanged); this file keeps the frameless drag + RequestClose wiring.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.ViewModels.Reports.Workflow;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Reports.Workflow
{
    public partial class WorkflowWarningDialog : Window
    {
        private readonly WorkflowWarningViewModel _vm;

        /// <summary>User's decision — read by caller after dialog closes.</summary>
        public UserWarningDecision UserDecision => _vm.UserDecision;

        public WorkflowWarningDialog(WorkflowResult partialResult)
        {
            InitializeComponent();

            _vm = new WorkflowWarningViewModel(partialResult);
            _vm.RequestClose = result =>
            {
                DialogResult = result;
                Close();
            };
            DataContext = _vm;

            MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            };
        }
    }
}
