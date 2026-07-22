using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote
{
    /// <summary>
    /// Pure POCO snapshot of the entire delivery note state for JSON serialization.
    /// Never contains WPF types. Populated by DeliveryNoteMapper.ToSnapshot(),
    /// consumed by DeliveryNoteMapper.DeserializeSnapshot().
    /// </summary>
    public class DeliveryNoteSnapshot
    {
        public int SchemaVersion { get; set; } = 1;

        // ── Header ───────────────────────────────────────────────────────
        public DnHeaderSnapshot Header { get; set; } = new();

        // ── Job Order reference snapshot (read-only context) ─────────────
        public DnJobOrderRefSnapshot JobOrderRef { get; set; } = new();

        // ── Line items ───────────────────────────────────────────────────
        public List<DnItemSnapshot> Items { get; set; } = new();

        // ── Attachments metadata ─────────────────────────────────────────
        public List<DnAttachmentSnapshot> Attachments { get; set; } = new();
    }

    // ── Header ──────────────────────────────────────────────────────────

    public class DnHeaderSnapshot
    {
        public string DeliveryNoteId { get; set; } = "";
        public string JobOrderId { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public int SequenceNumber { get; set; } = 1;
        public string Status { get; set; } = "Draft";
        public string DeliveryType { get; set; } = "Original";
        public bool IsFoc { get; set; }
        public string DriverName { get; set; } = "";
        public string VehicleNumber { get; set; } = "";
        public string ReceiverName { get; set; } = "";
        public DateTime DeliveryDate { get; set; } = DateTime.Now;
        public string DeliveryTime { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    // ── JO reference (embedded from parent Job Order at DN creation) ────

    public class DnJobOrderRefSnapshot
    {
        public string JobOrderId { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public string CompanyLocation { get; set; } = "";

        /// <summary>Spec groups from JO — used to display JO context in DN detail.</summary>
        public List<DnJoSpecGroupSnapshot> SpecGroups { get; set; } = new();
    }

    public class DnJoSpecGroupSnapshot
    {
        public int GroupNumber { get; set; }
        public string GroupName { get; set; } = "";
        public string GlassType { get; set; } = "";
        public string SpecName { get; set; } = "";
        public int TotalQty { get; set; }
        public decimal TotalArea { get; set; }
    }

    // ── Line items ──────────────────────────────────────────────────────

    public class DnItemSnapshot
    {
        public int SpecGroupNumber { get; set; }
        public string SpecGroupName { get; set; } = "";
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = "";
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }
        public int OrderedQty { get; set; }
        public int PreviouslyDeliveredQty { get; set; }
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }
        public int ReworkQty { get; set; }
        public int ReturnedQty { get; set; }
        public string Notes { get; set; } = "";
    }

    // ── Attachment metadata ─────────────────────────────────────────────

    public class DnAttachmentSnapshot
    {
        public string AttachmentType { get; set; } = "Image";
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public string Description { get; set; } = "";
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}