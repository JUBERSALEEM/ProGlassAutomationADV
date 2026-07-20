using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid
{
    public class GlassItemRow : ViewModelBase
    {
        private const decimal MIN_SQM = 0.5m;

        private int _serialNumber;
        public int SerialNumber { get => _serialNumber; set { _serialNumber = value; OnPropertyChanged(); } }

        private string _glassRef;
        public string GlassRef { get => _glassRef; set { _glassRef = value; OnPropertyChanged(); } }

        private int _w1;
        public int W1 { get => _w1; set { _w1 = value; OnPropertyChanged(); RecalculateAll(); } }

        private int _h1;
        public int H1 { get => _h1; set { _h1 = value; OnPropertyChanged(); RecalculateAll(); } }

        private int _w2;
        public int W2 { get => _w2; set { _w2 = value; OnPropertyChanged(); RecalculateAll(); } }

        private int _h2;
        public int H2 { get => _h2; set { _h2 = value; OnPropertyChanged(); RecalculateAll(); } }

        private int _qty = 1;
        public int Qty { get => _qty; set { _qty = value; OnPropertyChanged(); RecalculateAll(); } }

        private decimal _sqm1;
        public decimal Sqm1 { get => _sqm1; private set { _sqm1 = value; OnPropertyChanged(); } }

        private decimal _totalSqm;
        public decimal TotalSqm { get => _totalSqm; private set { _totalSqm = value; OnPropertyChanged(); } }

        private decimal _sqm2;
        public decimal Sqm2 { get => _sqm2; private set { _sqm2 = value; OnPropertyChanged(); } }

        private decimal _totalSqm2;
        public decimal TotalSqm2 { get => _totalSqm2; private set { _totalSqm2 = value; OnPropertyChanged(); } }

        private decimal _rate;
        public decimal Rate { get => _rate; set { _rate = value; OnPropertyChanged(); RecalculateAll(); } }

        private decimal _value;
        public decimal Value { get => _value; private set { _value = value; OnPropertyChanged(); } }

        // LM properties
        private decimal _lm1;
        public decimal Lm1 { get => _lm1; private set { _lm1 = value; OnPropertyChanged(); } }

        private decimal _lm2;
        public decimal Lm2 { get => _lm2; private set { _lm2 = value; OnPropertyChanged(); } }

        // Factor: 2 for SGU/DGU, 4 for LAM (all side polish)
        private int _perimeterFactor = 2;
        public int PerimeterFactor { get => _perimeterFactor; set { _perimeterFactor = value; OnPropertyChanged(); RecalculateAll(); } }

        public void RecalculateAll()
        {
            // SQM1
            decimal rawSqm1 = (decimal)W1 * H1 / 1_000_000m;
            Sqm1 = W1 > 0 && H1 > 0 ? System.Math.Max(System.Math.Round(rawSqm1, 3), MIN_SQM) : 0m;
            TotalSqm = System.Math.Round(Sqm1 * Qty, 3);

            // SQM2
            decimal rawSqm2 = (decimal)W2 * H2 / 1_000_000m;
            Sqm2 = W2 > 0 && H2 > 0 ? System.Math.Max(System.Math.Round(rawSqm2, 3), MIN_SQM) : 0m;
            TotalSqm2 = System.Math.Round(Sqm2 * Qty, 3);

            // LM1 = (W1+H1)/1000 * factor * qty
            Lm1 = W1 > 0 && H1 > 0 ? System.Math.Round(((decimal)W1 + H1) / 1000m * PerimeterFactor * Qty, 3) : 0m;

            // LM2 = (W2+H2)/1000 * factor * qty
            Lm2 = W2 > 0 && H2 > 0 ? System.Math.Round(((decimal)W2 + H2) / 1000m * PerimeterFactor * Qty, 3) : 0m;

            // Value
            Value = System.Math.Round(Sqm1 * Qty * Rate, 2);
        }
    }

    public class SpecGroup : ViewModelBase
    {
        public int GroupNumber { get; set; }
        private string _groupName;
        public string GroupName { get => _groupName; set { _groupName = value; OnPropertyChanged(); } }

        private decimal _baseRate;
        public decimal BaseRate
        {
            get => _baseRate;
            set
            {
                _baseRate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveRate));
                UpdateAllRowRates();
            }
        }

        private decimal _surchargePercent;
        public decimal SurchargePercent
        {
            get => _surchargePercent;
            set
            {
                _surchargePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveRate));
                UpdateAllRowRates();
            }
        }

        public decimal EffectiveRate => System.Math.Round(BaseRate * (1m + (SurchargePercent / 100m)), 2);

        private string _selectedGlassType;
        public string SelectedGlassType
        {
            get => _selectedGlassType;
            set { _selectedGlassType = value; OnPropertyChanged(); UpdatePerimeterFactors(); LoadFilteredSpecs(); }
        }

        private string _selectedSpecName;
        public string SelectedSpecName { get => _selectedSpecName; set { _selectedSpecName = value; OnPropertyChanged(); TryAutoFillRate(); } }

        public ObservableCollection<SavedSpecification> FilteredSpecs { get; } = new();
        public ObservableCollection<GlassItemRow> ItemRows { get; } = new();

        public int TotalRows => ItemRows?.Count ?? 0;
        public int FooterTotalQty => ItemRows?.Sum(r => r.Qty) ?? 0;
        public decimal FooterTotalSqm => System.Math.Round(ItemRows?.Sum(r => r.TotalSqm) ?? 0m, 3);
        public decimal FooterTotalSqm2 => System.Math.Round(ItemRows?.Sum(r => r.TotalSqm2) ?? 0m, 3);
        public decimal FooterTotalLm1 => System.Math.Round(ItemRows?.Sum(r => r.Lm1) ?? 0m, 3);
        public decimal FooterTotalLm2 => System.Math.Round(ItemRows?.Sum(r => r.Lm2) ?? 0m, 3);
        public decimal FooterTotalValue => System.Math.Round(ItemRows?.Sum(r => r.Value) ?? 0m, 2);

        public ICommand AddRowCommand { get; }
        public ICommand RemoveRowCommand { get; }

        public SpecGroup()
        {
            ItemRows.CollectionChanged += OnItemRowsChanged;
            AddRowCommand = new RelayCommand(_ => AddNewRow());
            RemoveRowCommand = new RelayCommand(p => { if (p is GlassItemRow r) ItemRows.Remove(r); });
        }

        private void OnItemRowsChanged(object s, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null) foreach (GlassItemRow r in e.OldItems) r.PropertyChanged -= OnRowChanged;
            if (e.NewItems != null) foreach (GlassItemRow r in e.NewItems) r.PropertyChanged += OnRowChanged;
            OnPropertyChanged(nameof(TotalRows));
            RenumberRows();
            RefreshFooter();
        }

        private void OnRowChanged(object s, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(GlassItemRow.Qty) or nameof(GlassItemRow.Sqm1) or nameof(GlassItemRow.TotalSqm) or nameof(GlassItemRow.Sqm2) or nameof(GlassItemRow.TotalSqm2) or nameof(GlassItemRow.Lm1) or nameof(GlassItemRow.Lm2) or nameof(GlassItemRow.Value))
                RefreshFooter();

            // Auto-adjust rate based on SQM size
            // SQM >= 4 → EffectiveRate (with surcharge)
            // SQM < 4  → BaseRate (no surcharge)
            if (e.PropertyName == nameof(GlassItemRow.Sqm1) && s is GlassItemRow row)
            {
                var targetRate = row.Sqm1 >= 4m ? EffectiveRate : BaseRate;
                if (row.Rate != targetRate)
                    row.Rate = targetRate;
            }
        }

        private void RefreshFooter()
        {
            OnPropertyChanged(nameof(FooterTotalQty));
            OnPropertyChanged(nameof(FooterTotalSqm));
            OnPropertyChanged(nameof(FooterTotalSqm2));
            OnPropertyChanged(nameof(FooterTotalLm1));
            OnPropertyChanged(nameof(FooterTotalLm2));
            OnPropertyChanged(nameof(FooterTotalValue));
        }

        private void AddNewRow()
        {
            var row = new GlassItemRow { SerialNumber = ItemRows.Count + 1, GlassRef = "", Qty = 1, Rate = EffectiveRate };
            row.PerimeterFactor = SelectedGlassType == "LAM" ? 4 : 2;
            ItemRows.Add(row);
        }

        private void RenumberRows()
        {
            for (int i = 0; i < ItemRows.Count; i++)
                ItemRows[i].SerialNumber = i + 1;
        }

        private void UpdatePerimeterFactors()
        {
            int factor = SelectedGlassType == "LAM" ? 4 : 2;
            foreach (var row in ItemRows) row.PerimeterFactor = factor;
        }

        private void UpdateAllRowRates()
        {
            foreach (var row in ItemRows)
            {
                // SQM >= 4 → EffectiveRate (with surcharge)
                // SQM < 4  → BaseRate (no surcharge)
                row.Rate = row.Sqm1 >= 4m ? EffectiveRate : BaseRate;
            }
        }

        public async void LoadFilteredSpecs()
        {
            FilteredSpecs.Clear();
            if (string.IsNullOrWhiteSpace(SelectedGlassType) || App.Specs == null) return;
            try { var s = await App.Specs.GetAllAsync(SelectedGlassType); foreach (var x in s) FilteredSpecs.Add(x); } catch { }
        }

        private void TryAutoFillRate()
        {
            if (string.IsNullOrWhiteSpace(SelectedSpecName)) return;
            var m = FilteredSpecs?.FirstOrDefault(s => string.Equals(s.SpecName, SelectedSpecName, System.StringComparison.OrdinalIgnoreCase));
            if (m != null) BaseRate = m.PricePerSqm;
        }
    }

    public class DataGridViewModel : ViewModelBase
    {
        public string Title { get; } = "SPECIFICATION SUB-GROUPS";
        public string Subtitle { get; } = "Specify the number of unique glass specifications in this proforma.";
        public List<string> GlassTypeOptions { get; } = new() { "SGU", "DGU", "LAM", "DGU+LAM" };
        public ObservableCollection<SpecGroup> SpecGroups { get; }
        public int TotalSpecGroups => SpecGroups?.Count ?? 0;
        public ICommand AddSpecGroupCommand { get; }
        public ICommand RemoveSpecGroupCommand { get; }

        public DataGridViewModel()
        {
            SpecGroups = new(); SpecGroups.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalSpecGroups));
            AddNewSpecGroup();
            AddSpecGroupCommand = new RelayCommand(_ => AddNewSpecGroup());
            RemoveSpecGroupCommand = new RelayCommand(p => { if (p is SpecGroup g && SpecGroups.Count > 1) { SpecGroups.Remove(g); RenumberGroups(); } });
        }

        private void AddNewSpecGroup() => SpecGroups.Add(new SpecGroup { GroupNumber = SpecGroups.Count + 1, GroupName = $"Spec Group {SpecGroups.Count + 1}", BaseRate = 150m, SurchargePercent = 0m, SelectedGlassType = "SGU" });
        private void RenumberGroups() { for (int i = 0; i < SpecGroups.Count; i++) { SpecGroups[i].GroupNumber = i + 1; if (SpecGroups[i].GroupName?.StartsWith("Spec Group ") == true) SpecGroups[i].GroupName = $"Spec Group {i + 1}"; } }
    }
}