using CsvHelper;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Export.Csv;
using System;
using System.Linq;

namespace ProGlassAutomation.Services.ProformaInvoice.Export
{
    /// <summary>
    /// Phase 9.5b — ICsvLayout implementation for Proforma Invoice.
    /// Replaces InvoiceCsvExportService via shared CsvExportEngine.
    ///
    /// 13-column CSV:
    ///   Spec # | Spec Name | Row # | Glass Ref | W1 | H1 | W2 | H2 |
    ///   Qty | Area (sqm) | Total Area (sqm) | Rate (AED) | Total (AED)
    ///
    /// Followed by blank row + 3 summary rows
    /// (11 empty fields + label + value = 13 fields per row).
    /// UTF-8 BOM handled automatically by CsvExportEngine.
    ///
    /// STRANGLER PATTERN Round 1 — legacy InvoiceCsvExportService
    /// still compiles alongside this.
    /// </summary>
    public class ProformaInvoiceCsvLayout : ICsvLayout
    {
        private readonly SavedProformaInvoice _entity;
        private readonly ProformaInvoiceSnapshot _snapshot;

        public ProformaInvoiceCsvLayout(
            SavedProformaInvoice entity,
            ProformaInvoiceSnapshot snapshot)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        // ── IDocumentLayout ──────────────────────────────────────────────────

        public string DocumentPrefix => "PI";
        public string DocumentId => _entity.ProformaId;
        public string DocumentTitle => "PROFORMA INVOICE";
        public string CompanyName => _entity.CustomerName;
        public string Reference => _entity.ProjectName;
        public DateTime DocumentDate => _entity.IssueDate;

        // ── ICsvLayout ───────────────────────────────────────────────────────

        /// <summary>
        /// Engine provides a configured CsvWriter with UTF-8 BOM stream.
        /// Layout writes header then data rows directly to the writer.
        /// </summary>
        public void WriteRows(CsvWriter writer)
        {
            // ── 13-column header ─────────────────────────────────────────
            writer.WriteField("Spec #");
            writer.WriteField("Spec Name");
            writer.WriteField("Row #");
            writer.WriteField("Glass Ref");
            writer.WriteField("W1");
            writer.WriteField("H1");
            writer.WriteField("W2");
            writer.WriteField("H2");
            writer.WriteField("Qty");
            writer.WriteField("Area (sqm)");
            writer.WriteField("Total Area (sqm)");
            writer.WriteField("Rate (AED)");
            writer.WriteField("Total (AED)");
            writer.NextRecord();

            // ── Data rows ────────────────────────────────────────────────
            if (_snapshot.SpecGroups != null)
            {
                foreach (var g in _snapshot.SpecGroups)
                {
                    string groupLabel =
                        !string.IsNullOrWhiteSpace(g.SelectedSpecName)
                            ? g.SelectedSpecName
                            : (!string.IsNullOrWhiteSpace(g.GroupName)
                                ? g.GroupName
                                : $"Spec Group {g.GroupNumber}");

                    if (g.ItemRows == null) continue;

                    foreach (var r in g.ItemRows)
                    {
                        decimal unitArea = Math.Max(
                            Math.Round((decimal)r.W1 * r.H1
                                / 1_000_000m, 3),
                            0.5m);
                        decimal totalArea = Math.Round(unitArea * r.Qty, 3);
                        decimal total = Math.Round(totalArea * r.Rate, 2);

                        writer.WriteField(g.GroupNumber);
                        writer.WriteField(groupLabel);
                        writer.WriteField(r.SerialNumber);
                        writer.WriteField(r.GlassRef ?? "");
                        writer.WriteField(r.W1);
                        writer.WriteField(r.H1);
                        writer.WriteField(r.W2);
                        writer.WriteField(r.H2);
                        writer.WriteField(r.Qty);
                        writer.WriteField(unitArea.ToString("0.000"));
                        writer.WriteField(totalArea.ToString("0.000"));
                        writer.WriteField(r.Rate.ToString("N2"));
                        writer.WriteField(total.ToString("N2"));
                        writer.NextRecord();
                    }
                }
            }

            // ── Blank separator row ──────────────────────────────────────
            writer.NextRecord();

            // ── Summary rows (11 empty + label + value = 13 fields) ──────
            WriteEmptyThenSummary(writer,
                "Glass Materials Total",
                _entity.GlassMaterialsSubtotal.ToString("N2"));

            WriteEmptyThenSummary(writer,
                "Other Charges Total",
                _entity.OtherChargesSubtotal.ToString("N2"));

            WriteEmptyThenSummary(writer,
                "GRAND TOTAL (VAT INC)",
                _entity.GrandTotalVatInclusive.ToString("N2"));
        }

        // ── Helper ───────────────────────────────────────────────────────────

        private static void WriteEmptyThenSummary(
            CsvWriter writer, string label, string value)
        {
            for (int i = 0; i < 11; i++) writer.WriteField("");
            writer.WriteField(label);
            writer.WriteField(value);
            writer.NextRecord();
        }
    }
}