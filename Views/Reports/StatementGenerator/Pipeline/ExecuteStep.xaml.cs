// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Reports/StatementGenerator/Pipeline/ExecuteStep.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Statement Generator — Wizard Step 3: EXECUTE (Chat 21 v3)
//
// PURPOSE:
//   Code-behind for the Execute step. Handles ONE thing:
//   auto-scroll the terminal log to bottom as new entries arrive.
//
//   This gives the user the classic "streaming terminal" experience
//   where the latest log line is always visible during pipeline run.
//
// LIFECYCLE:
//   Loaded    → attach to VM's ProcessingLog collection
//   Unloaded  → detach (prevent memory leak)
//   VM adds entry → CollectionChanged fires → ScrollToEnd()
//
// DATA CONTEXT:
//   Inherited from parent Shell (StatementGeneratorViewModel).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.ViewModels.Reports.StatementGenerator;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.Reports.StatementGenerator.Pipeline
{
    public partial class ExecuteStep : UserControl
    {
        private ObservableCollection<ProGlassAutomation.Models.Statement.Workflow.LogEntry>? _subscribedLog;

        public ExecuteStep()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        // ═════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═════════════════════════════════════════════════════════════════

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AttachToLog(DataContext as StatementGeneratorViewModel);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachFromLog();
        }

        private void OnDataContextChanged(
            object sender,
            DependencyPropertyChangedEventArgs e)
        {
            DetachFromLog();
            AttachToLog(e.NewValue as StatementGeneratorViewModel);
        }

        // ═════════════════════════════════════════════════════════════════
        //  SUBSCRIPTION MANAGEMENT
        // ═════════════════════════════════════════════════════════════════

        private void AttachToLog(StatementGeneratorViewModel? vm)
        {
            if (vm == null) return;
            if (ReferenceEquals(_subscribedLog, vm.ProcessingLog)) return;

            _subscribedLog = vm.ProcessingLog;
            _subscribedLog.CollectionChanged += OnProcessingLogChanged;
        }

        private void DetachFromLog()
        {
            if (_subscribedLog == null) return;
            _subscribedLog.CollectionChanged -= OnProcessingLogChanged;
            _subscribedLog = null;
        }

        // ═════════════════════════════════════════════════════════════════
        //  AUTO-SCROLL
        // ═════════════════════════════════════════════════════════════════

        private void OnProcessingLogChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add) return;

            // Defer to Background priority so the new item is rendered
            // before we scroll — otherwise we scroll to a stale extent.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    TerminalLogScroller?.ScrollToEnd();
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[ExecuteStep] ScrollToEnd failed: {ex.Message}");
                }
            }), DispatcherPriority.Background);
        }
    }
}