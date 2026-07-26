using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.MasterData
{
    /// <summary>
    /// Master Data entity — one saved project profile.
    /// Auto-saved from Proforma Invoice Step 2 on invoice save.
    ///
    /// Uniqueness: ProjectIdentifierName + CustomerName (case-insensitive).
    /// Same project name for different clients = different entity.
    ///
    /// Used by:
    ///   - Step 3 (future) Project Specs: card auto-fill
    ///   - JO project reference
    ///
    /// DB: masterdata.db → Projects table
    /// </summary>
    [Table("Projects")]
    public class SavedProject
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Project designation name, e.g. "SZR Tower Phase 2".</summary>
        [Required, MaxLength(200)]
        public string ProjectIdentifierName { get; set; } = "";

        /// <summary>Contract or project code/ID.</summary>
        [MaxLength(100)]
        public string ProjectContractNo { get; set; } = "";

        /// <summary>Site delivery location.</summary>
        [MaxLength(300)]
        public string SiteDeliveryLocation { get; set; } = "";

        /// <summary>LPO / PO reference number from client.</summary>
        [MaxLength(100)]
        public string LpoNumber { get; set; } = "";

        /// <summary>
        /// Client name this project belongs to.
        /// Used as part of uniqueness key:
        /// "Tower A" for "Apex LLC" ≠ "Tower A" for "Emirates LLC".
        /// </summary>
        [MaxLength(200)]
        public string CustomerName { get; set; } = "";

        /// <summary>How many times this project has been used.</summary>
        public int UsageCount { get; set; } = 1;

        /// <summary>Last time this project was selected or saved.</summary>
        public DateTime LastUsedAt { get; set; }

        /// <summary>When this record was first created.</summary>
        public DateTime CreatedAt { get; set; }
    }
}