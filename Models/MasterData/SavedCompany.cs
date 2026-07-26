using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.MasterData
{
    /// <summary>
    /// Master Data entity — one saved company profile.
    /// Auto-saved from Proforma Invoice Step 1 on invoice save.
    ///
    /// Uniqueness: CompanyName + CompanyTrn (case-insensitive).
    /// Two companies with same name but different TRN = different entities.
    ///
    /// Used by:
    ///   - Step 1 Company Setup: card auto-fill
    ///   - Future modules: DN letterhead, JO company ref
    ///
    /// DB: masterdata.db → Companies table
    /// </summary>
    [Table("Companies")]
    public class SavedCompany
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Company registered name, e.g. "Apex Glazing LLC".</summary>
        [Required, MaxLength(200)]
        public string CompanyName { get; set; } = "";

        /// <summary>UAE Federal Tax Authority TRN (15 digits).</summary>
        [MaxLength(20)]
        public string CompanyTrn { get; set; } = "";

        /// <summary>Headquarters location, e.g. "Dubai, UAE".</summary>
        [MaxLength(300)]
        public string CompanyLocation { get; set; } = "";

        /// <summary>How many times this company has been used in invoices.</summary>
        public int UsageCount { get; set; } = 1;

        /// <summary>Last time this company was selected or saved.</summary>
        public DateTime LastUsedAt { get; set; }

        /// <summary>When this record was first created.</summary>
        public DateTime CreatedAt { get; set; }
    }
}