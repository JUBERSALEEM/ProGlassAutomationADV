using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid
{
    /// <summary>
    /// Represents a single glass panel line item (one row in the data grid).
    /// Has 11 columns: SR | GlassRef | W1 | H1 | W2 | H2 | Qty | SQM1 | SQM2 | Rate | Value
    ///
    /// Formulas:
    ///   SQM1 = max((W1 × H1) / 1,000,000, 0.5)   [per piece, minimum 0.5]
    ///   SQM2 = max((W2 × H2) / 1,000,000, 0.5)   [per piece, minimum 0.5]
    ///   Value = SQM1 × Qty × Rate                  [total value for the row]
    ///
    /// SQM1/SQM2/Value are auto-calculated from other fields.
    /// </summary>
    public class GlassItemRow : ViewModelBase
    {
        // Minimum billable SQM per piece (industry rule)
        private const decimal MIN_SQM = 0.5m;

        private int _serialNumber;
        public int SerialNumber
        {
            get => _serialNumber;
            set { _serialNumber = value; OnPropertyChanged(); }
        }

        private string _glassRef;
        public string GlassRef
        {
            get => _glassRef;
            set { _glassRef = value; OnPropertyChanged(); }
        }

        private int _w1;
        public int W1
        {
            get => _w1;
            set { _w1 = value; OnPropertyChanged(); RecalculateAll(); }
        }

        private int _h1;
        public int H1
        {
            get => _h1;
            set { _h1 = value; OnPropertyChanged(); RecalculateAll(); }
        }

        private int _w2;
        public int W2
        {
            get => _w2;
            set { _w2 = value; OnPropertyChanged(); RecalculateSqm2(); }
        }

        private int _h2;
        public int H2
        {
            get => _h2;
            set { _h2 = value; OnPropertyChanged(); RecalculateSqm2(); }
        }

        private int _qty = 1;
        public int Qty
        {
            get => _qty;
            set { _qty = value; OnPropertyChanged(); RecalculateAll(); }
        }

        private decimal _sqm1;
        public decimal Sqm1
        {
            get => _sqm1;
            private set { _sqm1 = value; OnPropertyChanged(); }
        }

        private decimal _sqm2;
        public decimal Sqm2
        {
            get => _sqm2;
            private set { _sqm2 = value; OnPropertyChanged(); }
        }

        private decimal _rate;
        public decimal Rate
        {
            get => _rate;
            set { _rate = value; OnPropertyChanged(); RecalculateAll(); }
        }

        private decimal _value;
        public decimal Value
        {
            get => _value;
            private set { _value = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Recalculates SQM1 and Value.
        /// SQM1 = max((W1 × H1) / 1,000,000, 0.5)  [per piece, minimum 0.5]
        /// Value = SQM1 × Qty × Rate
        /// </summary>
        private void RecalculateAll()
        {
            decimal rawSqm1 = (decimal)W1 * H1 / 1_000_000m;
            Sqm1 = System.Math.Max(System.Math.Round(rawSqm1, 3), MIN_SQM);

            Value = System.Math.Round(Sqm1 * Qty * Rate, 2);
        }

        /// <summary>
        /// Recalculates SQM2 only (independent of Value).
        /// SQM2 = max((W2 × H2) / 1,000,000, 0.5)  [per piece, minimum 0.5]
        /// </summary>
        private void RecalculateSqm2()
        {
            decimal rawSqm2 = (decimal)W2 * H2 / 1_000_000m;
            Sqm2 = System.Math.Max(System.Math.Round(rawSqm2, 3), MIN_SQM);
        }
    }

    /// <summary>
    /// Represents a single specification group in the data grid.
    /// Each group contains multiple item rows (glass panels).
    /// </summary>
    public class SpecGroup : ViewModelBase
    {
        public int GroupNumber { get; set; }

        private string _groupName;
        public string GroupName
        {
            get => _groupName;
            set { _groupName = value; OnPropertyChanged(); }
        }

        // === Header Info: Base Rate + Surcharge % ===

        private decimal _baseRate;
        public decimal BaseRate
        {
            get => _baseRate;
            set
            {
                _baseRate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveRate));
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
            }
        }

        public decimal EffectiveRate => System.Math.Round(BaseRate * (1m + (SurchargePercent / 100m)), 2);

        // === Glass Type + Specification Selection ===

        private string _selectedGlassType;
        public string SelectedGlassType
        {
            get => _selectedGlassType;
            set
            {
                _selectedGlassType = value;
                OnPropertyChanged();
                LoadFilteredSpecs();
            }
        }

        private string _selectedSpecName;
        public string SelectedSpecName
        {
            get => _selectedSpecName;
            set
            {
                _selectedSpecName = value;
                OnPropertyChanged();
                TryAutoFillRate();
            }
        }

        public ObservableCollection<SavedSpecification> FilteredSpecs { get; }
            = new ObservableCollection<SavedSpecification>();

        // === Item Rows (Glass Panels) ===
        public ObservableCollection<GlassItemRow> ItemRows { get; }
            = new ObservableCollection<GlassItemRow>();

        public int TotalRows => ItemRows?.Count ?? 0;

        // === Commands ===
        public ICommand AddRowCommand { get; }
        public ICommand RemoveRowCommand { get; }

        public SpecGroup()
        {
            ItemRows.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(TotalRows));
                RenumberRows();
            };

            AddRowCommand = new RelayCommand(_ => AddNewRow());
            RemoveRowCommand = new RelayCommand(param =>
            {
                if (param is GlassItemRow row)
                    ItemRows.Remove(row);
            });
        }

        private void AddNewRow()
        {
            var newRow = new GlassItemRow
            {
                SerialNumber = ItemRows.Count + 1,
                GlassRef = string.Empty,
                Qty = 1,
                Rate = EffectiveRate   // Default rate from spec group
            };
            ItemRows.Add(newRow);
        }

        private void RenumberRows()
        {
            for (int i = 0; i < ItemRows.Count; i++)
            {
                ItemRows[i].SerialNumber = i + 1;
            }
        }

        public async void LoadFilteredSpecs()
        {
            FilteredSpecs.Clear();

            if (string.IsNullOrWhiteSpace(SelectedGlassType) || App.Specs == null)
                return;

            try
            {
                var specs = await App.Specs.GetAllAsync(SelectedGlassType);
                foreach (var s in specs)
                    FilteredSpecs.Add(s);
            }
            catch { }
        }

        private void TryAutoFillRate()
        {
            if (string.IsNullOrWhiteSpace(SelectedSpecName) || FilteredSpecs == null)
                return;

            var match = FilteredSpecs.FirstOrDefault(s =>
                string.Equals(s.SpecName, SelectedSpecName, System.StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                BaseRate = match.PricePerSqm;
            }
        }
    }

    /// <summary>
    /// ViewModel for Step 5 of the Proforma Invoice wizard.
    /// Handles the data grid where users add glass panel line items.
    /// Each spec group contains multiple item rows with 11 columns.
    /// </summary>
    public class DataGridViewModel : ViewModelBase
    {
        public string Title { get; } = "SPECIFICATION SUB-GROUPS";
        public string Subtitle { get; } = "Specify the number of unique glass specifications in this proforma.";

        public List<string> GlassTypeOptions { get; } = new List<string>
        {
            "SGU",
            "DGU",
            "LAM",
            "DGU+LAM"
        };

        public ObservableCollection<SpecGroup> SpecGroups { get; }

        public int TotalSpecGroups => SpecGroups?.Count ?? 0;

        public ICommand AddSpecGroupCommand { get; }
        public ICommand RemoveSpecGroupCommand { get; }

        public DataGridViewModel()
        {
            SpecGroups = new ObservableCollection<SpecGroup>();
            SpecGroups.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalSpecGroups));

            AddNewSpecGroup();

            AddSpecGroupCommand = new RelayCommand(_ => AddNewSpecGroup());
            RemoveSpecGroupCommand = new RelayCommand(param =>
            {
                if (param is SpecGroup group && SpecGroups.Count > 1)
                {
                    SpecGroups.Remove(group);
                    RenumberGroups();
                }
            });
        }

        private void AddNewSpecGroup()
        {
            var newGroup = new SpecGroup
            {
                GroupNumber = SpecGroups.Count + 1,
                GroupName = $"Spec Group {SpecGroups.Count + 1}",
                BaseRate = 150m,
                SurchargePercent = 0m,
                SelectedGlassType = "SGU"
            };
            SpecGroups.Add(newGroup);
        }

        private void RenumberGroups()
        {
            for (int i = 0; i < SpecGroups.Count; i++)
            {
                SpecGroups[i].GroupNumber = i + 1;
                if (SpecGroups[i].GroupName?.StartsWith("Spec Group ") == true)
                {
                    SpecGroups[i].GroupName = $"Spec Group {i + 1}";
                }
            }
        }
    }
}