namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step3ClientProfile
{
    /// <summary>
    /// ViewModel for Step 3 of the Proforma Invoice wizard.
    /// Handles customer identity, billing details, and responsible team members.
    /// </summary>
    public class ClientProfileViewModel : ViewModelBase
    {
        // === Client Identity ===

        private string _customerName = string.Empty;
        public string CustomerName
        {
            get => _customerName;
            set { _customerName = value; OnPropertyChanged(); }
        }

        private string _customerTrn = string.Empty;
        public string CustomerTrn
        {
            get => _customerTrn;
            set { _customerTrn = value; OnPropertyChanged(); }
        }

        private string _customerReferenceCode = string.Empty;
        public string CustomerReferenceCode
        {
            get => _customerReferenceCode;
            set { _customerReferenceCode = value; OnPropertyChanged(); }
        }

        // === Team Members ===

        private string _salesRepresentative = string.Empty;
        public string SalesRepresentative
        {
            get => _salesRepresentative;
            set { _salesRepresentative = value; OnPropertyChanged(); }
        }

        private string _documentPreparedBy = string.Empty;
        public string DocumentPreparedBy
        {
            get => _documentPreparedBy;
            set { _documentPreparedBy = value; OnPropertyChanged(); }
        }

        // === Billing & Contact ===

        private string _billingAddress = string.Empty;
        public string BillingAddress
        {
            get => _billingAddress;
            set { _billingAddress = value; OnPropertyChanged(); }
        }

        private string _attentionExecutiveName = string.Empty;
        public string AttentionExecutiveName
        {
            get => _attentionExecutiveName;
            set { _attentionExecutiveName = value; OnPropertyChanged(); }
        }

        private string _clientContactPhone = string.Empty;
        public string ClientContactPhone
        {
            get => _clientContactPhone;
            set { _clientContactPhone = value; OnPropertyChanged(); }
        }

        public ClientProfileViewModel()
        {
            // Empty constructor — all fields default to empty strings
        }
    }
}