using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice.Export;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges.CustomCharges;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// Main ViewModel for Step 6 (Other Charges).
    ///
    /// CHAT 17 — Per-Spec Targeting:
    ///   Each OtherChargeRow now owns a SpecTargetSelection (multi-select
    ///   specs + metric). BuildSourcePool() has been replaced with
    ///   BuildTargetSelectionForRow() which creates one selector per row.
    ///
    ///   When PerSpecMetrics changes (spec added/removed/renamed) each row's
    ///   SpecTargetSelection is refreshed by SyncTargetSelectionsFromHub().
    ///
    /// BUG-53, BUG-54, BUG-55 all preserved.
    /// </summary>
    public class OtherChargesViewModel : ViewModelBase
    {
        private readonly DataGridViewModel _dataGridVM;
        private readonly DispatcherTimer _polishDebouncer;
        private const int POLISH_DEBOUNCE_MS = 150;

        public string Title { get; } = "OTHER CHARGES";
        public string Subtitle { get; } =
            "Supplementary, fabrication and delivery charges — " +
            "select which specs to charge and which metric to use.";

        public MetricDiscoveryHubViewModel MetricHub { get; }

        public ObservableCollection<OtherChargeRow> ChargeRows { get; } = new();

        public WastagesSectionViewModel WastagesSection { get; }
        public CustomChargesSectionViewModel CustomChargesSection { get; }

        // ── Total ──────────────────────────────────────────────────────────

        private decimal _totalOtherCharges;
        public decimal TotalOtherCharges
        {
            get => _totalOtherCharges;
            private set
            {
                if (_totalOtherCharges == value) return;
                _totalOtherCharges = value;
                OnPropertyChanged();
            }
        }

        // ── Constructor ────────────────────────────────────────────────────

        public OtherChargesViewModel(DataGridViewModel dataGridVM)
        {
            _dataGridVM = dataGridVM
                ?? throw new ArgumentNullException(nameof(dataGridVM));

            _polishDebouncer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(POLISH_DEBOUNCE_MS)
            };
            _polishDebouncer.Tick += OnPolishDebounceElapsed;

            MetricHub = new MetricDiscoveryHubViewModel(_dataGridVM.SpecGroups);
            MetricHub.PropertyChanged += OnHubPropertyChanged;
            MetricHub.PerSpecMetrics.CollectionChanged += OnPerSpecMetricsChanged;

            BuildChargeRows();

            WastagesSection = new WastagesSectionViewModel(_dataGridVM);
            WastagesSection.PropertyChanged += OnSubSectionChanged;

            CustomChargesSection = new CustomChargesSectionViewModel();
            CustomChargesSection.PropertyChanged += OnSubSectionChanged;

            _dataGridVM.SpecGroups.CollectionChanged += OnSpecGroupsCollectionChanged;
            foreach (var g in _dataGridVM.SpecGroups)
                HookSpecGroup(g);

            RecalculatePolishRowsImmediate();
        }

        // ══════════════════════════════════════════════════════════════════
        //  SPEC TARGET SELECTION BUILDING
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a new SpecTargetSelection for the given row.
        /// All specs default to checked; metric defaults from template.
        /// </summary>
        private SpecTargetSelection BuildTargetSelectionForRow(OtherChargeRow row)
        {
            var selection = new SpecTargetSelection(row.Unit);

            // Populate specs (all checked by default — Decision 3)
            foreach (var specMetric in MetricHub.PerSpecMetrics)
            {
                selection.AvailableSpecs.Add(new SpecCheckOption
                {
                    SpecGroupNumber = specMetric.SpecGroupNumber,
                    DisplayName = specMetric.SpecDisplayName,
                    IsDoubleGlass = specMetric.IsDoubleGlass,
                    Lm1 = specMetric.Lm1,
                    Lm2 = specMetric.Lm2,
                    Sqm1 = specMetric.Sqm1,
                    Sqm2 = specMetric.Sqm2,
                    Qty = specMetric.Qty,
                    IsChecked = true
                });
            }

            // Default metric from template DefaultSourceKey (Decision 3)
            var template = PredefinedChargeTemplates.GetAll()
                .FirstOrDefault(t => t.Key == row.TemplateKey);

            if (template != null && !string.IsNullOrEmpty(template.DefaultSourceKey))
            {
                selection.SelectedMetric = selection.AvailableMetrics
                    .FirstOrDefault(m => m.Key == template.DefaultSourceKey)
                    ?? selection.AvailableMetrics.FirstOrDefault();
            }
            else
            {
                selection.SelectedMetric = selection.AvailableMetrics.FirstOrDefault();
            }

            return selection;
        }

        /// <summary>
        /// Refreshes all rows' TargetSelection with latest PerSpecMetrics.
        /// Preserves existing check states and metric selection when possible.
        /// Adds new specs (checked) or removes deleted ones.
        /// </summary>
        private void SyncTargetSelectionsFromHub()
        {
            foreach (var row in ChargeRows)
            {
                if (row.IsDeliveryRow) continue;

                var selection = row.TargetSelection;
                if (selection == null)
                {
                    row.TargetSelection = BuildTargetSelectionForRow(row);
                    continue;
                }

                // Sync spec list — add new specs, remove missing ones
                var currentKeys = MetricHub.PerSpecMetrics
                    .Select(s => s.SpecGroupNumber).ToHashSet();

                // Remove specs no longer present
                for (int i = selection.AvailableSpecs.Count - 1; i >= 0; i--)
                {
                    if (!currentKeys.Contains(selection.AvailableSpecs[i].SpecGroupNumber))
                        selection.AvailableSpecs.RemoveAt(i);
                }

                // Update existing + add new
                foreach (var specMetric in MetricHub.PerSpecMetrics)
                {
                    var existing = selection.AvailableSpecs
                        .FirstOrDefault(s => s.SpecGroupNumber == specMetric.SpecGroupNumber);

                    if (existing != null)
                    {
                        // Update metric values (preserves IsChecked)
                        existing.DisplayName = specMetric.SpecDisplayName;
                        existing.IsDoubleGlass = specMetric.IsDoubleGlass;
                        existing.Lm1 = specMetric.Lm1;
                        existing.Lm2 = specMetric.Lm2;
                        existing.Sqm1 = specMetric.Sqm1;
                        existing.Sqm2 = specMetric.Sqm2;
                        existing.Qty = specMetric.Qty;
                    }
                    else
                    {
                        // New spec — default to checked
                        selection.AvailableSpecs.Add(new SpecCheckOption
                        {
                            SpecGroupNumber = specMetric.SpecGroupNumber,
                            DisplayName = specMetric.SpecDisplayName,
                            IsDoubleGlass = specMetric.IsDoubleGlass,
                            Lm1 = specMetric.Lm1,
                            Lm2 = specMetric.Lm2,
                            Sqm1 = specMetric.Sqm1,
                            Sqm2 = specMetric.Sqm2,
                            Qty = specMetric.Qty,
                            IsChecked = true
                        });
                    }
                }

                // Notify computed value / summary changed
                selection.RaiseComputed();
                row.ForceRefresh();
            }

            RecalculateTotal();
        }

        // ── Hub property changed → sync ─────────────────────────────────

        private void OnHubPropertyChanged(
            object? sender, PropertyChangedEventArgs e)
        {
            SyncTargetSelectionsFromHub();
            WastagesSection?.SyncAllWastageSources();
        }

        private void OnPerSpecMetricsChanged(
            object? sender, NotifyCollectionChangedEventArgs e)
        {
            SyncTargetSelectionsFromHub();
        }

        // ── Charge rows ────────────────────────────────────────────────────

        private void BuildChargeRows()
        {
            ChargeRows.Clear();

            foreach (var template in PredefinedChargeTemplates.GetAll())
            {
                var row = new OtherChargeRow
                {
                    TemplateKey = template.Key,
                    ChargeName = template.ChargeName,
                    Description = template.Description,
                    Unit = template.Unit,
                    Rate = template.DefaultRate,
                    IsActive = false
                };

                if (row.IsPolishRow)
                {
                    foreach (var info in PolishSubTypeInfo.All)
                        row.AvailablePolishSubTypes.Add(info);

                    row.SelectedPolishSubType =
                        row.AvailablePolishSubTypes[0];
                }

                // Chat 17: Every non-delivery row gets a TargetSelection
                if (!row.IsDeliveryRow)
                {
                    row.TargetSelection = BuildTargetSelectionForRow(row);
                }

                row.PropertyChanged += OnChargeRowChanged;
                ChargeRows.Add(row);
            }
        }

        private void OnChargeRowChanged(
            object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(OtherChargeRow.ResultAmount)
                or nameof(OtherChargeRow.IsActive))
            {
                RecalculateTotal();
            }

            if (sender is OtherChargeRow row && row.IsPolishRow
                && e.PropertyName is
                    nameof(OtherChargeRow.SelectedPolishSubType)
                    or nameof(OtherChargeRow.IsActive))
            {
                RequestPolishRecalculation();
            }
        }

        // ── Sub-section change tracking ────────────────────────────────────

        private void OnSubSectionChanged(
            object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName ==
                    nameof(WastagesSectionViewModel.TotalWastageValue)
                || e.PropertyName ==
                    nameof(CustomChargesSectionViewModel.TotalCustomChargesValue))
            {
                RecalculateTotal();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  POLISH LM CALCULATION (BUG-53 + BUG-54)
        // ══════════════════════════════════════════════════════════════════

        private void RequestPolishRecalculation()
        {
            _polishDebouncer.Stop();
            _polishDebouncer.Start();
        }

        private void OnPolishDebounceElapsed(object? sender, EventArgs e)
        {
            _polishDebouncer.Stop();
            RecalculatePolishRowsImmediate();
        }

        private void RecalculatePolishRowsImmediate()
        {
            var specSnapshots = BuildSpecSnapshotsForCalculator();

            foreach (var row in ChargeRows.Where(r => r.IsPolishRow))
            {
                if (row.SelectedPolishSubType == null
                    || row.SelectedPolishSubType.Type == PolishSubType.Custom)
                {
                    continue;
                }

                decimal totalLm =
                    ProformaChargeMetricsCalculator.ComputePolishLm(
                        specSnapshots,
                        row.SelectedPolishSubType.Type.ToString());

                row.SetCalculatedPolishQty(totalLm);
            }
        }

        private List<SpecGroupSnapshot> BuildSpecSnapshotsForCalculator()
        {
            var result = new List<SpecGroupSnapshot>(
                _dataGridVM.SpecGroups.Count);

            foreach (var g in _dataGridVM.SpecGroups)
            {
                var snap = new SpecGroupSnapshot
                {
                    GroupNumber = g.GroupNumber,
                    GroupName = g.GroupName ?? "",
                    SelectedGlassType = g.SelectedGlassType ?? "",
                    SelectedSpecName = g.SelectedSpecName ?? ""
                };

                foreach (var r in g.ItemRows)
                {
                    snap.ItemRows.Add(new GlassItemRowSnapshot
                    {
                        SerialNumber = r.SerialNumber,
                        GlassRef = r.GlassRef ?? "",
                        W1 = r.W1,
                        H1 = r.H1,
                        W2 = r.W2,
                        H2 = r.H2,
                        Qty = r.Qty
                    });
                }

                result.Add(snap);
            }

            return result;
        }

        // ── Spec group change tracking ─────────────────────────────────────

        private void OnSpecGroupsCollectionChanged(
            object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (SpecGroup g in e.OldItems)
                    UnhookSpecGroup(g);

            if (e.NewItems != null)
                foreach (SpecGroup g in e.NewItems)
                    HookSpecGroup(g);

            RequestPolishRecalculation();
            WastagesSection?.SyncAllWastageSources();
        }

        private void HookSpecGroup(SpecGroup group)
        {
            group.PropertyChanged += OnSpecGroupPropertyChanged;
            group.ItemRows.CollectionChanged += OnItemRowsCollectionChanged;
            foreach (var item in group.ItemRows)
                item.PropertyChanged += OnItemRowPropertyChanged;
        }

        private void UnhookSpecGroup(SpecGroup group)
        {
            group.PropertyChanged -= OnSpecGroupPropertyChanged;
            group.ItemRows.CollectionChanged -= OnItemRowsCollectionChanged;
            foreach (var item in group.ItemRows)
                item.PropertyChanged -= OnItemRowPropertyChanged;
        }

        private void OnSpecGroupPropertyChanged(
            object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SpecGroup.SelectedGlassType))
            {
                RequestPolishRecalculation();
                WastagesSection?.SyncAllWastageSources();
            }

            if (e.PropertyName is nameof(SpecGroup.FooterTotalSqm)
                or nameof(SpecGroup.FooterTotalSqm2)
                or nameof(SpecGroup.GroupName))
            {
                WastagesSection?.SyncAllWastageSources();
            }
        }

        private void OnItemRowsCollectionChanged(
            object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (GlassItemRow r in e.OldItems)
                    r.PropertyChanged -= OnItemRowPropertyChanged;

            if (e.NewItems != null)
                foreach (GlassItemRow r in e.NewItems)
                    r.PropertyChanged += OnItemRowPropertyChanged;

            RequestPolishRecalculation();
        }

        private void OnItemRowPropertyChanged(
            object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(GlassItemRow.W1)
                or nameof(GlassItemRow.H1)
                or nameof(GlassItemRow.W2)
                or nameof(GlassItemRow.H2)
                or nameof(GlassItemRow.Qty))
            {
                RequestPolishRecalculation();
            }
        }

        // ── Total ──────────────────────────────────────────────────────────

        private void RecalculateTotal()
        {
            decimal predefinedSum = 0m;
            foreach (var row in ChargeRows)
                if (row.IsActive)
                    predefinedSum += row.ResultAmount;

            decimal wastageSum = WastagesSection?.TotalWastageValue ?? 0m;
            decimal customSum = CustomChargesSection?.TotalCustomChargesValue ?? 0m;

            TotalOtherCharges = Math.Round(
                predefinedSum + wastageSum + customSum, 2);
        }
    }
}