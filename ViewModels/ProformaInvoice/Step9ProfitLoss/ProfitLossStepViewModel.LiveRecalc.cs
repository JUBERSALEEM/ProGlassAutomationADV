// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.LiveRecalc.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.LiveRecalc members.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguLamParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.LamParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.SguParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Text.RegularExpressions; // Match still used while production rows read spacer/pvb groups
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss
{
    public partial class ProfitLossStepViewModel : ViewModelBase
    {
        // ══════════════════════════════════════════════════════════════════
        //  LIVE RECALCULATION
        //
        //  Bind() used to store these references and subscribe to nothing, so
        //  the report only rebuilt when the step was entered or REFRESH was
        //  pressed. Edit a size in Step 5, come back, and the figures were
        //  from the previous visit — silently wrong rather than obviously
        //  empty, which is the worse failure for a money screen.
        //
        //  Everything the costing reads is now watched:
        //    · spec groups added or removed
        //    · item rows added or removed inside a group
        //    · width / height / quantity edits on any row
        //    · the Step 4 pricing panels (rates, yield, margin)
        //
        //  Recalculation is coalesced: a paste that changes fifty rows fires
        //  one rebuild, not fifty. Without that, bulk edits would re-run the
        //  whole Sheet Store lookup per keystroke.

        private bool _liveWired;
        private bool _recalcQueued;

        private void WireLiveRecalculation()
        {
            if (_liveWired) return;
            _liveWired = true;

            if (_dataGrid?.SpecGroups is INotifyCollectionChanged groups)
            {
                groups.CollectionChanged += (_, e) =>
                {
                    // Watch whatever just arrived, stop watching what left.
                    if (e.NewItems != null)
                        foreach (SpecGroup g in e.NewItems.OfType<SpecGroup>())
                            WatchSpecGroup(g);

                    if (e.OldItems != null)
                        foreach (SpecGroup g in e.OldItems.OfType<SpecGroup>())
                            UnwatchSpecGroup(g);

                    QueueRecalculate();
                };
            }

            foreach (var g in _dataGrid?.SpecGroups ?? Enumerable.Empty<SpecGroup>())
                WatchSpecGroup(g);

            // Step 4 panels: a rate, yield or margin change moves the cost
            // basis, so the report must follow it.
            WatchPanel(_dguVm);
            WatchPanel(_sguVm);
            WatchPanel(_lamVm);
            WatchPanel(_dguLamVm);
            WatchPanel(_otherChargesVm);
        }

        private void WatchPanel(INotifyPropertyChanged? panel)
        {
            if (panel == null) return;
            panel.PropertyChanged += (_, __) => QueueRecalculate();
        }

        private void WatchSpecGroup(SpecGroup group)
        {
            if (group == null) return;

            // Detach first. A group can reach this method twice — added,
            // then seen again in a collection Reset — and a doubled handler
            // would rebuild the report twice per edit. Removing a handler
            // that was never attached is a no-op, so this is always safe.
            group.PropertyChanged -= OnSpecGroupChanged;
            group.PropertyChanged += OnSpecGroupChanged;

            if (group.ItemRows is INotifyCollectionChanged rows)
            {
                rows.CollectionChanged -= OnItemRowsChanged;
                rows.CollectionChanged += OnItemRowsChanged;
            }

            foreach (var row in group.ItemRows ?? Enumerable.Empty<GlassItemRow>())
                WatchItemRow(row);
        }

        private void UnwatchSpecGroup(SpecGroup group)
        {
            if (group == null) return;

            group.PropertyChanged -= OnSpecGroupChanged;

            if (group.ItemRows is INotifyCollectionChanged rows)
                rows.CollectionChanged -= OnItemRowsChanged;

            foreach (var row in group.ItemRows ?? Enumerable.Empty<GlassItemRow>())
                row.PropertyChanged -= OnItemRowChanged;
        }

        private void WatchItemRow(GlassItemRow row)
        {
            if (row == null) return;

            // Unsubscribe first: a row can be handed to Watch twice (added,
            // then seen again in a Reset), and a doubled handler would fire
            // two rebuilds per keystroke.
            row.PropertyChanged -= OnItemRowChanged;
            row.PropertyChanged += OnItemRowChanged;
        }

        private void OnSpecGroupChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Only the fields the costing actually reads.
            if (e.PropertyName is nameof(SpecGroup.SelectedSpecName)
                               or nameof(SpecGroup.SelectedGlassType))
            {
                QueueRecalculate();
            }
        }

        private void OnItemRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (GlassItemRow r in e.NewItems.OfType<GlassItemRow>())
                    WatchItemRow(r);

            if (e.OldItems != null)
                foreach (GlassItemRow r in e.OldItems.OfType<GlassItemRow>())
                    r.PropertyChanged -= OnItemRowChanged;

            // A Reset carries no item lists — re-watch the whole collection.
            if (e.Action == NotifyCollectionChangedAction.Reset
                && sender is IEnumerable<GlassItemRow> all)
            {
                foreach (var r in all) WatchItemRow(r);
            }

            QueueRecalculate();
        }

        private void OnItemRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Dimensions and quantity are the only row fields that change the
            // cut area. Reacting to every property would rebuild the report on
            // cosmetic edits such as the reference text.
            if (e.PropertyName is nameof(GlassItemRow.W1) or nameof(GlassItemRow.H1)
                               or nameof(GlassItemRow.W2) or nameof(GlassItemRow.H2)
                               or nameof(GlassItemRow.Qty))
            {
                QueueRecalculate();
            }
        }

        /// <summary>
        /// Coalesces a burst of edits into a single rebuild on the dispatcher's
        /// next idle turn.
        /// </summary>
        private void QueueRecalculate()
        {
            if (_recalcQueued) return;
            _recalcQueued = true;

            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher == null)
            {
                // No UI thread (unit tests) — run it straight through.
                _recalcQueued = false;
                _ = RecalculateAsync();
                return;
            }

            dispatcher.BeginInvoke(
                new Action(async () =>
                {
                    _recalcQueued = false;

                    // RecalculateAsync early-returns while a run is already
                    // in flight. Without re-queueing, an edit made during
                    // that run would be silently dropped and the report would
                    // sit one change behind the data — the exact staleness
                    // this wiring exists to remove.
                    if (IsBusy)
                    {
                        QueueRecalculate();
                        return;
                    }

                    await RecalculateAsync().ConfigureAwait(true);
                }),
                DispatcherPriority.Background);
        }

        private async Task FireRecalculateAsync()
        {
            // async void is confined to this command entry point and the
            // body cannot throw (RecalculateAsync swallows everything).
            await RecalculateAsync().ConfigureAwait(true);
        }

        // ══════════════════════════════════════════════════════════════
        //  THE CALCULATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Rebuilds every row and total. Never throws: any failure leaves
        /// the previous figures in place and reports through StatusMessage.
        /// </summary>

    }
}
