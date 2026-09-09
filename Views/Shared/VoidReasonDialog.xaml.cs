using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.Views.Shared
{
    /// <summary>
    /// Modal dialog that captures a mandatory reason before voiding (soft-
    /// closing) a posted document. ERP lifecycle hardening (Phase 1).
    ///
    /// Usage (via IDialogService):
    ///   var dlg = _dialogs.Create&lt;VoidReasonDialog&gt;("PI-2026-001");
    ///   if (dlg.ShowDialog() == true)
    ///       var reason = dlg.ResultReason;
    /// </summary>
    public partial class VoidReasonDialog : Window
    {
        private readonly VoidReasonViewModel _vm;

        /// <summary>The trimmed reason; valid only when ShowDialog() == true.</summary>
        public string ResultReason => _vm.ResultReason;

        public VoidReasonDialog(string documentLabel)
        {
            InitializeComponent();

            _vm = new VoidReasonViewModel(documentLabel);
            _vm.RequestClose = result =>
            {
                DialogResult = result;
                Close();
            };
            DataContext = _vm;

            ReasonTextBox.Loaded += (_, __) => ReasonTextBox.Focus();
        }

        private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
