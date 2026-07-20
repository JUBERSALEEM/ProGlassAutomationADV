using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// Main ViewModel for Step 6 (Other Charges).
    /// Owns predefined charge rows, wires them to live DataGrid metrics via GridSourceOption,
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
        }

        // ── Source pool ────────────────────────────────────────────────────

        /// <summary>
        /// Creates the master list of grid-source options and their live values.
        /// Called once on init — options are then continuously updated by OnHubPropertyChanged.
        /// </summary>
        private void BuildSourcePool()
        {
            AllSources.Clear();

            AllSources.Add(new GridSourceOption
            {
                Key = "LM1",
                Unit = OtherChargeUnit.Lm,
                FormulaHint = "W1 H1 PERIMETER (LM1)"
            });
            AllSources.Add(new GridSourceOption
            {
                Key = "LM2",
                Unit = OtherChargeUnit.Lm,
                FormulaHint = "W2 H2 PERIMETER (LM2)"
            });
            AllSources.Add(new GridSourceOption
            {
                Key = "LM_COMBINED",
                Unit = OtherChargeUnit.Lm,
                FormulaHint = "LM1 + LM2 COMBINED"
            });
            AllSources.Add(new GridSourceOption
            {
                Key = "SQM1",
                Unit = OtherChargeUnit.Sqm,
                FormulaHint = "GLASS 1 AREA (SQM1)"
            });
            AllSources.Add(new GridSourceOption
            {
                Key = "SQM2",
                Unit = OtherChargeUnit.Sqm,
                FormulaHint = "GLASS 2 AREA (SQM2)"
            });
            AllSources.Add(new GridSourceOption
            {
                Key = "SQM_COMBINED",
                Unit = OtherChargeUnit.Sqm,
                FormulaHint = "SQM1 + SQM2 COMBINED"
            });
            AllSources.Add(new GridSourceOption
            {
                Key = "DGU_CAVITY",
                Unit = OtherChargeUnit.Sqm,
                FormulaHint = "DGU CAVITY AREA"
            });
            AllSources.Add(new GridSourceOption
            {
                Key = "TOTAL_PIECES",
                Unit = OtherChargeUnit.Qty,
                FormulaHint = "TOTAL PIECE COUNT"
            });
            AllSources.Add(new GridSourceOption
            {
                Key = "DOUBLE_GLASS",
                Unit = OtherChargeUnit.Qty,
                FormulaHint = "GLASS COUNT (DGU/LAM ×2)"
            });

            SyncSourceValuesFromHub();
        }

        private void OnHubPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SyncSourceValuesFromHub();
        }

        /// <summary>Pushes the latest Hub metric values into every GridSourceOption.</summary>
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

                // Filter available sources by unit type
                foreach (var src in AllSources.Where(s => s.Unit == template.Unit))
                    row.AvailableSources.Add(src);

                // Pre-select the template's suggested source
                var defaultSrc = row.AvailableSources.FirstOrDefault(s => s.Key == template.DefaultSourceKey);
                if (defaultSrc != null)
                    row.SelectedSource = defaultSrc;
                else if (row.AvailableSources.Count > 0)
                    row.SelectedSource = row.AvailableSources[0];

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
        }

        // ── Total ──────────────────────────────────────────────────────────

        private void RecalculateTotal()
        {
            decimal sum = 0m;
            foreach (var row in ChargeRows)
                if (row.IsActive) sum += row.ResultAmount;

            TotalOtherCharges = Math.Round(sum, 2);
        }
    }
}