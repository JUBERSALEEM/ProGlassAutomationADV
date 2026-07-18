namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step2ProjectSpecs
{
    /// <summary>
    /// ViewModel for Step 2 of the Proforma Invoice wizard.
    /// Handles project identification, contract numbers, delivery location, and LPO.
    /// </summary>
    public class ProjectSpecsViewModel : ViewModelBase
    {
        // === Project Fields ===

        private string _projectIdentifierName = string.Empty;
        public string ProjectIdentifierName
        {
            get => _projectIdentifierName;
            set { _projectIdentifierName = value; OnPropertyChanged(); }
        }

        private string _projectContractNo = string.Empty;
        public string ProjectContractNo
        {
            get => _projectContractNo;
            set { _projectContractNo = value; OnPropertyChanged(); }
        }

        private string _siteDeliveryLocation = string.Empty;
        public string SiteDeliveryLocation
        {
            get => _siteDeliveryLocation;
            set { _siteDeliveryLocation = value; OnPropertyChanged(); }
        }

        private string _lpoNumber = string.Empty;
        public string LpoNumber
        {
            get => _lpoNumber;
            set { _lpoNumber = value; OnPropertyChanged(); }
        }

        public ProjectSpecsViewModel()
        {
            // Empty constructor — fields default to empty strings
        }
    }
}