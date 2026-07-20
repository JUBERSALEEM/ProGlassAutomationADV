using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges.Wastages;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// Main ViewModel for Step 6 (Other Charges).
    /// Owns predefined charge rows, wires them to live DataGrid metrics via GridSourceOption,
    /// computes Polish LM per sub-type by iterating raw spec-group rows,
    /// manages the Glass Wastages sub-section (color-aggregated),
    /// and exposes the running Total Other Charges (excluding VAT/Discount).
    /// </summary>
    public class OtherChargesViewModel : ViewModelBase
    {
        private readonly DataGridViewModel _dataGridVM;

        public string Title { get; } = "OTHER CHARGES";
        public string Subtitle { get; } = "Supplementary, fabrication and delivery charges — calculated live from DataGrid metrics.";

        /// <summary>Top stats hub (auto-refresh from DataGrid).</summary>
        public MetricDiscoveryHubViewModel MetricHub { get; }

        /// <summary>Reusable pool of grid-source options, kept in sync with DataGrid.</summary>
        public ObservableCollection<GridSourceOption> AllSources { get; } = new();

        /// <summary>Predefined charge rows (Argon, Silicon, Overlap, etc.).</summary>
        public ObservableCollection<OtherChargeRow> ChargeRows { get; } = new();

        /// <summary>Phase 7.11c — Color-aggregated wastage rows section.</summary>
        public WastagesSectionViewModel WastagesSection { get; }

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

            MetricHub = new MetricDiscoveryHubViewModel(_dataGridVM.SpecGroups);
            MetricHub.PropertyChanged += OnHubPropertyChanged;

            BuildSourcePool();
            BuildChargeRows();

            // Phase 7.11c: instantiate wastages sub-VM
            WastagesSection = new WastagesSectionViewModel(_dataGridVM);
            WastagesSection.PropertyChanged += OnWastagesSectionChanged;

            // Subscribe to DataGrid changes for Polish recalculation + Wastage source sync
            _dataGridVM.SpecGroups.CollectionChanged += OnSpecGroupsCollectionChanged;
            foreach (var g in _dataGridVM.SpecGroups)
                HookSpecGroup(g);

            RecalculatePolishRows();
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

            // Phase 7.11c: also refresh wastage sqm sources when metrics update
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
                    // Populate polish sub-types
                    foreach (var info in PolishSubTypeInfo.All)
                        row.AvailablePolishSubTypes.Add(info);

                    // Default to "All Side Polish"
                    row.SelectedPolishSubType = row.AvailablePolishSubTypes[0];
                }
                else
                {
                    // Standard row — filter available sources by unit type
                    foreach (var src in AllSources.Where(s => s.Unit == template.Unit))
                        row.AvailableSources.Add(src);

                    var defaultSrc = row.AvailableSources.FirstOrDefault(s => s.Key == template.DefaultSourceKey);
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

            // Polish sub-type change or active toggle → recompute polish LM
            if (sender is OtherChargeRow row && row.IsPolishRow
                && e.PropertyName is nameof(OtherChargeRow.SelectedPolishSubType)
                    or nameof(OtherChargeRow.IsActive))
            {
                RecalculatePolishRows();
            }
        }

        // ── Wastages section change tracking ───────────────────────────────

        private void OnWastagesSectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WastagesSectionViewModel.TotalWastageValue))
            {
                RecalculateTotal();
            }
        }

        // ── Polish LM calculation ──────────────────────────────────────────

        /// <summary>
        /// Called whenever spec groups / rows / dimensions change.
        /// Iterates every glass row across every spec group and computes
        /// the total polish LM based on the row's selected sub-type formula.
        /// </summary>
        private void RecalculatePolishRows()
        {
            foreach (var row in ChargeRows.Where(r => r.IsPolishRow))
            {
                if (row.SelectedPolishSubType == null
                    || row.SelectedPolishSubType.Type == PolishSubType.Custom)
                {
                    // Custom → user enters qty manually; nothing to compute here
                    continue;
                }

                decimal totalLm = 0m;
                foreach (var group in _dataGridVM.SpecGroups)
                {
                    foreach (var itemRow in group.ItemRows)
                    {
                        totalLm += CalculatePolishLmForRow(
                            itemRow.W1, itemRow.H1, itemRow.W2, itemRow.H2,
                            itemRow.Qty, group.SelectedGlassType,
                            row.SelectedPolishSubType.Type);
                    }
                }

                row.SetCalculatedPolishQty(Math.Round(totalLm, 3));
            }
        }

        /// <summary>
        /// Computes polish LM for ONE glass row based on sub-type formula.
        /// For DGU/LAM (2 glasses), polish applies to BOTH glasses so we sum W1H1 + W2H2 perimeters.
        /// </summary>
        private static decimal CalculatePolishLmForRow(
            int w1, int h1, int w2, int h2, int qty, string glassType, PolishSubType subType)
        {
            if (qty <= 0) return 0m;

            bool isDoubleGlass = glassType == "DGU" || glassType == "LAM" || glassType == "DGU+LAM";

            decimal lm = 0m;

            // Glass 1 (outer)
            if (w1 > 0 && h1 > 0)
                lm += CalcSingleGlassPolish(w1, h1, subType);

            // Glass 2 (inner) — only for DGU/LAM
            if (isDoubleGlass && w2 > 0 && h2 > 0)
                lm += CalcSingleGlassPolish(w2, h2, subType);

            return lm * qty;
        }

        private static decimal CalcSingleGlassPolish(int w, int h, PolishSubType subType)
        {
            return subType switch
            {
                PolishSubType.AllSidePolish => ((decimal)w + h) / 1000m * 2m,
                PolishSubType.AllSidePolishAsPerDwg => ((decimal)w + h) / 1000m * 2m,
                PolishSubType.BothHeightSide => (decimal)h / 1000m * 2m,
                PolishSubType.BothWidthSide => (decimal)w / 1000m * 2m,
                _ => 0m
            };
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

            RecalculatePolishRows();

            // Phase 7.11c: refresh wastage sources when spec groups added/removed
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

        private void OnSpecGroupPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SpecGroup.SelectedGlassType))
            {
                RecalculatePolishRows();
                WastagesSection?.SyncAllWastageSources();
            }

            // Phase 7.11c: refresh wastage sources when footer totals change
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

            RecalculatePolishRows();
        }

        private void OnItemRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(GlassItemRow.W1)
                or nameof(GlassItemRow.H1)
                or nameof(GlassItemRow.W2)
                or nameof(GlassItemRow.H2)
                or nameof(GlassItemRow.Qty))
            {
                RecalculatePolishRows();
            }
        }

        // ── Total ──────────────────────────────────────────────────────────

        private void RecalculateTotal()
        {
            decimal predefinedSum = 0m;
            foreach (var row in ChargeRows)
                if (row.IsActive) predefinedSum += row.ResultAmount;

            decimal wastageSum = WastagesSection?.TotalWastageValue ?? 0m;

            TotalOtherCharges = Math.Round(predefinedSum + wastageSum, 2);
        }
    }
}