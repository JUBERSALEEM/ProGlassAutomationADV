using ProGlassAutomation.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup
{
    /// <summary>
    /// Represents a saved company profile that can be quickly loaded.
    /// </summary>
    public class CompanyProfileCard : ViewModelBase
    {
        public string CompanyName { get; set; }
        public string ProjectName { get; set; }

        // Full data pre-filled when this profile is loaded
        public string Trn { get; set; }
        public string Location { get; set; }
    }

    /// <summary>
    /// ViewModel for Step 1 of the Proforma Invoice wizard.
    /// Handles company identity, TRN, dates, and fast-load profile selection.
    /// </summary>
    public class CompanySetupViewModel : ViewModelBase
    {
        // === Callback to parent wizard for navigation ===
        private readonly Action<int> _navigateToStep;

        // === Company Fields ===

        private string _companyName = string.Empty;
        public string CompanyName
        {
            get => _companyName;
            set { _companyName = value; OnPropertyChanged(); }
        }

        private string _companyTrn = string.Empty;
        public string CompanyTrn
        {
            get => _companyTrn;
            set { _companyTrn = value; OnPropertyChanged(); }
        }

        private string _companyLocation = string.Empty;
        public string CompanyLocation
        {
            get => _companyLocation;
            set { _companyLocation = value; OnPropertyChanged(); }
        }

        // === Proforma Fields ===

        private string _proformaId;
        public string ProformaId
        {
            get => _proformaId;
            set { _proformaId = value; OnPropertyChanged(); }
        }

        private DateTime _issueDate;
        public DateTime IssueDate
        {
            get => _issueDate;
            set { _issueDate = value; OnPropertyChanged(); }
        }

        private DateTime _validUntil;
        public DateTime ValidUntil
        {
            get => _validUntil;
            set { _validUntil = value; OnPropertyChanged(); }
        }

        private string _proformaStatus;
        public string ProformaStatus
        {
            get => _proformaStatus;
            set { _proformaStatus = value; OnPropertyChanged(); }
        }

        // Dropdown options for Status
        public List<string> StatusOptions { get; } = new List<string>
        {
            "Draft",
            "Sent",
            "Approved",
            "Rejected",
            "Expired"
        };

        // === Saved Company Profiles (Fast Setup Cards) ===
        public ObservableCollection<CompanyProfileCard> SavedProfiles { get; }

        // === Loaded Profile Indicator ===
        private string _loadedProfileName;
        public string LoadedProfileName
        {
            get => _loadedProfileName;
            set
            {
                _loadedProfileName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLoadedProfile));
            }
        }

        public bool HasLoadedProfile => !string.IsNullOrEmpty(LoadedProfileName);

        // === Commands ===
        public ICommand LoadProfileCommand { get; }
        public ICommand GoToGridCommand { get; }

        // Parameterless constructor for design-time / fallback
        public CompanySetupViewModel() : this(null) { }

        public CompanySetupViewModel(Action<int> navigateToStep)
        {
            _navigateToStep = navigateToStep;

            // Auto-generate proforma ID (PI-YEAR-001 format)
            ProformaId = $"PI-{DateTime.Now.Year}-001";

            // Default dates
            IssueDate = DateTime.Now;
            ValidUntil = DateTime.Now.AddDays(30);

            // Default status
            ProformaStatus = "Draft";

            // Sample saved profiles — will be replaced with DB-loaded data later
            SavedProfiles = new ObservableCollection<CompanyProfileCard>
            {
                new CompanyProfileCard
                {
                    CompanyName = "Apex Glazing & Glass Industries LLC",
                    ProjectName = "SZR Prestige Towers Facade",
                    Trn = "100485710800003",
                    Location = "Dubai, UAE"
                },
                new CompanyProfileCard
                {
                    CompanyName = "Emirates Glass Processing Co. LLC",
                    ProjectName = "Burj Azizi Glazing Phase 1",
                    Trn = "100290840100003",
                    Location = "Sharjah, UAE"
                },
                new CompanyProfileCard
                {
                    CompanyName = "Gulf Architectural Glass Factory",
                    ProjectName = "Yas Island Residential Heights",
                    Trn = "100777123400003",
                    Location = "Abu Dhabi, UAE"
                },
                new CompanyProfileCard
                {
                    CompanyName = "Arabian Aluminium Co. LLC",
                    ProjectName = "Burj Khalifa Sky-Lounge Glazing",
                    Trn = "100132456700003",
                    Location = "Dubai, UAE"
                },
                new CompanyProfileCard
                {
                    CompanyName = "Technal Middle East Glass Factory",
                    ProjectName = "Abu Dhabi Airport Midfield Terminal Extension",
                    Trn = "100908070600003",
                    Location = "Abu Dhabi, UAE"
                }
            };

            // Load Profile Command — fills form fields from selected card
            LoadProfileCommand = new RelayCommand(param =>
            {
                if (param is CompanyProfileCard profile)
                {
                    CompanyName = profile.CompanyName;
                    CompanyTrn = profile.Trn;
                    CompanyLocation = profile.Location;
                    LoadedProfileName = profile.CompanyName;
                }
            });

            // Go Directly to Grid (Step 5) Command
            GoToGridCommand = new RelayCommand(_ =>
            {
                _navigateToStep?.Invoke(5);
            });
        }
    }
}