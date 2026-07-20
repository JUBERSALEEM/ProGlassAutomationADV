using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// Read-only stats widget at the top of Step 6.
    /// Live-aggregates key metrics from the DataGrid (Step 5) — Total LM1, LM2, combined,
    /// pieces, double-glass count, per-area breakdowns, and primary glass color list.
    /// </summary>
    public class MetricDiscoveryHubViewModel : ViewModelBase
    {
        private readonly ObservableCollection<SpecGroup> _specGroups;

        // ── Live metrics ────────────────────────────────────────────────────

        private decimal _totalLm1;
        public decimal TotalLm1 { get => _totalLm1; private set { if (_totalLm1 == value) return; _totalLm1 = value; OnPropertyChanged(); } }

        private decimal _totalLm2;
        public decimal TotalLm2 { get => _totalLm2; private set { if (_totalLm2 == value) return; _totalLm2 = value; OnPropertyChanged(); } }

        private decimal _combinedLm;
        public decimal CombinedLm { get => _combinedLm; private set { if (_combinedLm == value) return; _combinedLm = value; OnPropertyChanged(); } }

        private int _totalPieces;
        public int TotalPieces { get => _totalPieces; private set { if (_totalPieces == value) return; _totalPieces = value; OnPropertyChanged(); } }

        private int _doubleGlassPieces;
        public int DoubleGlassPieces { get => _doubleGlassPieces; private set { if (_doubleGlassPieces == value) return; _doubleGlassPieces = value; OnPropertyChanged(); } }

        private decimal _totalSqm1;
        public decimal TotalSqm1 { get => _totalSqm1; private set { if (_totalSqm1 == value) return; _totalSqm1 = value; OnPropertyChanged(); } }

        private decimal _totalSqm2;
        public decimal TotalSqm2 { get => _totalSqm2; private set { if (_totalSqm2 == value) return; _totalSqm2 = value; OnPropertyChanged(); } }

        private decimal _combinedSqm;
        public decimal CombinedSqm { get => _combinedSqm; private set { if (_combinedSqm == value) return; _combinedSqm = value; OnPropertyChanged(); } }

        private decimal _dguCavityArea;
        public decimal DguCavityArea { get => _dguCavityArea; private set { if (_dguCavityArea == value) return; _dguCavityArea = value; OnPropertyChanged(); } }

        public ObservableCollection<string> PrimaryGlassRefs { get; } = new();

        // ── Constructor ────────────────────────────────────────────────────

        public MetricDiscoveryHubViewModel(ObservableCollection<SpecGroup> specGroups)
        {
            _specGroups = specGroups ?? throw new System.ArgumentNullException(nameof(specGroups));

            _specGroups.CollectionChanged += OnSpecGroupsChanged;
            foreach (var g in _specGroups)
                g.PropertyChanged += OnSpecGroupPropertyChanged;

            Refresh();
        }

        // ── Change tracking ────────────────────────────────────────────────

        private void OnSpecGroupsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (SpecGroup g in e.OldItems)
                    g.PropertyChanged -= OnSpecGroupPropertyChanged;

            if (e.NewItems != null)
                foreach (SpecGroup g in e.NewItems)
                    g.PropertyChanged += OnSpecGroupPropertyChanged;

            Refresh();
        }

        private void OnSpecGroupPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SpecGroup.FooterTotalQty)
                or nameof(SpecGroup.FooterTotalSqm)
                or nameof(SpecGroup.FooterTotalSqm2)
                or nameof(SpecGroup.FooterTotalLm1)
                or nameof(SpecGroup.FooterTotalLm2)
                or nameof(SpecGroup.SelectedGlassType))
            {
                Refresh();
            }
        }

        // ── Aggregation ────────────────────────────────────────────────────

        public void Refresh()
        {
            decimal lm1 = 0m, lm2 = 0m, sqm1 = 0m, sqm2 = 0m, dguArea = 0m;
            int qty = 0, doubleQty = 0;

            PrimaryGlassRefs.Clear();

            foreach (var g in _specGroups)
            {
                lm1 += g.FooterTotalLm1;
                lm2 += g.FooterTotalLm2;
                sqm1 += g.FooterTotalSqm;
                sqm2 += g.FooterTotalSqm2;
                qty += g.FooterTotalQty;

                // Double-glass = DGU or LAM has 2 pieces per unit
                bool isDoubleGlass = g.SelectedGlassType == "DGU"
                                     || g.SelectedGlassType == "LAM"
                                     || g.SelectedGlassType == "DGU+LAM";

                if (isDoubleGlass)
                {
                    doubleQty += g.FooterTotalQty * 2;
                    dguArea += g.FooterTotalSqm;
                }
                else
                {
                    doubleQty += g.FooterTotalQty;
                }

                // Collect primary glass ref label
                if (!string.IsNullOrWhiteSpace(g.SelectedSpecName))
                {
                    if (!PrimaryGlassRefs.Contains(g.SelectedSpecName))
                        PrimaryGlassRefs.Add(g.SelectedSpecName);
                }
            }

            TotalLm1 = System.Math.Round(lm1, 2);
            TotalLm2 = System.Math.Round(lm2, 2);
            CombinedLm = System.Math.Round(lm1 + lm2, 2);
            TotalSqm1 = System.Math.Round(sqm1, 2);
            TotalSqm2 = System.Math.Round(sqm2, 2);
            CombinedSqm = System.Math.Round(sqm1 + sqm2, 2);
            DguCavityArea = System.Math.Round(dguArea, 2);
            TotalPieces = qty;
            DoubleGlassPieces = doubleQty;
        }
    }
}