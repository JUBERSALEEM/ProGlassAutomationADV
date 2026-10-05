// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/OrderReport/SavedOrderReport.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — persistence entity (2026-09-06)
//
// PURPOSE:
//   EF Core entity for one saved order report in orderreports.db, plus the
//   lightweight list DTO used by the saved-reports list. Mirrors the
//   Models/Statement/SavedStatement.cs JSON-blob strategy exactly:
//     • indexed filter/summary columns for fast list rendering,
//     • FullSnapshotJson = OrderReportBundle for re-render/re-export,
//     • FilterJson = the criteria that produced this report.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.OrderReport
{
    [Table("OrderReports")]
    public class SavedOrderReport
    {
        [Key]
        public int Id { get; set; }

        /// <summary>User-facing report number, e.g. "REP-2026-001". UNIQUE.</summary>
        [Required, MaxLength(60)]
        public string ReportNumber { get; set; } = "";

        /// <summary>Human-readable title, e.g. "Order Report — Alpha Glass LLC".</summary>
        [MaxLength(300)]
        public string Title { get; set; } = "";

        // ── Filter criteria (indexed for list search) ─────────────────────
        [MaxLength(200)]
        public string FilterCompany { get; set; } = "";
        [MaxLength(200)]
        public string FilterCustomer { get; set; } = "";
        [MaxLength(200)]
        public string FilterProject { get; set; } = "";
        [MaxLength(200)]
        public string FilterSalesman { get; set; } = "";
        public DateTime? FilterDateFrom { get; set; }
        public DateTime? FilterDateTo { get; set; }

        // ── Selected Proforma Invoices (comma-separated) ─────────────────
        [MaxLength(2000)]
        public string ProformaIds { get; set; } = "";

        // ── Summary stats (indexed for list display without JSON parse) ───
        public int PiCount { get; set; }
        public int TotalOrderedQty { get; set; }
        public int TotalDeliveredQty { get; set; }
        public int TotalInvoicedQty { get; set; }
        public int MissingDocuments { get; set; }

        [Column(TypeName = "TEXT")]
        public decimal TotalInvoicedValue { get; set; }

        // ── PDF output path ───────────────────────────────────────────────
        [MaxLength(500)]
        public string PdfPath { get; set; } = "";

        // ── Full snapshot + filter JSON ───────────────────────────────────
        [Required, Column(TypeName = "TEXT")]
        public string FullSnapshotJson { get; set; } = "{}";

        [Required, Column(TypeName = "TEXT")]
        public string FilterJson { get; set; } = "{}";

        // ── Timestamps (set explicitly by repository — BUG-11 pattern) ────
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Lightweight list item — populated from indexed columns only.
    /// </summary>
    public class OrderReportListItem
    {
        public int Id { get; set; }
        public string ReportNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string FilterCompany { get; set; } = "";
        public string FilterCustomer { get; set; } = "";
        public string FilterProject { get; set; } = "";
        public string FilterSalesman { get; set; } = "";
        public DateTime? FilterDateFrom { get; set; }
        public DateTime? FilterDateTo { get; set; }
        public int PiCount { get; set; }
        public int TotalOrderedQty { get; set; }
        public int TotalDeliveredQty { get; set; }
        public int TotalInvoicedQty { get; set; }
        public int MissingDocuments { get; set; }
        public decimal TotalInvoicedValue { get; set; }
        public string PdfPath { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public string CreatedAtDisplay =>
            CreatedAt.ToString("dd MMM yyyy  HH:mm");
        public string TotalInvoicedValueDisplay =>
            $"AED {TotalInvoicedValue:N2}";
        public bool HasPdf =>
            !string.IsNullOrWhiteSpace(PdfPath) &&
            System.IO.File.Exists(PdfPath);
        public bool HasMissingDocuments => MissingDocuments > 0;

        /// <summary>Short subtitle — the most specific filter applied.</summary>
        public string SubTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(FilterCustomer)) return FilterCustomer;
                if (!string.IsNullOrWhiteSpace(FilterProject)) return FilterProject;
                if (!string.IsNullOrWhiteSpace(FilterCompany)) return FilterCompany;
                if (!string.IsNullOrWhiteSpace(FilterSalesman)) return FilterSalesman;
                return "All Records";
            }
        }
    }
}
