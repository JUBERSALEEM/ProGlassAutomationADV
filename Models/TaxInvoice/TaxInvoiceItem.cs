using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.TaxInvoice
{
    /// <summary>
    /// Line item representing a billed glass row on a Tax Invoice.
    /// Normalized database storage separate from JSON blob.
    /// </summary>
    [Table("TaxInvoiceItems")]
    public class TaxInvoiceItem
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Parent Tax Invoice reference (InvoiceNumber).</summary>
        [Required, MaxLength(50)]
        [ForeignKey(nameof(TaxInvoice))]
        public string TaxInvoiceId { get; set; } = "";

        public TaxInvoice? TaxInvoice { get; set; }

        public int SpecGroupNumber { get; set; }

        [MaxLength(200)]
        public string SpecGroupName { get; set; } = "";

        public int SerialNumber { get; set; }

        [MaxLength(200)]
        public string GlassRef { get; set; } = "";

        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }

        public int DeliveredQty { get; set; }
        public int InvoicedQty { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Rate { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [MaxLength(500)]
        public string Notes { get; set; } = "";
    }
}