using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.CreditNote.SavedCreditNote
{
    /// <summary>
    /// EF Core entity for one Credit Note row in creditnotes.db.
    ///
    /// WHY A CREDIT NOTE EXISTS (ERP hardening — Phase 4)
    ///   A Tax Invoice is a fiscal document. Once issued it is NEVER edited or
    ///   deleted — VAT law requires the original figures to survive. When a
    ///   posted invoice is wrong (over-billed, returned goods, cancelled order)
    ///   the correction is a SEPARATE, negative document that references the
    ///   original: a Credit Note. It reduces the customer's balance by its
    ///   amount and leaves a complete, auditable paper trail.
    ///
    /// RELATIONSHIP
    ///   Credit Note ──references──> Tax Invoice (TaxInvoiceReference)
    ///                └──inherits──> ProformaId (family), so it appears in the
    ///                               same family view and statements as the
    ///                               invoice it corrects.
    ///
    /// NUMBERING (immutable, gap-free within a year)
    ///   CN-YYYY-NNN, e.g. CN-2026-001. The number is assigned once on first
    ///   save and never reused — a voided credit note keeps its number.
    ///
    /// AMOUNTS
    ///   Stored as POSITIVE magnitudes (Subtotal / VatAmount / GrandTotal); the
    ///   document TYPE (credit) is what makes them reduce the ledger. This
    ///   matches how the invoice stores its figures and avoids sign bugs in
    ///   SUM/rounding. Consumers subtract a credit note's GrandTotal.
    ///
    /// Mirrors SavedTaxInvoice deliberately (JSON-blob strategy, TEXT decimals,
    /// explicit repository-set timestamps) so the module behaves like the rest
    /// of the ERP and the same mappers/PDF pipeline can be reused later.
    /// </summary>
    [Table("CreditNotes")]
    public class SavedCreditNote
    {
        [Key]
        public int Id { get; set; }

        // ── Identification ────────────────────────────────────────────────

        /// <summary>User-facing credit-note number, e.g. "CN-2026-001". UNIQUE.</summary>
        [Required, MaxLength(60)]
        public string CreditNoteNumber { get; set; } = "";

        /// <summary>
        /// The Tax Invoice this credit note corrects, e.g. "INV-2026-001".
        /// Required — a credit note never exists on its own.
        /// </summary>
        [Required, MaxLength(60)]
        public string TaxInvoiceReference { get; set; } = "";

        /// <summary>
        /// Parent Proforma Invoice reference inherited from the invoice, e.g.
        /// "PI-2026-001". Family key so the credit note shows alongside the
        /// invoice family and in statements.
        /// </summary>
        [MaxLength(50)]
        public string ProformaId { get; set; } = "";

        /// <summary>Job Order reference for cross-module queries. May be empty.</summary>
        [MaxLength(50)]
        public string JobOrderId { get; set; } = "";

        /// <summary>Customer's Purchase Order number, copied from the invoice.</summary>
        [MaxLength(100)]
        public string PurchaseOrderNumber { get; set; } = "";

        // ── Sequencing within the year ────────────────────────────────────

        /// <summary>Ordinal position within the CN-YYYY series (1, 2, 3…).</summary>
        public int SequenceNumber { get; set; } = 1;

        // ── Reason (required for a fiscal correction) ─────────────────────

        /// <summary>
        /// Why the credit note was raised (goods returned, over-billed,
        /// order cancelled, pricing error…). Required by the validator — a
        /// fiscal correction must always state its reason.
        /// </summary>
        [MaxLength(1000)]
        public string Reason { get; set; } = "";

        // ── Status ────────────────────────────────────────────────────────

        /// <summary>Draft | Issued | Applied | Cancelled</summary>
        [MaxLength(30)]
        public string Status { get; set; } = "Draft";

        // ── Void / soft-close (mirrors the fiscal documents) ──────────────

        /// <summary>True when soft-closed (voided) instead of deleted.</summary>
        public bool IsVoid { get; set; }

        public DateTime? VoidedAtUtc { get; set; }

        [MaxLength(120)]
        public string? VoidedBy { get; set; }

        [MaxLength(1000)]
        public string? VoidReason { get; set; }

        // ── Party ─────────────────────────────────────────────────────────

        [MaxLength(200)]
        public string CustomerName { get; set; } = "";

        [MaxLength(200)]
        public string ProjectName { get; set; } = "";

        [MaxLength(200)]
        public string CompanyName { get; set; } = "";

        [MaxLength(50)]
        public string CompanyTrn { get; set; } = "";

        // ── Dates ───────────────────────────────────────────────────────────

        public DateTime IssueDate { get; set; }

        // ── Money (positive magnitudes; the type makes them a credit) ─────

        [Column(TypeName = "TEXT")] public decimal Subtotal { get; set; }
        [Column(TypeName = "TEXT")] public decimal DiscountAmount { get; set; }
        [Column(TypeName = "TEXT")] public decimal AfterDiscount { get; set; }
        [Column(TypeName = "TEXT")] public decimal VatAmount { get; set; }
        [Column(TypeName = "TEXT")] public decimal GrandTotal { get; set; }

        public int TotalQty { get; set; }
        [Column(TypeName = "TEXT")] public decimal TotalSqm { get; set; }

        // ── Snapshot blob ─────────────────────────────────────────────────

        /// <summary>Full credit-note state serialized as JSON for load/PDF.</summary>
        [Column(TypeName = "TEXT")]
        public string FullSnapshotJson { get; set; } = "{}";

        // ── Timestamps + concurrency ──────────────────────────────────────

        public DateTime CreatedAt { get; set; }

        /// <summary>Set explicitly by repository in InsertAsync + UpdateAsync.</summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// ERP hardening (Phase 5): optimistic-concurrency counter, incremented
        /// by the repository on every successful UpdateAsync.
        /// </summary>
        public int RowVersion { get; set; }

        // ── Navigation ─────────────────────────────────────────────────────

        /// <summary>Line items stored normalized; cascade-deleted with parent.</summary>
        public List<CreditNoteItem> Items { get; set; } = new();
    }

    /// <summary>
    /// One credit-note line item. Mirrors TaxInvoiceItem so the same line
    /// mapper/print pipeline can be reused. Amounts are positive magnitudes.
    /// </summary>
    [Table("CreditNoteItems")]
    public class CreditNoteItem
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Parent credit-note number; FK via HasPrincipalKey.</summary>
        [Required, MaxLength(60)]
        [ForeignKey(nameof(CreditNote))]
        public string CreditNoteNumber { get; set; } = "";

        /// <summary>Navigation property — not loaded by default.</summary>
        public SavedCreditNote? CreditNote { get; set; }

        [MaxLength(60)]
        public string TaxInvoiceReference { get; set; } = "";

        [MaxLength(50)]
        public string ProformaId { get; set; } = "";

        public int SpecGroupNumber { get; set; }

        [MaxLength(200)]
        public string SpecGroupName { get; set; } = "";

        [MaxLength(100)]
        public string GlassType { get; set; } = "";

        public int SerialNumber { get; set; }

        [MaxLength(200)]
        public string GlassRef { get; set; } = "";

        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }

        public int Qty { get; set; }

        [Column(TypeName = "TEXT")] public decimal Sqm { get; set; }
        [Column(TypeName = "TEXT")] public decimal TotalSqm { get; set; }
        [Column(TypeName = "TEXT")] public decimal Rate { get; set; }
        [Column(TypeName = "TEXT")] public decimal Value { get; set; }

        [MaxLength(500)]
        public string Notes { get; set; } = "";
    }
}
