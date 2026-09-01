// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Reports/Workflow/WorkflowProgressDialog.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Workflow Engine (Chat 17)
//
// PURPOSE:
//   Code-behind for the workflow progress modal dialog.
//   Starts the workflow engine on the Loaded event and closes
//   automatically when the ViewModel signals RequestClose.
//
// USAGE:
//   var dialog = new WorkflowProgressDialog(filter, "STMT-2026-001", "Alpha LLC");
//   dialog.Owner = mainWindow;
//   dialog.ShowDialog();
//   var result = dialog.Result;  // WorkflowResult ready for PDF export
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.ViewModels.Reports.Workflow;
using ProGlassAutomation.Views.Reports.Workflow;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Reports.Workflow
{
    public partial class WorkflowProgressDialog : Window
    {
        private readonly WorkflowProgressViewModel _vm;

        /// <summary>
        /// The final result of the workflow.
        /// Populated after the dialog closes.
        /// Null if the user closed before workflow completed.
        /// </summary>
        public WorkflowResult? Result { get; private set; }

        public WorkflowProgressDialog(
            StatementFilter filter,
            string statementNumber,
            string customerName)
        {
            InitializeComponent();

            _vm = new WorkflowProgressViewModel(
                App.Dialogs, filter, statementNumber, customerName);

            // Wire callback: when engine detects missing invoices,
            // show the warning resolution dialog
            _vm.ResolveMissingInvoices = ShowMissingInvoicesDialogAsync;

            // Wire callback: when user clicks Close, capture result
            _vm.OnDialogClosing = r => Result = r;

            // Wire callback: when VM requests close, close the window
            _vm.RequestClose = () =>
            {
                Dispatcher.Invoke(() =>
                {
                    DialogResult = true;
                    Close();
                });
            };

            DataContext = _vm;

            // Allow dragging the window by clicking anywhere
            MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            };
        }

        /// <summary>
        /// Fires when the window loads.
        /// Starts the workflow engine asynchronously.
        /// </summary>
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Small delay to let the dialog render before starting work
            await Task.Delay(200);
            await _vm.RunAsync();
        }

        /// <summary>
        /// Shows the warning resolution dialog when engine detects
        /// uninvoiced delivery notes.
        /// Returns user's decision: Create / Skip / Abort.
        /// </summary>
        private async Task<UserWarningDecision> ShowMissingInvoicesDialogAsync(
            WorkflowResult partialResult)
        {
            var tcs = new TaskCompletionSource<UserWarningDecision>();

            await Dispatcher.InvokeAsync(() =>
            {
                var dialog = new WorkflowWarningDialog(partialResult)
                {
                    Owner = this
                };

                bool? dialogResult = dialog.ShowDialog();

                tcs.SetResult(dialog.UserDecision);
            });

            return await tcs.Task;
        }
    }
}