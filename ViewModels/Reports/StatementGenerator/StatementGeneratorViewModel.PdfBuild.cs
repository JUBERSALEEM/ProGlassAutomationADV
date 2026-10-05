// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.PdfBuild.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.3 — StatementGeneratorViewModel partial split. This part owns the
// PdfBuild members; fields + constructor live in StatementGeneratorViewModel.cs.
// ═══════════════════════════════════════════════════════════════════════

using PdfSharp.Pdf;
using ProGlassAutomation.Services.Logging;
using PdfSharp.Pdf.IO;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.Reports.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Database;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public partial class StatementGeneratorViewModel : ViewModelBase
    {
        // ═════════════════════════════════════════════════════════════════
        //  BUILD PDF SECTIONS
        // ═════════════════════════════════════════════════════════════════

        private void BuildPdfSections(WorkflowResult wf)
        {
            MergedPdfSections.Clear();
            BalancePdfSections.Clear();
            AlertsPdfSections.Clear();

            if (wf.Snapshot == null) return;

            MergedPdfSections.Add(new PdfSection
            { Icon = "\uE9F7", IconBrushKey = "TintIndigoBrush", Title = "Statement Cover Page", PageInfo = "1 page" });

            MergedPdfSections.Add(new PdfSection
            { Icon = "\uE8C7", IconBrushKey = "TintPurpleBrush", Title = "Financial Summary", PageInfo = "1 page" });

            int invoiceCount = wf.Snapshot.InvoiceRows.Count;
            if (invoiceCount > 0)
            {
                int detailPages = Math.Max(1, (invoiceCount + 9) / 10);
                MergedPdfSections.Add(new PdfSection
                {
                    Icon = "\uE9F9",
                    IconBrushKey = "TintBlueBrush",
                    Title = "Invoice Detail Table",
                    PageInfo = $"{detailPages:N0} page{(detailPages == 1 ? "" : "s")}"
                });
            }

            if (_includeAgingChart && wf.Snapshot.Aging.Total > 0)
                MergedPdfSections.Add(new PdfSection
                { Icon = "\uE9D9", IconBrushKey = "TintBlueBrush", Title = "Aging Analysis Chart", PageInfo = "1 page" });

            if (_includeBalanceAnalysis)
                MergedPdfSections.Add(new PdfSection
                { Icon = "\uE9D2", IconBrushKey = "TintGreenBrush", Title = "Balance Comparison", PageInfo = "1 page" });

            if (wf.SignedProformaInvoiceBytes != null && wf.SignedProformaInvoiceBytes.Length > 0)
            {
                string piName = string.IsNullOrWhiteSpace(wf.SignedProformaInvoiceFileName)
                    ? "Signed_Proforma_Invoice.pdf"
                    : wf.SignedProformaInvoiceFileName;

                MergedPdfSections.Add(new PdfSection
                { Icon = "\uE723", IconBrushKey = "TintIndigoBrush", Title = piName, PageInfo = "1 page" });
            }

            foreach (var invPdf in wf.TaxInvoicePdfs)
            {
                if (invPdf.IsValid)
                {
                    MergedPdfSections.Add(new PdfSection
                    {
                        Icon = "\uE8A5",
                        IconBrushKey = "TintIndigoBrush",
                        Title = $"{invPdf.InvoiceNumber}.pdf",
                        PageInfo = "Auto",
                        Status = PdfSectionStatus.Normal
                    });
                }
                else
                {
                    MergedPdfSections.Add(new PdfSection
                    {
                        Icon = "\uE7BA",
                        IconBrushKey = "TintRedBrush",
                        Title = $"{invPdf.InvoiceNumber}.pdf",
                        PageInfo = "1 page",
                        Status = PdfSectionStatus.Placeholder
                    });
                }

                foreach (var (_, fileName) in invPdf.Attachments)
                {
                    bool isImage = StatementLogHelper.IsImageFileName(fileName);
                    MergedPdfSections.Add(new PdfSection
                    {
                        Icon = "\uE723",
                        IconBrushKey = "TintIndigoBrush",
                        Title = fileName,
                        PageInfo = "1 page",
                        Status = isImage ? PdfSectionStatus.Converted : PdfSectionStatus.Normal
                    });
                }
            }

            foreach (var dn in wf.SignedDeliveryNotes)
            {
                for (int i = 0; i < dn.AttachmentFileNames.Count; i++)
                {
                    string fileName = dn.AttachmentFileNames[i];
                    bool isImage = StatementLogHelper.IsImageFileName(fileName);

                    MergedPdfSections.Add(new PdfSection
                    {
                        Icon = "\uE7F4",
                        IconBrushKey = "TintPurpleBrush",
                        Title = fileName,
                        PageInfo = "1 page",
                        Status = isImage ? PdfSectionStatus.Converted : PdfSectionStatus.Normal
                    });
                }
            }

            BalancePdfSections.Add(new PdfSection
            { Icon = "\uE8C7", IconBrushKey = "TintPurpleBrush", Title = "Financial Summary", PageInfo = "1 page" });

            if (wf.Snapshot.Aging.Total > 0)
                BalancePdfSections.Add(new PdfSection
                { Icon = "\uE9D9", IconBrushKey = "TintBlueBrush", Title = "Aging Analysis Chart", PageInfo = "1 page" });

            BalancePdfSections.Add(new PdfSection
            { Icon = "\uE9D2", IconBrushKey = "TintGreenBrush", Title = "Balance Comparison", PageInfo = "1 page" });

            BalancePdfSections.Add(new PdfSection
            { Icon = "\uE9F9", IconBrushKey = "TintBlueBrush", Title = "Outstanding Breakdown", PageInfo = "1 page" });

            BalancePdfSections.Add(new PdfSection
            { Icon = "\uE8EF", IconBrushKey = "TintGreenBrush", Title = "Payment History", PageInfo = "1 page" });

            if (WorkflowWarnings.Count > 0)
                AlertsPdfSections.Add(new PdfSection
                {
                    Icon = "\uE7BA",
                    IconBrushKey = "TintRedBrush",
                    Title = "Warnings Summary",
                    PageInfo = $"{Math.Max(1, (WorkflowWarnings.Count + 4) / 5)} page(s)",
                    Status = PdfSectionStatus.Warning
                });

            if (ErrorCount > 0)
                AlertsPdfSections.Add(new PdfSection
                {
                    Icon = "\uE7BA",
                    IconBrushKey = "TintRedBrush",
                    Title = "Overdue Invoices",
                    PageInfo = $"{ErrorCount:N0} entries",
                    Status = PdfSectionStatus.Warning
                });

            if (wf.UninvoicedDeliveryNotes.Count > 0)
                AlertsPdfSections.Add(new PdfSection
                {
                    Icon = "\uE7BA",
                    IconBrushKey = "TintRedBrush",
                    Title = "Uninvoiced Deliveries",
                    PageInfo = $"{wf.UninvoicedDeliveryNotes.Count:N0} entries",
                    Status = PdfSectionStatus.Warning
                });

            AlertsPdfSections.Add(new PdfSection
            {
                Icon = "\uE8C8",
                IconBrushKey = "TintIndigoBrush",
                Title = "Workflow Audit Trail",
                PageInfo = $"{wf.Steps.Count:N0} steps"
            });

            OnPropertyChanged(nameof(MergedSectionCount));
            OnPropertyChanged(nameof(BalanceSectionCount));
            OnPropertyChanged(nameof(AlertsSectionCount));
        }

        // ═════════════════════════════════════════════════════════════════
        //  PDF ACTIONS
        // ═════════════════════════════════════════════════════════════════

        private void OpenLastPdf()
        {
            if (!HasLastPdf) return;
            try { StatementPdfExporter.OpenPdf(_lastPdfPath); }
            catch (Exception ex) { StatusMessage = $"Could not open PDF: {ExceptionMessages.Deep(ex)}"; }
        }

        private void CopyLastPdfPathToClipboard()
        {
            if (!HasLastPdf) return;
            try
            {
                Clipboard.SetText(_lastPdfPath);
                StatusMessage = "PDF path copied to clipboard.";
            }
            catch (Exception ex) { StatusMessage = $"Could not copy path: {ExceptionMessages.Deep(ex)}"; }
        }

        // ── Sharing Center (Pattern B: the generated PDF already exists) ──
        private void ShareLastPdf()
        {
            if (!HasLastPdf) return;

            var context = new ProGlassAutomation.Services.Sharing.ShareContext
            {
                DocumentTypeKey = "Statement",
                Title = "Statement of Account",
                CustomerName = FilterCustomer ?? "",
                CompanyName = FilterCompany ?? "",
                SalesmanName = FilterSalesman ?? "",
                SourceModule = "Statement"
            };

            if (ProGlassAutomation.Services.Sharing.ShareLauncher
                    .AttachFile(context, _lastPdfPath, _dialogs))
                ProGlassAutomation.Services.Sharing.ShareLauncher
                    .OpenComposer(context, _dialogs);
        }

    }
}
