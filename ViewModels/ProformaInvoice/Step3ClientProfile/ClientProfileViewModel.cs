using System.Linq;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step3ClientProfile
{
    /// <summary>
    /// ViewModel for Step 3 of the Proforma Invoice wizard.
    /// Handles customer identity, billing details, and responsible team members.
    ///
    /// BUG-38 [MEDIUM]: UAE TRN 15-digit validation added for CustomerTrn.
    ///                  Exposed via CustomerTrnValidationMessage and
    ///                  IsCustomerTrnValid properties. UAE Federal Tax
    ///                  Authority requires TRN to be exactly 15 digits.
    /// BUG-39: Pure data holder — no bugs in logic layer. Only validation
    ///         and null-safety improvements applied.
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
            set
            {
                if (_customerTrn == value) return;
                _customerTrn = value;
                OnPropertyChanged();
                // BUG-38: raise validation change notifications
                OnPropertyChanged(nameof(CustomerTrnValidationMessage));
                OnPropertyChanged(nameof(IsCustomerTrnValid));
            }
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

        // ══════════════════════════════════════════════════════════════════
        //  BUG-38 [MEDIUM]: UAE TRN Validation
        //
        //  UAE Federal Tax Authority TRN format:
        //    - Exactly 15 digits
        //    - No spaces, no letters, no punctuation
        //
        //  Same rules as CompanySetupViewModel — kept parallel intentionally
        //  so both company and client TRNs get identical treatment.
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns null when CustomerTrn is valid, or an error message
        /// when invalid. Empty TRN is treated as "not yet entered".
        /// </summary>
        public string? CustomerTrnValidationMessage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_customerTrn))
                    return null;   // no error while user hasn't typed yet

                var digitsOnly = new string(_customerTrn.Where(char.IsDigit).ToArray());

                if (digitsOnly.Length != 15)
                    return "Customer TRN must be exactly 15 digits.";

                if (digitsOnly != _customerTrn.Trim())
                    return "TRN should contain only digits (no spaces or letters).";

                return null;   // valid
            }
        }

        /// <summary>
        /// True when TRN is either empty or a valid 15-digit UAE TRN.
        /// XAML uses this to enable/disable Continue button.
        /// </summary>
        public bool IsCustomerTrnValid => CustomerTrnValidationMessage == null;

        public ClientProfileViewModel()
        {
            // Empty constructor — all fields default to empty strings
        }
    }
}