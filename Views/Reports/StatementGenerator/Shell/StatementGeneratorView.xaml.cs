// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Reports/StatementGenerator/Shell/StatementGeneratorView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Statement Generator — Wizard Shell (Chat 21 v3)
//
// PURPOSE:
//   Code-behind for the wizard shell. Kept minimal — the entire
//   wizard is data-driven via XAML bindings to CurrentStep VM prop.
//
// RESPONSIBILITIES (only two, both mandatory):
//
//   1. INITIALIZE
//      Standard partial class hookup via InitializeComponent().
//      No DataContext is set here — inherited from parent
//      SalesReportView via DataTemplate resolution.
//
//   2. AUTO-RUN PIPELINE ON STEP 3 ENTRY
//      Watches VM's PropertyChanged for CurrentStep transitions.
//      When step becomes 3 (Execute), automatically fires the
//      pipeline run. Entering the step IS the trigger — user
//      doesn't click a "Start" button. This matches the wizard
//      pattern (each step activates its work automatically).
//
// LIFECYCLE:
//   Loaded    → subscribe to VM PropertyChanged
//   Unloaded  → unsubscribe (prevent memory leak)
//   VM.CurrentStep changes to 3 → trigger VM.RunPipelineAsync()
//
// THREAD SAFETY:
//   All operations occur on UI thread (PropertyChanged fires on
//   whichever thread set the property; VM setters use Dispatcher).
//   RunPipelineAsync is async — fire-and-forget from step-change
//   handler is safe because the VM's IsGenerating flag prevents
//   double-fires (guarded internally in RunPipelineAsync).
//
// DESIGN NOTE:
//   We use code-behind (not a Behavior) for this small orchestration
//   because it's tightly coupled to this shell only. Extracting to
//   a reusable Behavior would be over-engineering for a one-time hook.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.Reports.StatementGenerator;
using ProGlassAutomation.Services.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.StatementGenerator.Shell
{
    public partial class StatementGeneratorView : UserControl
    {
        // ── Track subscribed VM for clean unsubscribe on unload ──────────
        private StatementGeneratorViewModel? _subscribedVm;

        public StatementGeneratorView()
        {
            InitializeComponent();

            // Subscribe/unsubscribe on load/unload — safer than constructor
            // because DataContext isn't guaranteed to be set yet at ctor time.
            Loaded += OnShellLoaded;
            Unloaded += OnShellUnloaded;

            // Also handle DataContext changes (happens when parent
            // SalesReportView swaps VMs on New/Edit statement navigation).
            DataContextChanged += OnDataContextChanged;
        }

        // ═════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═════════════════════════════════════════════════════════════════

        private void OnShellLoaded(object sender, RoutedEventArgs e)
        {
            AttachToVm(DataContext as StatementGeneratorViewModel);
        }

        private void OnShellUnloaded(object sender, RoutedEventArgs e)
        {
            DetachFromVm();
        }

        private void OnDataContextChanged(
            object sender,
            DependencyPropertyChangedEventArgs e)
        {
            // DataContext swap — detach old, attach new
            DetachFromVm();
            AttachToVm(e.NewValue as StatementGeneratorViewModel);
        }

        // ═════════════════════════════════════════════════════════════════
        //  VM SUBSCRIPTION MANAGEMENT
        // ═════════════════════════════════════════════════════════════════

        private void AttachToVm(StatementGeneratorViewModel? vm)
        {
            if (vm == null) return;
            if (ReferenceEquals(_subscribedVm, vm)) return;   // already attached

            _subscribedVm = vm;
            _subscribedVm.PropertyChanged += OnVmPropertyChanged;
        }

        private void DetachFromVm()
        {
            if (_subscribedVm == null) return;

            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;
            _subscribedVm = null;
        }

        // ═════════════════════════════════════════════════════════════════
        //  AUTO-RUN PIPELINE ON STEP 3 ENTRY
        //
        //  When user navigates to Step 3 (Execute), automatically
        //  triggers the pipeline run. This is the wizard pattern:
        //  entering a step activates its work.
        //
        //  Guards:
        //    - Only fires when CurrentStep property changes
        //    - Only fires when new value is 3
        //    - VM's RunPipelineAsync internally guards against
        //      double-execution via IsGenerating flag
        // ═════════════════════════════════════════════════════════════════

        private async void OnVmPropertyChanged(
            object? sender,
            PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(StatementGeneratorViewModel.CurrentStep))
                return;

            if (_subscribedVm == null) return;

            // Auto-run pipeline when Step 3 (Execute) becomes active
            if (_subscribedVm.CurrentStep == 3)
            {
                try
                {
                    AppLogService.Debug("[Shell] Step 3 entered — auto-running pipeline");
                    await _subscribedVm.RunPipelineAsync();
                }
                catch (System.Exception ex)
                {
                    AppLogService.Debug($"[Shell] Pipeline auto-run failed: {ex.Message}");
                }
            }
        }
    }
}