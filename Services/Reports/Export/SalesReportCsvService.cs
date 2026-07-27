using CsvHelper;
using ProGlassAutomation.Models.Reports.Sales;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Reports.Export
{
    public class SalesReportCsvService : ICsvLayout
    {
        private readonly List<SalesReport> _records;
        private readonly SalesFilterModel? _filter;

        public SalesReportCsvService(List<SalesReport> records, SalesFilterModel? filter = null)
        {
            _records = records ?? throw new ArgumentNullException(nameof(records));
            _filter = filter;
        }

        public string DocumentPrefix => "STMT";
        public string DocumentId => DateTime.Now.ToString("yyyyMMdd-HHmm");
        public string DocumentTitle => "SALES TRANSACTION HISTORY";

        public string CompanyName => _filter?.CustomerName ?? "All Customers";
        public string Reference => _filter?.ProjectName ?? "All Projects";
        public DateTime DocumentDate => DateTime.Today;

        public void WriteRows(CsvWriter writer)
        {
            // Write Header
            writer.WriteField("Index");
            writer.WriteField("Invoice Date");
            writer.WriteField("Invoice Number");
            writer.WriteField("Customer Name");
            writer.WriteField("Company Name");
            writer.WriteField("Project Name");
            writer.WriteField("Salesperson");
            writer.WriteField("Subtotal (AED)");
            writer.WriteField("Discount (AED)");
            writer.WriteField("Taxable Amount (AED)");
            writer.WriteField("VAT (5% AED)");
            writer.WriteField("Grand Total (AED)");
            writer.WriteField("Payment Status");
            writer.WriteField("Proforma Reference");
            writer.WriteField("Job Order Reference");
            writer.WriteField("Delivery Note Reference");
            writer.NextRecord();

            // Write Data Row
            int index = 1;
            foreach (var r in _records)
            {
                writer.WriteField(index);
                writer.WriteField(r.InvoiceDate.ToString("yyyy-MM-dd"));
                writer.WriteField(r.InvoiceNumber);
                writer.WriteField(r.CustomerName);
                writer.WriteField(r.CompanyName);
                writer.WriteField(r.ProjectName);
                writer.WriteField(r.Salesman);
                writer.WriteField((r.Subtotal * r.ExchangeRate).ToString("0.00"));
                writer.WriteField((r.Discount * r.ExchangeRate).ToString("0.00"));
                writer.WriteField(r.TaxableAmountAed.ToString("0.00"));
                writer.WriteField(r.VatAmountAed.ToString("0.00"));
                writer.WriteField(r.GrandTotalAed.ToString("0.00"));
                writer.WriteField(r.PaymentStatus);
                writer.WriteField(r.ProformaId);
                writer.WriteField(r.JobOrderId);
                writer.WriteField(r.DeliveryNoteId);
                writer.NextRecord();

                index++;
            }
        }
    }
}