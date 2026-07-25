using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice.Export;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges.CustomCharges;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// Main ViewModel for Step 6 (Other Charges).
    /// Owns predefined charge rows, wires them to live DataGrid metrics,
    /// computes Polish LM per sub-type, manages wastage + custom charge
    /// sections, and exposes running Total Other Charges.
    ///
    /// BUG-53 [HIGH]: RecalculatePolishRows debounced via DispatcherTimer
    ///                (150 ms). Was firing THOUSANDS of times during Excel
    ///                paste of 500 rows — now fires once after paste settles.
    /// BUG-54 [HIGH]: Inline SQM/perimeter formula replaced with shared
    ///                ProformaChargeMetricsCalculator.ComputePolishLm.
    /// BUG-55 [MEDIUM]: Subscription pattern documented — weak events not
    ///                  needed at current data volumes but flagged for
    ///                  future scaling.
    /// </summary>
    public class OtherChargesViewModel : ViewModelBase
    {
        private readonly DataGridViewModel _dataGridVM;

        // BUG-53: debouncer for polish recalculation
        private readonly DispatcherTimer _polishDebouncer;
        private const int POLISH_DEBOUNCE_MS = 150;

        public string Title { get; } = "OTHER CHARGES";
        public string Subtitle { get; } = "Supplementary, fabrication and delivery charges — calculated live from DataGrid metrics.";

        public MetricDiscoveryHubViewModel MetricHub { get; }

        public ObservableCollection<GridSourceOption> AllSources { get; } = new();
        public ObservableCollection<OtherChargeRow> ChargeRows { get; } = new();

        public WastagesSectionViewModel WastagesSection { get; }
        public CustomChargesSectionViewModel CustomChargesSection { get; }

        // ── Total ──────────────────────────────────────────────────────────

        private decimal _totalOtherCharges;
        public decimal TotalOtherCharges
        {
            get => _totalOtherCharges;
            private set { if (_totalOtherCharges == value) return; _totalOtherCharges = value; OnPropertyChanged(); }
        }

        // ── Constructor ────────────────────────────────────────────────────

        public OtherChargesViewModel(DataGridViewModel dataGridVM)
        {
            _dataGridVM = dataGridVM ?? throw new ArgumentNullException(nameof(dataGridVM));

            // BUG-53: initialize debouncer
            _polishDebouncer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(POLISH_DEBOUNCE_MS)
            };
            _polishDebouncer.Tick += OnPolishDebounceElapsed;

            MetricHub = new MetricDiscoveryHubViewModel(_dataGridVM.SpecGroups);
            MetricHub.PropertyChanged += OnHubPropertyChanged;

            BuildSourcePool();
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

        // ── Source pool ────────────────────────────────────────────────────

        private void BuildSourcePool()
        {
            AllSources.Clear();

            AllSources.Add(new GridSourceOption { Key = "LM1", Unit = OtherChargeUnit.Lm, FormulaHint = "W1 H1 PERIMETER (LM1)" });
            AllSources.Add(new GridSourceOption { Key = "LM2", Unit = OtherChargeUnit.Lm, FormulaHint = "W2 H2 PERIMETER (LM2)" });
            AllSources.Add(new GridSourceOption { Key = "LM_COMBINED", Unit = OtherChargeUnit.Lm, FormulaHint = "LM1 + LM2 COMBINED" });
            AllSources.Add(new GridSourceOption { Key = "SQM1", Unit = OtherChargeUnit.Sqm, FormulaHint = "GLASS 1 AREA (SQM1)" });
            AllSources.Add(new GridSourceOption { Key = "SQM2", Unit = OtherChargeUnit.Sqm, FormulaHint = "GLASS 2 AREA (SQM2)" });
            AllSources.Add(new GridSourceOption { Key = "SQM_COMBINED", Unit = OtherChargeUnit.Sqm, FormulaHint = "SQM1 + SQM2 COMBINED" });
            AllSources.Add(new GridSourceOption { Key = "DGU_CAVITY", Unit = OtherChargeUnit.Sqm, FormulaHint = "DGU CAVITY AREA" });
            AllSources.Add(new GridSourceOption { Key = "TOTAL_PIECES", Unit = OtherChargeUnit.Qty, FormulaHint = "TOTAL PIECE COUNT" });
            AllSources.Add(new GridSourceOption { Key = "DOUBLE_GLASS", Unit = OtherChargeUnit.Qty, FormulaHint = "GLASS COUNT (DGU/LAM ×2)" });

            SyncSourceValuesFromHub();
        }

        private void OnHubPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SyncSourceValuesFromHub();
            WastagesSection?.SyncAllWastageSources();
        }

        private void SyncSourceValuesFromHub()
        {
            foreach (var src in AllSources)
            {
                src.Value = src.Key switch
                {
                    "LM1" => MetricHub.TotalLm1,
                    "LM2" => MetricHub.TotalLm2,
                    "LM_COMBINED" => MetricHub.CombinedLm,
                    "SQM1" => MetricHub.TotalSqm1,
                    "SQM2" => MetricHub.TotalSqm2,
                    "SQM_COMBINED" => MetricHub.CombinedSqm,
                    "DGU_CAVITY" => MetricHub.DguCavityArea,
                    "TOTAL_PIECES" => MetricHub.TotalPieces,
                    "DOUBLE_GLASS" => MetricHub.DoubleGlassPieces,
                    _ => 0m
                };

                UpdateDisplayLabel(src);
            }

            RecalculateTotal();
        }

        private static void UpdateDisplayLabel(GridSourceOption src)
        {
            string valueText = src.Unit switch
            {
                OtherChargeUnit.Sqm => $"{src.Value:0.##} sqm",
                OtherChargeUnit.Lm => $"{src.Value:0.##} lm",
                OtherChargeUnit.Qty => $"{(int)src.Value} pcs",
                _ => src.Value.ToString("0.##")
            };

            string prefix = src.Key switch
            {
                "LM1" => "LM1 (Glass 1 Perimeter)",
                "LM2" => "LM2 (Glass 2 Perimeter)",
                "LM_COMBINED" => "LM1 + LM2 (Combined)",
                "SQM1" => "SQM1 (Glass 1 Area)",
                "SQM2" => "SQM2 (Glass 2 Area)",
                "SQM_COMBINED" => "SQM1 + SQM2 (Combined)",
                "DGU_CAVITY" => "DGU Cavity Area",
                "TOTAL_PIECES" => "Total Pieces",
                "DOUBLE_GLASS" => "Double Glass Count (1+1)",
                _ => src.Key
            };

            src.DisplayLabel = $"{prefix}: {valueText}";
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

                    row.SelectedPolishSubType = row.AvailablePolishSubTypes[0];
                }
                else
                {
                    foreach (var src in AllSources.Where(s => s.Unit == template.Unit))
                        row.AvailableSources.Add(src);

                    var defaultSrc = row.AvailableSources.FirstOrDefault(
                        s => s.Key == template.DefaultSourceKey);
                    if (defaultSrc != null)
                        row.SelectedSource = defaultSrc;
                    else if (row.AvailableSources.Count > 0)
                        row.SelectedSource = row.AvailableSources[0];
                }

                row.PropertyChanged += OnChargeRowChanged;
                ChargeRows.Add(row);
            }
        }

        private void OnChargeRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(OtherChargeRow.ResultAmount)
                or nameof(OtherChargeRow.IsActive))
            {
                RecalculateTotal();
            }

            if (sender is OtherChargeRow row && row.IsPolishRow
                && e.PropertyName is nameof(OtherChargeRow.SelectedPolishSubType)
                    or nameof(OtherChargeRow.IsActive))
            {
                // BUG-53: use debounced path
                RequestPolishRecalculation();
            }
        }

        // ── Sub-section change tracking ────────────────────────────────────

        private void OnSubSectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WastagesSectionViewModel.TotalWastageValue)
                || e.PropertyName == nameof(CustomChargesSectionViewModel.TotalCustomChargesValue))
            {
                RecalculateTotal();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  POLISH LM CALCULATION (BUG-53 debounced, BUG-54 shared calculator)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// BUG-53: Requests a polish recalculation with debouncing.
        /// Multiple rapid calls (e.g. during Excel paste of 500 rows)
        /// coalesce into ONE recalculation after 150 ms of silence.
        /// </summary>
        private void RequestPolishRecalculation()
        {
            _polishDebouncer.Stop();
            _polishDebouncer.Start();
        }

        private void OnPolishDebounceElapsed(object sender, EventArgs e)
        {
            _polishDebouncer.Stop();
            RecalculatePolishRowsImmediate();
        }

        /// <summary>
        /// BUG-54: Uses shared ProformaChargeMetricsCalculator.ComputePolishLm
        /// instead of inline W+H formulas. Same math, single source of truth,
        /// automatic snapshot compatibility with PDF/Excel export layouts.
        /// </summary>
        private void RecalculatePolishRowsImmediate()
        {
            // Build a lightweight snapshot of spec groups for the shared calculator
            var specSnapshots = BuildSpecSnapshotsForCalculator();

            foreach (var row in ChargeRows.Where(r => r.IsPolishRow))
            {
                if (row.SelectedPolishSubType == null
                    || row.SelectedPolishSubType.Type == PolishSubType.Custom)
                {
                    // Custom → user enters qty manually; nothing to compute
                    continue;
                }

                // BUG-54: shared calculator
                decimal totalLm = ProformaChargeMetricsCalculator.ComputePolishLm(
                    specSnapshots,
                    row.SelectedPolishSubType.Type.ToString());

                row.SetCalculatedPolishQty(totalLm);
            }
        }

        /// <summary>
        /// Converts live SpecGroup VMs into POCO snapshots that the shared
        /// calculator understands. This is a tiny in-memory adapter — no DB,
        /// no allocation cost beyond the transient list.
        /// </summary>
        private List<SpecGroupSnapshot> BuildSpecSnapshotsForCalculator()
        {
            var result = new List<SpecGroupSnapshot>(_dataGridVM.SpecGroups.Count);

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

        private void OnSpecGroupsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (SpecGroup g in e.OldItems)
                    UnhookSpecGroup(g);

            if (e.NewItems != null)
                foreach (SpecGroup g in e.NewItems)
                    HookSpecGroup(g);

            RequestPolishRecalculation();  // BUG-53: debounced
            WastagesSection?.SyncAllWastageSources();
        }

        /// <summary>
        /// BUG-55: Documented — this iterates every item in the spec group
        /// and hooks a handler. For 500-row groups this creates 500 subs.
        /// Weak events would be more elegant but current data volumes
        /// (typical DGU project = 50-100 items) do not justify the complexity.
        /// Flagged for revisit if list virtualization issues appear.
        /// </summary>
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

        private void OnSpecGroupPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SpecGroup.SelectedGlassType))
            {
                RequestPolishRecalculation();  // BUG-53: debounced
                WastagesSection?.SyncAllWastageSources();
            }

            if (e.PropertyName is nameof(SpecGroup.FooterTotalSqm)
                or nameof(SpecGroup.FooterTotalSqm2)
                or nameof(SpecGroup.GroupName))
            {
                WastagesSection?.SyncAllWastageSources();
            }
        }

        private void OnItemRowsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (GlassItemRow r in e.OldItems)
                    r.PropertyChanged -= OnItemRowPropertyChanged;

            if (e.NewItems != null)
                foreach (GlassItemRow r in e.NewItems)
                    r.PropertyChanged += OnItemRowPropertyChanged;

            RequestPolishRecalculation();  // BUG-53: debounced
        }

        private void OnItemRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(GlassItemRow.W1)
                or nameof(GlassItemRow.H1)
                or nameof(GlassItemRow.W2)
                or nameof(GlassItemRow.H2)
                or nameof(GlassItemRow.Qty))
            {
                RequestPolishRecalculation();  // BUG-53: debounced
            }
        }

        // ── Total ──────────────────────────────────────────────────────────

        private void RecalculateTotal()
        {
            decimal predefinedSum = 0m;
            foreach (var row in ChargeRows)
                if (row.IsActive) predefinedSum += row.ResultAmount;

            decimal wastageSum = WastagesSection?.TotalWastageValue ?? 0m;
            decimal customSum = CustomChargesSection?.TotalCustomChargesValue ?? 0m;

            TotalOtherCharges = Math.Round(predefinedSum + wastageSum + customSum, 2);
        }
    }
}