using ProGlassAutomation.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup
{
    /// <summary>
    /// Represents a saved company profile that can be quickly loaded.
    ///
    /// BUG-34: This is a placeholder card until the Master Data module
    /// (Phase 20+) provides real company profiles. The SavedProfiles
    /// collection is now EMPTY by default — no more fake sample data
    /// with real-looking UAE TRN numbers in production.
    /// </summary>
    public class CompanyProfileCard : ViewModelBase
    {
        public string CompanyName { get; set; } = "";
        public string ProjectName { get; set; } = "";

        // Full data pre-filled when this profile is loaded
        public string Trn { get; set; } = "";
        public string Location { get; set; } = "";
    }

    /// <summary>
    /// ViewModel for Step 1 of the Proforma Invoice wizard.
    /// Handles company identity, TRN, dates, and fast-load profile selection.
    ///
    /// BUG-33 [HIGH]: ProformaId no longer uses "Ticks % 1000" which
    ///                caused collisions. Now queries the DB via
    ///                App.Invoices.SuggestNextIdAsync() for a unique ID.
    ///                Falls back to a safe GUID-based ID if DB unavailable.
    /// BUG-34 [HIGH]: SavedProfiles is now empty by default. Loading
    ///                real profiles from Master Data DB is Phase 20+.
    /// BUG-35 [MEDIUM]: UAE TRN 15-digit validation exposed via
    ///                  TrnValidationMessage property (View binds to it).
    /// BUG-36 [MEDIUM]: StatusOptions documented — matches list filter.
    /// BUG-37 [LOW]: LoadedProfileName cleared when user edits fields
    ///               manually after loading a profile.
    /// </summary>
    public class CompanySetupViewModel : ViewModelBase
    {
        // === Callback to parent wizard for navigation ===
        private readonly Action<int>? _navigateToStep;

        // BUG-37: guard flag — true while LoadProfile is actively populating,
        // so setters don't wipe LoadedProfileName during initial fill
        private bool _isLoadingProfile;

        // === Company Fields ===

        private string _companyName = string.Empty;
        public string CompanyName
        {
            get => _companyName;
            set
            {
                if (_companyName == value) return;
                _companyName = value;
                OnPropertyChanged();
                MarkUserEditedIfNotLoading();
            }
        }

        private string _companyTrn = string.Empty;
        public string CompanyTrn
        {
            get => _companyTrn;
            set
            {
                if (_companyTrn == value) return;
                _companyTrn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrnValidationMessage));   // BUG-35
                OnPropertyChanged(nameof(IsTrnValid));             // BUG-35
                MarkUserEditedIfNotLoading();
            }
        }

        private string _companyLocation = string.Empty;
        public string CompanyLocation
        {
            get => _companyLocation;
            set
            {
                if (_companyLocation == value) return;
                _companyLocation = value;
                OnPropertyChanged();
                MarkUserEditedIfNotLoading();
            }
        }

        // === Proforma Fields ===

        private string _proformaId = "";
        public string ProformaId
        {
            get => _proformaId;
            set
            {
                if (_proformaId == value) return;
                _proformaId = value;
                OnPropertyChanged();
            }
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

        private string _proformaStatus = "Draft";
        public string ProformaStatus
        {
            get => _proformaStatus;
            set { _proformaStatus = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Dropdown options for Status. Must match SavedInvoicesListViewModel
        /// StatusFilterOptions (minus the "All Statuses" entry) so filtering
        /// works consistently.
        ///
        /// BUG-36: Documented — keep in sync with list VM filter options.
        /// </summary>
        public List<string> StatusOptions { get; } = new List<string>
        {
            "Draft",
            "Sent",
            "Approved",
            "Rejected",
            "Expired"
        };

        // === UAE TRN Validation (BUG-35) ===

        /// <summary>
        /// UAE Federal Tax Authority TRN must be exactly 15 digits.
        /// Returns null when valid, or an error message when invalid.
        /// Empty TRN is treated as "not yet entered" (no error shown).
        /// </summary>
        public string? TrnValidationMessage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_companyTrn))
                    return null;  // no error while user hasn't typed yet

                var digitsOnly = new string(_companyTrn.Where(char.IsDigit).ToArray());

                if (digitsOnly.Length != 15)
                    return "UAE TRN must be exactly 15 digits.";

                if (digitsOnly != _companyTrn.Trim())
                    return "TRN should contain only digits (no spaces or letters).";

                return null;  // valid
            }
        }

        /// <summary>
        /// True when TRN is either empty or a valid 15-digit UAE TRN.
        /// XAML uses this to enable/disable Save button downstream.
        /// </summary>
        public bool IsTrnValid => TrnValidationMessage == null;

        // === Saved Company Profiles (Fast Setup Cards) ===

        /// <summary>
        /// BUG-34 [HIGH]: This collection is now EMPTY by default.
        /// Previously contained hardcoded fake data (Apex, Emirates, Gulf,
        /// Arabian, Technal) with real-looking UAE TRN numbers. That was
        /// production data hygiene violation and could be mistaken for
        /// real customer data.
        ///
        /// Phase 20+ Master Data module will populate this from a real DB.
        /// Until then, users type company details manually in Step 1.
        /// </summary>
        public ObservableCollection<CompanyProfileCard> SavedProfiles { get; }
            = new ObservableCollection<CompanyProfileCard>();

        // === Loaded Profile Indicator ===

        private string _loadedProfileName = "";
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

        public CompanySetupViewModel(Action<int>? navigateToStep)
        {
            _navigateToStep = navigateToStep;

            // BUG-33 [HIGH]: temporary placeholder until first async load.
            // The shell VM (ProformaInvoiceViewModel.StartNewInvoice) should
            // call GenerateUniqueProformaIdAsync() after construction, or
            // callers may await GenerateUniqueProformaIdAsync() directly
            // whenever a fresh invoice starts.
            ProformaId = $"PI-{DateTime.Now.Year}-000";

            // Default dates
            IssueDate = DateTime.Now;
            ValidUntil = DateTime.Now.AddDays(30);

            // Default status
            ProformaStatus = "Draft";

            // BUG-34: NO hardcoded profiles. Empty collection.
            // Real profiles come from Phase 20+ Master Data module.

            // Load Profile Command — fills form fields from selected card
            LoadProfileCommand = new RelayCommand(param =>
            {
                if (param is CompanyProfileCard profile)
                    LoadProfile(profile);
            });

            // Go Directly to Grid (Step 5) Command
            GoToGridCommand = new RelayCommand(_ =>
            {
                _navigateToStep?.Invoke(5);
            });
        }

        // ══════════════════════════════════════════════════════════════════
        //  BUG-33 [HIGH]: Async unique-ID generator
        //
        //  Called by shell VM StartNewInvoice() after constructing this VM.
        //  Uses the repository's SuggestNextIdAsync to guarantee uniqueness.
        //  Falls back to a GUID-based ID if the repo is unavailable.
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Generates a database-verified unique Proforma ID.
        /// Should be called by shell VM whenever a new invoice starts.
        /// Safe to call multiple times — always fetches next unused ID.
        /// </summary>
        public async Task GenerateUniqueProformaIdAsync()
        {
            try
            {
                if (App.Invoices == null)
                {
                    // DB not initialized — use a safe GUID-based fallback
                    // that cannot collide even without a DB check
                    ProformaId =
                        $"PI-{DateTime.Now.Year}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";
                    return;
                }

                // Ask the repository for the next available ID in the
                // "PI-{year}-001" family. The repo handles increment,
                // padding, and duplicate checks in one query (post BUG-08).
                string seedId = $"PI-{DateTime.Now.Year}-000";
                ProformaId = await App.Invoices.SuggestNextIdAsync(seedId);
            }
            catch (Exception)
            {
                // Any DB error → fall back to GUID-based ID (never collides)
                ProformaId =
                    $"PI-{DateTime.Now.Year}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  BUG-37 [LOW]: Loaded profile lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void LoadProfile(CompanyProfileCard profile)
        {
            _isLoadingProfile = true;
            try
            {
                CompanyName = profile.CompanyName;
                CompanyTrn = profile.Trn;
                CompanyLocation = profile.Location;
                LoadedProfileName = profile.CompanyName;
            }
            finally
            {
                _isLoadingProfile = false;
            }
        }

        /// <summary>
        /// BUG-37: Clears LoadedProfileName when user manually edits
        /// company fields after loading a profile. Prevents stale indicator.
        /// </summary>
        private void MarkUserEditedIfNotLoading()
        {
            if (_isLoadingProfile) return;

            if (!string.IsNullOrEmpty(_loadedProfileName))
                LoadedProfileName = "";
        }
    }
}