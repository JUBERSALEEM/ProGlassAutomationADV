// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.PrivateHelpers.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.PrivateHelpers members.
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
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════════════

        private bool ValidateBeforeSave()
        {
            if (string.IsNullOrWhiteSpace(ProformaId))
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.ProformaInvoiceReferenceIsRequired"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(CustomerName))
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.CustomerNameIsRequired"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!IsTrnValid)
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.CompanyTRNMustBeExactly"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (DueDate < IssueDate)
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.DueDateCannotBeEarlier"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (SpecGroups.Count == 0 || SpecGroups.All(g => g.Rows.Count == 0))
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.LeastOneGlassItemRow"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (SpecGroups.SelectMany(g => g.Rows).Any(r => r.Qty <= 0))
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.InvoiceRowsMustHaveQty"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (GrandTotal <= 0m)
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.GrandTotalMustBeGreater"),
                    LocalizationService.Instance.T("Msg.CapValidation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private InvWizardState BuildWizardState() => new()
        {
            ProformaId = ProformaId,
            DeliveryNoteReference = DeliveryNoteReference,
            JobOrderId = JobOrderId,
            PurchaseOrderNumber = PurchaseOrderNumber,
            Status = Status,
            IssueDate = IssueDate,
            PaymentTerms = PaymentTerms,
            PaymentTermsDays = PaymentTermsDays,
            CompanyName = CompanyName,
            CompanyTrn = CompanyTrn,
            CompanyAddress = CompanyAddress,
            CompanyLocation = CompanyLocation,
            CompanyPhone = CompanyPhone,
            CompanyEmail = CompanyEmail,
            LogoBase64 = LogoBase64ForSave,
            LogoPath = LogoPath,
            LogoLayout = LogoLayout,
            LogoWidthMm = LogoWidthMm,
            LogoHeightMm = LogoHeightMm,
            CustomerName = CustomerName,
            CustomerTrn = CustomerTrn,
            CustomerAddress = CustomerAddress,
            CustomerPhone = CustomerPhone,
            CustomerEmail = CustomerEmail,
            ContactPerson = ContactPerson,
            // CHAT 20: 4-field project section aligned with PI.
            ProjectIdentifierName = ProjectIdentifierName,
            ProjectContractNo = ProjectContractNo,
            ProjectReference = ProjectReference,
            SiteDeliveryLocation = SiteDeliveryLocation,
            DiscountPercent = DiscountPercent,
            ProformaTotal = ProformaTotal,
            PreviouslyInvoicedTotal = PreviouslyInvoicedTotal,
            BankName = BankName,
            BankAccountNumber = BankAccountNumber,
            Iban = Iban,
            SwiftCode = SwiftCode,
            PaymentNotes = PaymentNotes,
            PreparedByName = PreparedByName,
            PreparedByTitle = PreparedByTitle,
            PreparedByDepartment = PreparedByDepartment,
            PreparedBySignatureBase64 = "",
            SpecGroups = SpecGroups.Select(g => new InvSpecGroupState
            {
                GroupNumber = g.GroupNumber,
                GroupName = g.GroupName,
                GlassType = g.GlassType,
                SpecName = g.SpecName,
                ProformaId = ProformaId,
                BaseRate = g.BaseRate,
                Rows = g.Rows.Select(r => new InvRowState
                {
                    SerialNumber = r.SerialNumber,
                    GlassRef = r.GlassRef,
                    W1 = r.W1,
                    H1 = r.H1,
                    W2 = r.W2,
                    H2 = r.H2,
                    Qty = r.Qty,
                    Rate = r.Rate,
                    Notes = r.Notes
                }).ToList()
            }).ToList(),
            AttachmentSnapshots = Attachments.Select(a => new InvAttachmentSnapshot
            {
                AttachmentType = a.AttachmentType,
                FileName = a.FileName,
                FileBase64 = a.FileBytes != null
                    ? Convert.ToBase64String(a.FileBytes) : "",
                FileSize = a.FileSize,
                Description = a.Description,
                UploadedAt = a.UploadedAt
            }).ToList()
        };

        private void RestoreFromSnapshot(
    SavedTaxInvoice entity, TaxInvoiceSnapshot snap)
        {
            _proformaId = snap.Header.ProformaId;
            _deliveryNoteReference = snap.Header.DeliveryNoteReference;
            _jobOrderId = snap.Header.JobOrderId;
            _purchaseOrderNumber = snap.Header.PurchaseOrderNumber;
            _status = snap.Header.Status;
            _issueDate = snap.Header.IssueDate == default
                ? DateTime.Today : snap.Header.IssueDate;
            _dueDate = snap.Header.DueDate == default
                ? _issueDate.AddDays(30) : snap.Header.DueDate;

            _companyName = snap.Company.CompanyName;
            _companyTrn = snap.Company.CompanyTrn;
            _companyAddress = snap.Company.CompanyAddress;
            _companyLocation = snap.Company.CompanyLocation;
            _companyPhone = snap.Company.CompanyPhone;
            _companyEmail = snap.Company.CompanyEmail;
            _logoPath = snap.Company.LogoPath ?? "";
            LogoBase64ForSave = snap.Company.LogoBase64 ?? "";
            _logoLayout = snap.Company.LogoLayout;
            _logoWidthMm = snap.Company.LogoWidthMm > 0 ? snap.Company.LogoWidthMm : 22;
            _logoHeightMm = snap.Company.LogoHeightMm > 0 ? snap.Company.LogoHeightMm : 26;

            _customerName = snap.Customer.CustomerName;
            _customerTrn = snap.Customer.CustomerTrn;
            _customerAddress = snap.Customer.CustomerAddress;
            _customerPhone = snap.Customer.CustomerPhone;
            _customerEmail = snap.Customer.CustomerEmail;
            _contactPerson = snap.Customer.ContactPerson;

            // CHAT 20: Restore new fields with legacy migration.
            // New saved invoices have ProjectIdentifierName populated.
            // Old saved invoices have only ProjectName (legacy field).
            // If new field is empty, fall back to legacy value — no data loss.
#pragma warning disable CS0612 // Suppress [Obsolete] warnings for migration reads
            _projectIdentifierName = !string.IsNullOrWhiteSpace(snap.Project.ProjectIdentifierName)
                ? snap.Project.ProjectIdentifierName
                : snap.Project.ProjectName;     // ← legacy fallback

            _projectContractNo = snap.Project.ProjectContractNo;
            _projectReference = snap.Project.ProjectReference;

            _siteDeliveryLocation = !string.IsNullOrWhiteSpace(snap.Project.SiteDeliveryLocation)
                ? snap.Project.SiteDeliveryLocation
                : snap.Project.ProjectLocation;  // ← legacy fallback
#pragma warning restore CS0612

            _bankName = snap.Bank.BankName;
            _bankAccountNumber = snap.Bank.BankAccountNumber;
            _iban = snap.Bank.Iban;
            _swiftCode = snap.Bank.SwiftCode;
            _paymentTerms = snap.Bank.PaymentTerms;
            _paymentTermsDays = snap.Bank.PaymentTermsDays;
            _paymentNotes = snap.Bank.PaymentNotes;

            _preparedByName = snap.PreparedBy.Name;
            _preparedByTitle = snap.PreparedBy.Title;
            _preparedByDepartment = snap.PreparedBy.Department;

            _discountPercent = snap.Financial.DiscountPercent;
            _previouslyInvoicedTotal = snap.Financial.PreviouslyInvoicedTotal;

            _specGroups.Clear();
            foreach (var g in snap.SpecGroups)
            {
                var groupVm = new InvSpecGroupViewModel(g.GroupNumber, _proformaId)
                {
                    GroupName = g.GroupName,
                    GlassType = g.GlassType,
                    SpecName = g.SpecName,
                    BaseRate = g.BaseRate,
                    OnRowChanged = RecalculateFinancials
                };

                foreach (var r in g.Rows)
                {
                    groupVm.Rows.Add(new InvRowViewModel(groupVm)
                    {
                        SerialNumber = r.SerialNumber,
                        GlassRef = r.GlassRef,
                        W1 = r.W1,
                        H1 = r.H1,
                        W2 = r.W2,
                        H2 = r.H2,
                        Qty = r.Qty,
                        Rate = r.Rate,
                        Notes = r.Notes
                    });
                }

                groupVm.RecalculateGroup();
                _specGroups.Add(groupVm);
            }

            _attachments.Clear();
            foreach (var a in entity.Attachments)
            {
                _attachments.Add(new InvAttachmentViewModel
                {
                    Id = a.Id,
                    AttachmentType = a.AttachmentType,
                    FileName = a.FileName,
                    MimeType = a.MimeType,
                    FileBytes = a.FileBytes,
                    FileSize = a.FileSize,
                    Description = a.Description,
                    UploadedAt = a.UploadedAt
                });
            }

            decimal subtotal = 0m;
            foreach (var group in _specGroups)
                subtotal += group.GroupTotalValue;

            decimal discountAmt = Math.Round(subtotal * (_discountPercent / 100m), 2);
            decimal afterDiscount = subtotal - discountAmt;
            // Was a bare 0.05m. VatCalculator is the single declaration of
            // the UAE rate; duplicating it here meant the tax invoice detail
            // screen could drift from every other VAT calculation.
            decimal vatAmount = Math.Round(
                afterDiscount * ProGlassAutomation.Services.Reporting.VatCalculator.VAT_RATE_DECIMAL, 2);

            _subtotal = Math.Round(subtotal, 2);
            _discountAmount = discountAmt;
            _afterDiscount = afterDiscount;
            _vatAmount = vatAmount;
            _grandTotal = afterDiscount + vatAmount;

            OnPropertyChanged(string.Empty);
        }

        private void ResetFields()
        {
            ProformaId = ""; DeliveryNoteReference = "";
            JobOrderId = ""; PurchaseOrderNumber = "";
            Status = "Draft";
            IssueDate = DateTime.Today;
            PaymentTerms = "Net 30"; PaymentTermsDays = 30;
            DueDate = DateTime.Today.AddDays(30);
            CompanyName = ""; CompanyTrn = ""; CompanyAddress = "";
            CompanyLocation = ""; CompanyPhone = ""; CompanyEmail = "";
            LogoPath = ""; LogoBase64ForSave = "";
            LogoLayout = Models.MasterData.CompanyLogoLayout.None;
            LogoWidthMm = 22; LogoHeightMm = 26;
            CustomerName = ""; CustomerTrn = ""; CustomerAddress = "";
            CustomerPhone = ""; CustomerEmail = ""; ContactPerson = "";
            // CHAT 20: 4-field project reset
            ProjectIdentifierName = ""; ProjectContractNo = "";
            ProjectReference = ""; SiteDeliveryLocation = "";
            BankName = ""; BankAccountNumber = ""; Iban = "";
            SwiftCode = ""; PaymentNotes = "";
            PreparedByName = ""; PreparedByTitle = ""; PreparedByDepartment = "";
            _discountPercent = 0m;
            OnPropertyChanged(nameof(DiscountPercent));
            SpecGroups.Clear();
            Attachments.Clear();
            FamilyMembers = new ObservableCollection<TaxInvoiceRowViewModel>();
            FamilyProformaTotal = 0m;
            FamilyInvoicedTotal = 0m;
            FamilyBalanceTotal = 0m;
            RecalculateFinancials();
        }

        /// <summary>
        /// Opens the Issue All popup for every Draft in this PI family.
        /// Sibling drafts go through UpdateStatusAsync (snapshot header
        /// patched in the repository). The open invoice, if selected,
        /// uses ChangeStatusAndSaveAsync so TRN / line validation still runs.
        /// </summary>
        private async Task IssueSelectedDraftsAsync()
        {
            var drafts = FamilyMembers.Where(m => m.IsDraft).ToList();
            if (drafts.Count == 0) return;

            var selected = IssueDraftsDialog.Show(
                Application.Current?.MainWindow, drafts);
            if (selected == null || selected.Count == 0) return;

            if (_taxInvoices == null)
            {
                StatusMessage = "Database not initialized.";
                return;
            }

            try
            {
                IsSaving = true;
                var others = selected
                    .Where(m => !string.Equals(
                        m.InvoiceNumber, _editingInvoiceNumber,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                bool includeCurrent = selected.Any(m => string.Equals(
                    m.InvoiceNumber, _editingInvoiceNumber,
                    StringComparison.OrdinalIgnoreCase));

                foreach (var member in others)
                {
                    await _taxInvoices.UpdateStatusAsync(
                        member.InvoiceNumber, "Issued");
                    AuditService.Log(
                        AuditSections.TaxInvoice, "Tax invoice issued",
                        $"{member.InvoiceNumber} — Issue all",
                        "TI", member.InvoiceNumber);
                }

                int issued = others.Count;
                IsSaving = false;

                if (includeCurrent && _status == "Draft")
                {
                    await ChangeStatusAndSaveAsync("Issued");
                    if (_status == "Issued")
                    {
                        issued++;
                        StatusMessage = $"Issued {issued} tax invoice(s).";
                    }
                    else if (others.Count > 0)
                    {
                        StatusMessage =
                            $"Issued {others.Count} sibling invoice(s). " +
                            $"This invoice was not issued. {StatusMessage}";
                    }
                }
                else
                {
                    await LoadFamilyAsync(ProformaId);
                    if (issued > 0)
                        StatusMessage = $"Issued {issued} tax invoice(s).";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Issue all failed: {ExceptionMessages.Deep(ex)}";
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.CouldNotIssueInvoicesX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapIssueAllFailed"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        private async Task ChangeStatusAndSaveAsync(string newStatus)
        {
            // Phase 3: enforce centralized status-transition rules at the
            // business boundary.
            var transition = ProGlassAutomation.Services.Workflow.StatusTransitions
                .Validate(
                    ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.TaxInvoice,
                    _status, newStatus);
            if (!transition.IsValid)
            {
                StatusMessage = transition.Reason ?? "Invalid status change.";
                return;
            }

            Status = newStatus;
            await SaveAsync();
        }
    }
}
