using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote
{
    /// <summary>
    /// One line item on a delivery note — tracks ordered vs delivered vs balance
    /// at the individual glass row level within a spec group.
    ///
    /// Stored as normalized rows (not in JSON blob) so we can query
    /// delivery history across multiple DNs without parsing JSON.
    /// </summary>
    [Table("DeliveryNoteItems")]
    public class DeliveryNoteItem
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Parent DN reference, e.g. "DN-2026-001-01".</summary>
        [Required, MaxLength(50)]
        public string DeliveryNoteId { get; set; } = "";

        /// <summary>Parent JO reference for cross-DN queries.</summary>
        [Required, MaxLength(50)]
        public string JobOrderId { get; set; } = "";

        // ── Spec identification ──────────────────────────────────────────

        public int SpecGroupNumber { get; set; }

        [MaxLength(200)]
        public string SpecGroupName { get; set; } = "";

        // ── Glass item identification ────────────────────────────────────

        public int SerialNumber { get; set; }

        [MaxLength(200)]
        public string GlassRef { get; set; } = "";

        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }

        // ── Quantity tracking ────────────────────────────────────────────

        /// <summary>Total qty from original JO for this row.</summary>
        public int OrderedQty { get; set; }

        /// <summary>Sum of delivered qty from ALL prior DNs for this row.</summary>
        public int PreviouslyDeliveredQty { get; set; }

        /// <summary>Qty delivered in THIS DN.</summary>
        public int DeliveredQty { get; set; }

        /// <summary>Computed: OrderedQty - PreviouslyDeliveredQty - DeliveredQty.</summary>
        public int BalanceQty { get; set; }

        /// <summary>Qty marked as rework (returned for fixing).</summary>
        public int ReworkQty { get; set; }

        /// <summary>Qty marked as returned/rejected (not re-deliverable).</summary>
        public int ReturnedQty { get; set; }

        // ── Notes ────────────────────────────────────────────────────────

        [MaxLength(500)]
        public string Notes { get; set; } = "";
    }
}