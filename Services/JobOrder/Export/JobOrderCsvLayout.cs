using CsvHelper;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using System;
using System.Linq;

namespace ProGlassAutomation.Services.JobOrder.Export
{
    /// <summary>
    /// CSV layout for a Job Order — flat cut list only per Round 3
    /// design decision Q3. No header sections, no notes — just one
    /// row per glass item with all identifying context columns so
    /// the file is machine-parseable.
    ///
    /// Header row includes the Overdue flag column per Q2.
    /// </summary>
    public class JobOrderCsvLayout : ICsvLayout
    {
        private readonly SavedJobOrder _entity;
        private readonly JobOrderSnapshot _snapshot;
        private readonly ProformaInvoiceSnapshot _invoice;

        public JobOrderCsvLayout(SavedJobOrder entity, JobOrderSnapshot snapshot)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _invoice = snapshot.EmbeddedInvoice ?? new ProformaInvoiceSnapshot();
        }

        // ── IDocumentLayout ──────────────────────────────────────────
        public string DocumentPrefix => "JO";
        public string DocumentId => _entity.JobOrderId ?? _snapshot.JobOrderId;
        public string DocumentTitle => "Job Order — Cut List";
        public string CompanyName => _invoice.Client?.CustomerName ?? string.Empty;
        public string Reference => ResolveReference();
        public DateTime DocumentDate => _snapshot.ConvertedAt != default
                                           ? _snapshot.ConvertedAt
                                           : _entity.CreatedAt;

        public void WriteRows(CsvWriter writer)
        {
            // Header row
            writer.WriteField("JobOrderId");
            writer.WriteField("SourceProformaId");
            writer.WriteField("Customer");
            writer.WriteField("Project");
            writer.WriteField("Status");
            writer.WriteField("Priority");
            writer.WriteField("DeliveryDate");
            writer.WriteField("Overdue");
            writer.WriteField("SpecNumber");
            writer.WriteField("SpecName");
            writer.WriteField("GlassType");
            writer.WriteField("RowNumber");
            writer.WriteField("GlassRef");
            writer.WriteField("W1");
            writer.WriteField("H1");
            writer.WriteField("W2");
            writer.WriteField("H2");
            writer.WriteField("Qty");
            writer.NextRecord();

            string jobId = _entity.JobOrderId ?? "";
            string sourcePi = _snapshot.SourceProformaId ?? "";
            string customer = _invoice.Client?.CustomerName ?? "";
            string project = _invoice.Project?.ProjectIdentifierName ?? "";
            string status = _snapshot.Production?.Status ?? "Draft";
            string priority = _snapshot.Production?.Priority ?? "Medium";
            string deliveryStr = _snapshot.Production?.DeliveryDate.HasValue == true
                                        ? _snapshot.Production.DeliveryDate.Value.ToString("yyyy-MM-dd")
                                        : "";
            string overdueFlag = IsOverdue() ? "YES" : "NO";

            if (_invoice.SpecGroups == null || _invoice.SpecGroups.Count == 0)
                return;

            foreach (var group in _invoice.SpecGroups.OrderBy(g => g.GroupNumber))
            {
                if (group.ItemRows == null) continue;

                foreach (var row in group.ItemRows)
                {
                    writer.WriteField(jobId);
                    writer.WriteField(sourcePi);
                    writer.WriteField(customer);
                    writer.WriteField(project);
                    writer.WriteField(status);
                    writer.WriteField(priority);
                    writer.WriteField(deliveryStr);
                    writer.WriteField(overdueFlag);
                    writer.WriteField(group.GroupNumber);
                    writer.WriteField(group.SelectedSpecName ?? "");
                    writer.WriteField(group.SelectedGlassType ?? "");
                    writer.WriteField(row.SerialNumber);
                    writer.WriteField(row.GlassRef ?? "");
                    writer.WriteField(row.W1);
                    writer.WriteField(row.H1);
                    writer.WriteField(row.W2);
                    writer.WriteField(row.H2);
                    writer.WriteField(row.Qty);
                    writer.NextRecord();
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────

        private bool IsOverdue()
        {
            var status = _snapshot.Production?.Status ?? "";
            var delivery = _snapshot.Production?.DeliveryDate;
            if (!delivery.HasValue) return false;
            if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(status, "Delivered", StringComparison.OrdinalIgnoreCase)) return false;
            return delivery.Value.Date < DateTime.Today;
        }

        private string ResolveReference()
        {
            var proj = _invoice.Project;
            if (proj == null) return string.Empty;

            if (!string.IsNullOrWhiteSpace(proj.ProjectIdentifierName))
                return proj.ProjectIdentifierName;
            if (!string.IsNullOrWhiteSpace(proj.ProjectContractNo))
                return proj.ProjectContractNo;
            if (!string.IsNullOrWhiteSpace(proj.LpoNumber))
                return proj.LpoNumber;

            return string.Empty;
        }
    }
}