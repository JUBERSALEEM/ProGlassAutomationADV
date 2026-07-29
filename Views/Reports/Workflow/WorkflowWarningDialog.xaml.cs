// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Reports/Workflow/WorkflowWarningDialog.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Workflow Engine (Chat 17)
//
// PURPOSE:
//   Modal dialog shown when workflow detects uninvoiced deliveries.
//   Shows the missing DN list and asks user: Create / Skip / Abort.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Statement.Workflow;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Reports.Workflow
{
    public partial class WorkflowWarningDialog : Window
    {
        /// <summary>User's decision — read by caller after dialog closes.</summary>
        public UserWarningDecision UserDecision { get; private set; }
            = UserWarningDecision.SkipAndContinue;

        public WorkflowWarningDialog(WorkflowResult partialResult)
        {
            InitializeComponent();
            DataContext = partialResult;

            MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            };
        }

        private void CreateInvoice_Click(object sender, RoutedEventArgs e)
        {
            UserDecision = UserWarningDecision.CreateInvoice;
            DialogResult = true;
            Close();
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            UserDecision = UserWarningDecision.SkipAndContinue;
            DialogResult = true;
            Close();
        }

        private void Abort_Click(object sender, RoutedEventArgs e)
        {
            UserDecision = UserWarningDecision.Abort;
            DialogResult = false;
            Close();
        }
    }
}