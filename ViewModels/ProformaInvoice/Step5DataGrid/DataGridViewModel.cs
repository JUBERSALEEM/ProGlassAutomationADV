using ProGlassAutomation.Commands;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid
{
    /// <summary>
    /// Represents a single specification group in the data grid.
    /// Each group will contain multiple item rows (glass panels)
    /// with dimensions, quantities, rates, and values.
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

        /// <summary>
        /// Auto-calculated final rate per SQM after applying surcharge.
        /// Formula: EffectiveRate = BaseRate * (1 + SurchargePercent / 100)
        /// Example: BaseRate=150, Surcharge=20% -> EffectiveRate = 180.00
        /// </summary>
        public decimal EffectiveRate => System.Math.Round(BaseRate * (1m + (SurchargePercent / 100m)), 2);

        // Item rows, glass specs will be added in Sub-Phases 7.4 through 7.11
    }

    /// <summary>
    /// ViewModel for Step 5 of the Proforma Invoice wizard.
    /// Handles the data grid where users add glass panel line items.
    /// Each spec group contains multiple item rows with 11 columns:
    /// SR | GlassRef | W1 | H1 | W2 | H2 | Qty | SQM | TotalSQM | Rate | Value
    /// Rates from Step 4 (Pricing Estimator) are pushed here via APPLY button.
    /// </summary>
    public class DataGridViewModel : ViewModelBase
    {
        // === Header Info ===
        public string Title { get; } = "SPECIFICATION SUB-GROUPS";
        public string Subtitle { get; } = "Specify the number of unique glass specifications in this proforma.";

        // === Spec Groups Collection ===
        public ObservableCollection<SpecGroup> SpecGroups { get; }

        // === Total Groups Counter (auto-updates from collection) ===
        public int TotalSpecGroups => SpecGroups?.Count ?? 0;

        // === Commands ===
        public ICommand AddSpecGroupCommand { get; }
        public ICommand RemoveSpecGroupCommand { get; }

        public DataGridViewModel()
        {
            SpecGroups = new ObservableCollection<SpecGroup>();

            // Auto-update TotalSpecGroups when collection changes
            SpecGroups.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalSpecGroups));

            // Seed with 1 default spec group (like the screenshot)
            AddNewSpecGroup();

            // Command: Add new spec group
            AddSpecGroupCommand = new RelayCommand(_ => AddNewSpecGroup());

            // Command: Remove a spec group
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
                BaseRate = 150m,         // Sensible default (matches screenshot)
                SurchargePercent = 0m    // No surcharge by default
            };
            SpecGroups.Add(newGroup);
        }

        private void RenumberGroups()
        {
            for (int i = 0; i < SpecGroups.Count; i++)
            {
                SpecGroups[i].GroupNumber = i + 1;
                // Only auto-rename if name matches default pattern
                if (SpecGroups[i].GroupName?.StartsWith("Spec Group ") == true)
                {
                    SpecGroups[i].GroupName = $"Spec Group {i + 1}";
                }
            }
        }
    }
}