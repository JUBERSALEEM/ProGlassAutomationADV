using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.TaxInvoice
{
    /// <summary>
    /// Timeline event or status update for a Tax Invoice (e.g. Generated, Paid, Partially Paid).
    /// </summary>
    [Table("TaxInvoiceHistory")]
    public class TaxInvoiceHistory
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Parent Tax Invoice reference (InvoiceNumber).</summary>
        [Required, MaxLength(50)]
        [ForeignKey(nameof(TaxInvoice))]
        public string TaxInvoiceId { get; set; } = "";

        public TaxInvoice? TaxInvoice { get; set; }

        [Required, MaxLength(50)]
        public string Status { get; set; } = "";

        public DateTime EventDate { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string Notes { get; set; } = "";
    }
}