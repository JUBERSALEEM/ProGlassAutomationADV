// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Sharing/ShareComposerWindow.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Sharing Center — share composer host (Stage 2)
//
// Thin host (InvoiceApprovalDialog pattern): builds the VM from the
// ShareContext, wires RequestClose to DialogResult + Close, and kicks off
// the async init on load. All behavior lives in ShareComposerViewModel.
//
// Opened via IDialogService so ViewModels never touch windows:
//   App.Dialogs.ShowDialog<ShareComposerWindow>(context)
// Returns true when at least one share succeeded, false/null otherwise.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.Sharing;
using System;
using System.Windows;

namespace ProGlassAutomation.Views.Sharing
{
    public partial class ShareComposerWindow : Window
    {
        private readonly ShareComposerViewModel _vm;

        /// <summary>True when at least one share succeeded.</summary>
        public bool Shared => _vm.HasSharedSuccessfully;

        public ShareComposerWindow(Services.Sharing.ShareContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            InitializeComponent();

            _vm = new ShareComposerViewModel(context);
            _vm.RequestClose = result =>
            {
                try { DialogResult = result; }
                catch (InvalidOperationException ex)
                {
                    // Already closing/closed — Close() below still runs.
                    ProGlassAutomation.Services.Logging.AppLogService.Debug(
                        "[Sharing.Composer] dialog result skipped: " + ex.Message);
                }
                Close();
            };
            DataContext = _vm;

            Loaded += (_, __) => AsyncUtility.SafeFireAndForget(
                () => _vm.InitializeAsync(),
                "Share composer initialization failed");
        }
    }
}
