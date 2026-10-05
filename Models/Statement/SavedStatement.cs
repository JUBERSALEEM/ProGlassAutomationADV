// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/SavedStatement.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// FIX vs PHASE 1 VERSION:
//   Added List<StatementLineItem> LineItems navigation property.
//   Required for:
//     • EF Core cascade-delete configuration in DbContext
//     • StatementRepository.InsertAsync (adds line items via nav prop)
//     • StatementRepository.GetByIdAsync (Include(s => s.LineItems))
//     • StatementGeneratorViewModel.RunGenerateAsync (entity.LineItems.Add)
//
// All other fields UNCHANGED from Phase 1 delivery.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.Statement
{
    /// <summary>
    /// EF Core entity for one saved account statement in statements.db.
    /// </summary>
    [Table("Statements")]
    public class SavedStatement
    {
        [Key]
        public int Id { get; set; }

        // ── Statement identification ──────────────────────────────────────

        /// <summary>
        /// User-facing statement number, e.g. "STMT-2026-001". UNIQUE.
        /// </summary>
        [Required, MaxLength(60)]
        public string StatementNumber { get; set; } = "";

        /// <summary>
        /// Human-readable title, e.g. "Account Statement — Alpha Glass LLC".
        /// Derived from filter at generation time.
        /// </summary>
        [MaxLength(300)]
        public string Title { get; set; } = "";

        // ── Filter criteria (indexed for list search) ─────────────────────

        /// <summary>Company name used as primary filter. Empty = not filtered.</summary>
        [MaxLength(200)]
        public string FilterCompany { get; set; } = "";

        /// <summary>Customer name used as primary filter. Empty = not filtered.</summary>
        [MaxLength(200)]
        public string FilterCustomer { get; set; } = "";

        /// <summary>Project name used as primary filter. Empty = not filtered.</summary>
        [MaxLength(200)]
        public string FilterProject { get; set; } = "";

        /// <summary>Salesman name used as primary filter. Empty = not filtered.</summary>
        [MaxLength(200)]
        public string FilterSalesman { get; set; } = "";

        /// <summary>Period start date. Null = no lower bound applied.</summary>
        public DateTime? FilterDateFrom { get; set; }

        /// <summary>Period end date. Null = no upper bound applied.</summary>
        public DateTime? FilterDateTo { get; set; }

        // ── Summary stats (indexed for list display without JSON parse) ────

        /// <summary>Total number of invoices included in this statement.</summary>
        public int InvoiceCount { get; set; }

        /// <summary>Sum of GrandTotal across all included invoices (AED).</summary>
        [Column(TypeName = "TEXT")]
        public decimal TotalInvoiced { get; set; }

        /// <summary>Sum of outstanding balances across all included invoices (AED).</summary>
        [Column(TypeName = "TEXT")]
        public decimal TotalOutstanding { get; set; }

        /// <summary>Sum of overdue amounts across all included invoices (AED).</summary>
        [Column(TypeName = "TEXT")]
        public decimal OverdueAmount { get; set; }

        // ── PDF output path ───────────────────────────────────────────────

        /// <summary>
        /// Full path to the generated PDF on disk.
        /// Empty string if PDF has not been generated yet.
        /// </summary>
        [MaxLength(500)]
        public string PdfPath { get; set; } = "";

        /// <summary>
        /// True if attachments were appended to the combined PDF.
        /// Used to show "includes attachments" badge in list.
        /// </summary>
        public bool HasAttachments { get; set; }

        // ── Full snapshot + filter JSON ───────────────────────────────────

        /// <summary>
        /// Complete computed statement data as JSON (StatementSnapshot).
        /// Allows PDF re-export without re-querying DB.
        /// </summary>
        [Required, Column(TypeName = "TEXT")]
        public string FullSnapshotJson { get; set; } = "{}";

        /// <summary>
        /// Serialized StatementFilter used to generate this statement.
        /// Allows re-run with same criteria.
        /// </summary>
        [Required, Column(TypeName = "TEXT")]
        public string FilterJson { get; set; } = "{}";

        // ── Timestamps ────────────────────────────────────────────────────

        /// <summary>Set explicitly by repository in InsertAsync only.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Set explicitly by repository in InsertAsync + UpdateAsync.</summary>
        public DateTime UpdatedAt { get; set; }

        // ── Navigation (EF relationships) ─────────────────────────────────

        /// <summary>
        /// Line items stored normalized for cross-statement queries.
        /// Cascade-deleted with parent statement.
        /// Populated by StatementRepository.InsertAsync via entity.LineItems.Add().
        /// Loaded by GetByIdAsync via Include(s => s.LineItems).
        /// </summary>
        public List<StatementLineItem> LineItems { get; set; } = new();
    }
}