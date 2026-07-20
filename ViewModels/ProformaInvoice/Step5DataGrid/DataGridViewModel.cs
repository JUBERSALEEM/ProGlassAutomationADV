using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

        public decimal EffectiveRate => System.Math.Round(BaseRate * (1m + (SurchargePercent / 100m)), 2);

        // === Glass Type + Specification Selection ===

        /// <summary>
        /// Selected glass type (SGU / DGU / LAM / DGU+LAM).
        /// When changed, the Specification dropdown filters and reloads.
        /// </summary>
        private string _selectedGlassType;
        public string SelectedGlassType
        {
            get => _selectedGlassType;
            set
            {
                _selectedGlassType = value;
                OnPropertyChanged();

                // Reload specs filtered by new glass type
                LoadFilteredSpecs();
            }
        }

        /// <summary>
        /// User can type manually OR pick from dropdown.
        /// Selecting from dropdown auto-fills BaseRate.
        /// </summary>
        private string _selectedSpecName;
        public string SelectedSpecName
        {
            get => _selectedSpecName;
            set
            {
                _selectedSpecName = value;
                OnPropertyChanged();

                // Auto-fill BaseRate from matching spec
                TryAutoFillRate();
            }
        }

        // === Filtered Specs List (loaded from DB) ===
        public ObservableCollection<SavedSpecification> FilteredSpecs { get; }
            = new ObservableCollection<SavedSpecification>();

        /// <summary>
        /// Loads specs from specifications.db, filtered by SelectedGlassType.
        /// Sorted by UsageCount DESC, then LastUsedAt DESC.
        /// </summary>
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
            catch
            {
                // Silently fail if DB not accessible; user can still type manually
            }
        }

        /// <summary>
        /// When user picks a spec from dropdown, auto-fill the Base Rate.
        /// If user typed a custom spec name (not in list), no rate change.
        /// </summary>
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
    /// Each spec group contains multiple item rows with 11 columns:
    /// SR | GlassRef | W1 | H1 | W2 | H2 | Qty | SQM | TotalSQM | Rate | Value
    /// Rates from Step 4 (Pricing Estimator) are pushed here via APPLY button.
    /// </summary>
    public class DataGridViewModel : ViewModelBase
    {
        // === Header Info ===
        public string Title { get; } = "SPECIFICATION SUB-GROUPS";
        public string Subtitle { get; } = "Specify the number of unique glass specifications in this proforma.";

        // === Glass Type Options (shared across all spec groups) ===
        public List<string> GlassTypeOptions { get; } = new List<string>
        {
            "SGU",
            "DGU",
            "LAM",
            "DGU+LAM"
        };

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
                BaseRate = 150m,
                SurchargePercent = 0m,
                SelectedGlassType = "SGU"   // Default to SGU (triggers spec load)
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