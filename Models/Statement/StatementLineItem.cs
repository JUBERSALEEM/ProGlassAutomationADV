// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/StatementLineItem.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// PURPOSE:
//   EF Core entity for one invoice line in a saved statement.
//   Stored normalized (not in JSON blob) for cross-statement queries
//   and re-export without full JSON parse.
//
//   One StatementLineItem per Tax Invoice per SavedStatement.
//   Cascade-deleted with parent SavedStatement.
//
//   Financial fields use TEXT column type (same as TaxInvoiceItem)
//   to avoid SQLite REAL rounding on decimal values.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.Statement
{
    /// <summary>
    /// One invoice row in a saved statement.
    /// Mirrors StatementInvoiceRow snapshot but stored in DB for queries.
    /// </summary>
    [Table("StatementLineItems")]
    public class StatementLineItem
    {
        [Key]
        public int Id { get; set; }

        // ── Parent reference ──────────────────────────────────────────────

        /// <summary>FK to SavedStatement.Id. Cascade-deleted.</summary>
        public int StatementId { get; set; }

        /// <summary>Navigation property — not loaded by default.</summary>
        [ForeignKey(nameof(StatementId))]
        public SavedStatement? Statement { get; set; }

        // ── Invoice identity ──────────────────────────────────────────────

        [Required, MaxLength(60)]
        public string InvoiceNumber { get; set; } = "";

        [Required, MaxLength(50)]
        public string ProformaId { get; set; } = "";

        [MaxLength(500)]
        public string DeliveryNoteRef { get; set; } = "";

        [MaxLength(30)]
        public string InvoiceType { get; set; } = "Full";

        [MaxLength(30)]
        public string Status { get; set; } = "";

        // ── Dates ─────────────────────────────────────────────────────────

        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }

        // ── Quantities ────────────────────────────────────────────────────

        public int TotalQty { get; set; }

        [Column(TypeName = "TEXT")]
        public decimal TotalSqm { get; set; }

        // ── Financials ────────────────────────────────────────────────────

        [Column(TypeName = "TEXT")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "TEXT")]
        public decimal VatAmount { get; set; }

        [Column(TypeName = "TEXT")]
        public decimal GrandTotal { get; set; }

        [Column(TypeName = "TEXT")]
        public decimal PaidAmount { get; set; }

        // ── Aging ─────────────────────────────────────────────────────────

        /// <summary>Days past DueDate. 0 if not overdue or paid.</summary>
        public int DaysOverdue { get; set; }
    }
}