namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup
{
    /// <summary>
    /// ViewModel for Step 1 of the Proforma Invoice wizard.
    /// Handles company identity, TRN, dates, and fast-load profile selection.
    /// </summary>
    public class CompanySetupViewModel : ViewModelBase
    {
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

        public CompanySetupViewModel()
        {
            // Profile cards will be added in Sub-Phase 3.5
            // Proforma fields (ID, dates, status) will be added in Sub-Phase 3.4
        }
    }
}