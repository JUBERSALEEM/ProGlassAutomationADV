// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.PartyFields.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.PartyFields members.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.TaxInvoice.Attachment;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.TaxInvoice;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList;
using ProGlassAutomation.Views.TaxInvoice.TaxInvoiceDetail;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceDetail
{
    public partial class TaxInvoiceDetailViewModel : ViewModelBase
    {
        // ══════════════════════════════════════════════════════════════════
        //  COMPANY / CUSTOMER / PROJECT / BANK / PREPARED BY
        // ══════════════════════════════════════════════════════════════════

        private string _companyName = "";
        public string CompanyName
        { get => _companyName; set { _companyName = value; OnPropertyChanged(); } }

        private string _companyTrn = "";
        public string CompanyTrn
        {
            get => _companyTrn;
            set
            {
                _companyTrn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTrnValid));
            }
        }

        public bool IsTrnValid =>
            Regex.IsMatch(_companyTrn ?? "", @"^\d{15}$");

        private string _companyAddress = "";
        public string CompanyAddress
        { get => _companyAddress; set { _companyAddress = value; OnPropertyChanged(); } }

        private string _companyLocation = "";
        public string CompanyLocation
        { get => _companyLocation; set { _companyLocation = value; OnPropertyChanged(); } }

        private string _companyPhone = "";
        public string CompanyPhone
        { get => _companyPhone; set { _companyPhone = value; OnPropertyChanged(); } }

        private string _companyEmail = "";
        public string CompanyEmail
        { get => _companyEmail; set { _companyEmail = value; OnPropertyChanged(); } }

        private string _customerName = "";
        public string CustomerName
        { get => _customerName; set { _customerName = value; OnPropertyChanged(); } }

        private string _customerTrn = "";
        public string CustomerTrn
        { get => _customerTrn; set { _customerTrn = value; OnPropertyChanged(); } }

        private string _customerAddress = "";
        public string CustomerAddress
        { get => _customerAddress; set { _customerAddress = value; OnPropertyChanged(); } }

        private string _customerPhone = "";
        public string CustomerPhone
        { get => _customerPhone; set { _customerPhone = value; OnPropertyChanged(); } }

        private string _customerEmail = "";
        public string CustomerEmail
        { get => _customerEmail; set { _customerEmail = value; OnPropertyChanged(); } }

        private string _contactPerson = "";
        public string ContactPerson
        { get => _contactPerson; set { _contactPerson = value; OnPropertyChanged(); } }

        // ── Project (CHAT 20: aligned with Proforma Invoice naming) ──────
        //
        // Previously: ProjectName, ProjectLocation, ProjectReference (3 fields)
        // Now:        ProjectIdentifierName, ProjectContractNo, ProjectReference,
        //             SiteDeliveryLocation (4 fields)
        //
        // Matches PI ProjectSnapshot exactly — no data loss on PI → TI import.

        private string _projectIdentifierName = "";
        /// <summary>
        /// Short project designation (e.g. "IMAD HABIB").
        /// Matches PI ProjectIdentifierName.
        /// </summary>
        public string ProjectIdentifierName
        { get => _projectIdentifierName; set { _projectIdentifierName = value; OnPropertyChanged(); } }

        private string _projectContractNo = "";
        /// <summary>
        /// Project number with name (e.g. "428 IMAD HABIB").
        /// Matches PI ProjectContractNo.
        /// </summary>
        public string ProjectContractNo
        { get => _projectContractNo; set { _projectContractNo = value; OnPropertyChanged(); } }

        private string _projectReference = "";
        /// <summary>
        /// Full descriptive reference (e.g. "HOUSE FOR IMAD HABIB").
        /// Matches PI ProjectReference.
        /// </summary>
        public string ProjectReference
        { get => _projectReference; set { _projectReference = value; OnPropertyChanged(); } }

        private string _siteDeliveryLocation = "";
        /// <summary>
        /// Site or delivery address (e.g. "AJMAN").
        /// Matches PI SiteDeliveryLocation.
        /// </summary>
        public string SiteDeliveryLocation
        { get => _siteDeliveryLocation; set { _siteDeliveryLocation = value; OnPropertyChanged(); } }

        private string _bankName = "";
        public string BankName
        { get => _bankName; set { _bankName = value; OnPropertyChanged(); } }

        private string _bankAccountNumber = "";
        public string BankAccountNumber
        { get => _bankAccountNumber; set { _bankAccountNumber = value; OnPropertyChanged(); } }

        private string _iban = "";
        public string Iban
        { get => _iban; set { _iban = value; OnPropertyChanged(); } }

        private string _swiftCode = "";
        public string SwiftCode
        { get => _swiftCode; set { _swiftCode = value; OnPropertyChanged(); } }

        private string _paymentNotes = "";
        public string PaymentNotes
        { get => _paymentNotes; set { _paymentNotes = value; OnPropertyChanged(); } }

        private string _preparedByName = "";
        public string PreparedByName
        { get => _preparedByName; set { _preparedByName = value; OnPropertyChanged(); } }

        private string _preparedByTitle = "";
        public string PreparedByTitle
        { get => _preparedByTitle; set { _preparedByTitle = value; OnPropertyChanged(); } }

        private string _preparedByDepartment = "";
        public string PreparedByDepartment
        { get => _preparedByDepartment; set { _preparedByDepartment = value; OnPropertyChanged(); } }

    }
}
