using System;
using System.Collections.Generic;

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

        public CompanySetupViewModel()
        {
            // Auto-generate proforma ID (PI-YEAR-001 format)
            ProformaId = $"PI-{DateTime.Now.Year}-001";

            // Default dates
            IssueDate = DateTime.Now;
            ValidUntil = DateTime.Now.AddDays(30);

            // Default status
            ProformaStatus = "Draft";

            // Profile cards will be added in Sub-Phase 3.5
        }
    }
}