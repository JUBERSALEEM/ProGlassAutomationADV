using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.TaxInvoice
{
    /// <summary>
    /// EF Core entity for one Tax Invoice row in taxinvoices.db.
    /// Following the same JSON-blob strategy as PI, JO, and DN.
    /// </summary>
    [Table("TaxInvoices")]
    public class TaxInvoice
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Unique user-facing invoice reference, e.g. "INV-2026-001".</summary>
        [Required, MaxLength(50)]
        public string InvoiceNumber { get; set; } = "";

        /// <summary>Date the invoice was generated.</summary>
        public DateTime InvoiceDate { get; set; }

        /// <summary>Source Delivery Note reference.</summary>
        [Required, MaxLength(50)]
        public string DeliveryNoteId { get; set; } = "";

        /// <summary>Source Job Order reference.</summary>
        [Required, MaxLength(50)]
        public string JobOrderId { get; set; } = "";

        /// <summary>Source Proforma Invoice reference.</summary>
        [MaxLength(50)]
        public string ProformaId { get; set; } = "";

        [MaxLength(200)]
        public string CustomerName { get; set; } = "";

        [MaxLength(200)]
        public string ProjectName { get; set; } = "";

        [MaxLength(200)]
        public string Salesman { get; set; } = "";

        [MaxLength(50)]
        public string CustomerTrn { get; set; } = "";

        [MaxLength(50)]
        public string CompanyTrn { get; set; } = "";

        // Financial totals (for sorting and quick listing)
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TaxableAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal VatAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal GrandTotal { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "AED";

        [Column(TypeName = "decimal(18, 4)")]
        public decimal ExchangeRate { get; set; } = 1.0m;

        /// <summary>Unpaid | Paid | Partially Paid</summary>
        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "Unpaid";

        // JSON Serialization Snapshot of complete state
        [Required]
        public string FullSnapshotJson { get; set; } = "{}";

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public List<TaxInvoiceItem> Items { get; set; } = new();
        public List<TaxInvoiceHistory> History { get; set; } = new();
    }
}