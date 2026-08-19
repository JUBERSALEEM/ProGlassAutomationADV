using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice.Export;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguLamParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.LamParameters;
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
        private LamParametersViewModel? _lamVm;
        private DguLamParametersViewModel? _dguLamVm;
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

        public void BindLaminationEditors(
            LamParametersViewModel lamVm,
            DguLamParametersViewModel dguLamVm)
        {
            if (_lamVm != null)
                _lamVm.PropertyChanged -= OnLaminationEditorChanged;
            if (_dguLamVm != null)
                _dguLamVm.PropertyChanged -= OnLaminationEditorChanged;

            _lamVm = lamVm;
            _dguLamVm = dguLamVm;

            if (_lamVm != null)
                _lamVm.PropertyChanged += OnLaminationEditorChanged;
            if (_dguLamVm != null)
                _dguLamVm.PropertyChanged += OnLaminationEditorChanged;

            SyncLaminationColorsFromStep4();
        }

        private void OnLaminationEditorChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is
                nameof(LamParametersViewModel.OuterGlassColor)
                or nameof(LamParametersViewModel.InnerGlassColor)
                or nameof(LamParametersViewModel.GeneratedSpecName)
                or nameof(DguLamParametersViewModel.LamOuterColor)
                or nameof(DguLamParametersViewModel.LamInnerColor)
                or nameof(DguLamParametersViewModel.GeneratedSpecName)
                or nameof(DguLamParametersViewModel.OuterGlassColor)
                or null
                or "")
            {
                SyncLaminationColorsFromStep4();
            }
        }

        private void SyncLaminationColorsFromStep4()
        {
            int lamCount = _dataGridVM.SpecGroups.Count(g =>
                string.Equals(g.SelectedGlassType, "LAM", StringComparison.OrdinalIgnoreCase));
            int dguLamCount = _dataGridVM.SpecGroups.Count(g =>
                string.Equals(g.SelectedGlassType, "DGU+LAM", StringComparison.OrdinalIgnoreCase));

            foreach (var group in _dataGridVM.SpecGroups)
            {
                if (string.Equals(group.SelectedGlassType, "LAM", StringComparison.OrdinalIgnoreCase)
                    && _lamVm != null)
                {
                    bool takeLive = lamCount == 1
                        || LaminateGlassMatcher.SpecNameMatchesGenerated(
                            group.SelectedSpecName, _lamVm.GeneratedSpecName);
                    if (takeLive)
                        group.ApplyLaminateColors(_lamVm.OuterGlassColor, _lamVm.InnerGlassColor);
                }
                else if (string.Equals(group.SelectedGlassType, "DGU+LAM", StringComparison.OrdinalIgnoreCase)
                    && _dguLamVm != null)
                {
                    bool takeLive = dguLamCount == 1
                        || LaminateGlassMatcher.SpecNameMatchesGenerated(
                            group.SelectedSpecName, _dguLamVm.GeneratedSpecName);
                    if (takeLive)
                        group.ApplyLaminateColors(_dguLamVm.LamOuterColor, _dguLamVm.LamInnerColor);
                }
            }

            MetricHub.Refresh();
            RequestPolishRecalculation();
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

            // Default metric from template DefaultSourceKey (Decision 3).
            // TOTAL_PIECES is a legacy alias for QTY — NormalizeKey maps it.
            var template = PredefinedChargeTemplates.GetAll()
                .FirstOrDefault(t => t.Key == row.TemplateKey);

            string defaultKey = template?.DefaultSourceKey ?? "";
            string normalized = MetricOption.NormalizeKey(defaultKey);
            selection.SelectedMetric = selection.AvailableMetrics
                .FirstOrDefault(m => m.Key == normalized)
                ?? selection.AvailableMetrics.FirstOrDefault();

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
                    or nameof(OtherChargeRow.IsActive)
                    or nameof(OtherChargeRow.TargetSelection))
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

                var scoped = FilterSnapshotsForRow(specSnapshots, row);
                decimal totalLm =
                    ProformaChargeMetricsCalculator.ComputePolishLm(
                        scoped,
                        row.SelectedPolishSubType.Type.ToString(),
                        matchedSameGlassOnly: row.IsMatchedLamPolishRow);

                row.SetCalculatedPolishQty(totalLm);
            }
        }

        private static List<SpecGroupSnapshot> FilterSnapshotsForRow(
            List<SpecGroupSnapshot> all, OtherChargeRow row)
        {
            if (row.TargetSelection == null)
                return all;

            var checkedNumbers = row.TargetSelection.AvailableSpecs
                .Where(s => s.IsChecked)
                .Select(s => s.SpecGroupNumber)
                .ToHashSet();

            if (checkedNumbers.Count == 0)
                return new List<SpecGroupSnapshot>();

            return all.Where(s => checkedNumbers.Contains(s.GroupNumber)).ToList();
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
                    SelectedSpecName = g.SelectedSpecName ?? "",
                    OuterGlassColor = g.OuterGlassColor ?? "",
                    InnerGlassColor = g.InnerGlassColor ?? ""
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
            if (e.PropertyName is nameof(SpecGroup.SelectedGlassType)
                or nameof(SpecGroup.SelectedSpecName)
                or nameof(SpecGroup.OuterGlassColor)
                or nameof(SpecGroup.InnerGlassColor))
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

        /// <summary>
        /// Re-syncs live Step 5 metrics into every charge row. Called when
        /// the user enters Step 6 so QTY / RESULT match the current grid.
        /// </summary>
        public void RefreshWorkspace()
        {
            SyncLaminationColorsFromStep4();
            MetricHub.Refresh();
            SyncTargetSelectionsFromHub();
            RecalculatePolishRowsImmediate();
            WastagesSection?.SyncAllWastageSources();
            RecalculateTotal();
        }

        /// <summary>
        /// New invoice: predefined rows back to defaults, wastage and
        /// custom lists emptied. Previous invoice selections must not leak.
        /// </summary>
        public void ResetForNewInvoice()
        {
            foreach (var row in ChargeRows)
                row.PropertyChanged -= OnChargeRowChanged;

            BuildChargeRows();

            WastagesSection.WastageRows.Clear();
            WastagesSection.RefreshTotal();

            CustomChargesSection.AddedCharges.Clear();
            CustomChargesSection.RefreshTotal();

            RecalculatePolishRowsImmediate();
            RecalculateTotal();
        }
    }
}